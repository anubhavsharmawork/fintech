using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using CorporateBankingService.Data;
using CorporateBankingService.Services;
using Npgsql;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using FluentValidation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var corpPort = Environment.GetEnvironmentVariable("CORPORATE_SERVICE_PORT");
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(corpPort))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{corpPort}");
    }
    else if (!string.IsNullOrEmpty(port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }
    else
    {
        builder.WebHost.UseUrls("http://0.0.0.0:7004");
    }

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    string? connectionString = null;
    try
    {
        connectionString = GetConnectionString(builder.Configuration);
    }
    catch
    {
        Log.Warning("No database connection string configuration found.");
    }

    if (!string.IsNullOrEmpty(connectionString))
    {
        builder.Services.AddDbContext<CorporateDbContext>(options =>
            options.UseNpgsql(connectionString));
    }
    else
    {
        Log.Warning("Using InMemory database for CorporateDbContext (demo mode)");
        builder.Services.AddDbContext<CorporateDbContext>(options =>
            options.UseInMemoryDatabase("CorporateDemo"));
    }

    builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
    builder.Services.AddScoped<IOrganisationService, CorporateBankingService.Services.OrganisationService>();
    builder.Services.AddScoped<IApprovalService, CorporateBankingService.Services.ApprovalService>();

    const string DefaultDemoSigningKey = "demo-signing-key-change-me-0123456789-XYZ987654321";
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var authority = builder.Configuration["JWT_AUTHORITY"];
            var audience = builder.Configuration["JWT_AUDIENCE"];
            var signingKey = builder.Configuration["JWT_SIGNING_KEY"] ?? DefaultDemoSigningKey;

            if (!string.IsNullOrEmpty(authority))
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            }
            else if (!string.IsNullOrEmpty(signingKey))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            }
        });

    var amqpUrl = builder.Configuration["CLOUDAMQP_URL"]
                 ?? builder.Configuration["RABBITMQ_URL"]
                 ?? builder.Configuration["AMQP_URL"];

    builder.Services.AddMassTransit(x =>
    {
        if (!string.IsNullOrWhiteSpace(amqpUrl) && Uri.TryCreate(amqpUrl, UriKind.Absolute, out var uri))
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                Log.Information("CorporateBankingService RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
                cfg.Host(uri);
            });
        }
        else
        {
            Log.Warning("RabbitMQ URL not configured. Using in-memory transport.");
            x.UsingInMemory();
        }
    });

    // OpenTelemetry Distributed Tracing
    var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(otelEndpoint))
    {
        var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "CorporateBankingService";
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otelEndpoint.TrimEnd('/') + "/v1/traces");
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
                    if (!string.IsNullOrWhiteSpace(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otelEndpoint.TrimEnd('/') + "/v1/metrics");
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
                    if (!string.IsNullOrWhiteSpace(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                }));
        Log.Information("[Telemetry] OpenTelemetry tracing and metrics configured for {Service} -> {Endpoint}", serviceName, otelEndpoint);
    }
    else
    {
        Log.Information("[Tracing] OTEL_EXPORTER_OTLP_ENDPOINT not set. Distributed tracing disabled.");
    }

    builder.Services.AddValidatorsFromAssemblyContaining<CorporateBankingService.Validation.CreateOrganisationRequestValidator>(ServiceLifetime.Singleton);

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<CorporateBankingService.Validation.FluentValidationFilter>();
    });

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    builder.Services.AddHealthChecks();

    builder.Services.AddHttpClient("transactions", client =>
    {
        var baseUrl = builder.Configuration["TRANSACTION_SERVICE_URL"] ?? "http://127.0.0.1:7003";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    var app = builder.Build();

    app.UseForwardedHeaders();

    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<CorporateDbContext>();
        if (context.Database.IsRelational())
        {
            try
            {
                await context.Database.MigrateAsync();
                Log.Information("[DB][EF][Corporate] Database migrated");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB][EF][Corporate] Migration failed, ensuring tables exist");
                await context.Database.EnsureCreatedAsync();
            }
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
            Log.Information("[DB][EF][Corporate] Skipping migration (InMemory)");
        }

        // Seed corporate demo data
        var corpUserId = Guid.Parse("10101010-1010-1010-1010-101010101010");
        var corpOrgId = Guid.Parse("20202020-2020-2020-2020-202020202020");

        if (!await context.Organisations.AnyAsync(o => o.Id == corpOrgId))
        {
            context.Organisations.Add(new CorporateBankingService.Models.Organisation
            {
                Id = corpOrgId,
                Name = "Acme Corp Ltd",
                RegistrationNumber = "NZ9876543",
                CreatedByUserId = corpUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            context.OrganisationMembers.Add(new CorporateBankingService.Models.OrganisationMember
            {
                Id = Guid.Parse("21212121-2121-2121-2121-212121212121"),
                OrganisationId = corpOrgId,
                UserId = corpUserId,
                Email = "corpadmin",
                Role = "Admin",
                Status = "Active",
                InvitedAt = DateTime.UtcNow,
                AcceptedAt = DateTime.UtcNow
            });

            context.ApprovalPolicies.Add(new CorporateBankingService.Models.ApprovalPolicy
            {
                Id = Guid.Parse("22222220-2222-2222-2222-222222222220"),
                OrganisationId = corpOrgId,
                RequiredApprovals = 1,
                MonetaryThreshold = 5000m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Seed a sample pending-approval batch
            var batchId = Guid.Parse("70707070-7070-7070-7070-707070707070");
            context.PaymentBatches.Add(new CorporateBankingService.Models.PaymentBatch
            {
                Id = batchId,
                OrganisationId = corpOrgId,
                SubmittedByUserId = corpUserId,
                Status = "PendingApproval",
                Currency = "NZD",
                TotalAmount = 12500.00m,
                ItemCount = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                SubmittedAt = DateTime.UtcNow.AddHours(-6)
            });

            context.PaymentBatchItems.Add(new CorporateBankingService.Models.PaymentBatchItem
            {
                Id = Guid.Parse("71717171-7171-7171-7171-717171717171"),
                PaymentBatchId = batchId,
                SourceAccountId = Guid.Parse("30303030-3030-3030-3030-303030303030"),
                PayeeName = "Supplier A Ltd",
                PayeeAccountNumber = "12-3456-0001234-00",
                Amount = 7500.00m,
                Description = "Invoice #INV-2026-041"
            });

            context.PaymentBatchItems.Add(new CorporateBankingService.Models.PaymentBatchItem
            {
                Id = Guid.Parse("72727272-7272-7272-7272-727272727272"),
                PaymentBatchId = batchId,
                SourceAccountId = Guid.Parse("30303030-3030-3030-3030-303030303030"),
                PayeeName = "Contractor B",
                PayeeAccountNumber = "06-7890-0005678-00",
                Amount = 5000.00m,
                Description = "Monthly retainer - July"
            });

            await context.SaveChangesAsync();
            Log.Information("[DB][Seed][Corporate] Demo organisation, member, policy and batch seeded");
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapControllers();

    Log.Information("CorporateBankingService starting up on port {Port}", corpPort ?? port ?? "7004");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CorporateBankingService terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static string GetConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
        return connectionString;

    var databaseUrl = configuration["DATABASE_URL"];
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var npgBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = uri.UserInfo.Split(':')[0],
            Password = uri.UserInfo.Split(':')[1],
            Database = uri.LocalPath.Trim('/'),
            SslMode = SslMode.Require
        };
        return npgBuilder.ToString();
    }

    var pgHost = configuration["PGHOST"];
    if (!string.IsNullOrEmpty(pgHost))
    {
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = pgHost,
            Port = int.TryParse(configuration["PGPORT"], out var p) ? p : 5432,
            Username = configuration["PGUSER"],
            Password = configuration["PGPASSWORD"],
            Database = configuration["PGDATABASE"] ?? "postgres",
            SslMode = SslMode.Require
        };
        return cs.ToString();
    }

    throw new InvalidOperationException("No database connection string found");
}
