using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using TransactionService.Data;
using TransactionService.Services;
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

    // Bind to specific port locally or Heroku PORT
    var transactionPort = Environment.GetEnvironmentVariable("TRANSACTION_PORT");
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(transactionPort))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{transactionPort}");
    }
    else if (!string.IsNullOrEmpty(port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }
    else
    {
        builder.WebHost.UseUrls("http://0.0.0.0:7003");
    }

    // Forwarded headers for reverse proxy (Heroku router)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Database connection
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
        builder.Services.AddDbContext<TransactionDbContext>(options =>
            options.UseNpgsql(connectionString));
    }
    else
    {
        Log.Warning("Using InMemory database for TransactionDbContext (demo mode)");
        builder.Services.AddDbContext<TransactionDbContext>(options =>
             options.UseInMemoryDatabase("TransactionsDemo"));
    }

    builder.Services.Configure<TransactionService.Configuration.FraudDetectionSettings>(
        builder.Configuration.GetSection(TransactionService.Configuration.FraudDetectionSettings.SectionName));
    builder.Services.AddScoped<BudgetAggregationService>();
    builder.Services.AddScoped<IAmlService, AmlService>();
    builder.Services.AddSingleton<IAmlScreeningChannel, AmlScreeningChannel>();
    builder.Services.AddHostedService<AmlScreeningWorker>();
    builder.Services.AddScoped<ITransactionService, TransactionService.Services.TransactionService>();

    // Redis distributed cache configuration
    var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_URL")
                               ?? builder.Configuration["Redis:ConnectionString"];

    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        try
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "transactions:";
            });
            Log.Information("[Cache][Redis] Connected to Redis at {RedisHost}", redisConnectionString.Split(',')[0]);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Cache][Redis] Failed to configure Redis. Falling back to in-memory cache.");
            builder.Services.AddDistributedMemoryCache();
        }
    }
    else
    {
        Log.Warning("[Cache][Memory] Redis not configured. Using in-memory distributed cache (local development mode).");
        builder.Services.AddDistributedMemoryCache();
    }

    builder.Services.AddScoped<ICacheService, CacheService>();

    // Authentication
    var jwtAuthority = builder.Configuration["JWT_AUTHORITY"];
    var jwtAudience = builder.Configuration["JWT_AUDIENCE"];
    var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"];

    if (string.IsNullOrEmpty(jwtAuthority) && string.IsNullOrEmpty(jwtSigningKey))
        throw new InvalidOperationException(
            "JWT configuration is required. Set JWT_AUTHORITY or JWT_SIGNING_KEY in environment variables.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            if (!string.IsNullOrEmpty(jwtAuthority))
            {
                options.Authority = jwtAuthority;
                options.Audience = jwtAudience;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            }
            else if (!string.IsNullOrEmpty(jwtSigningKey))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            }
        });

    // MassTransit configuration
    var amqpUrl = builder.Configuration["CLOUDAMQP_URL"]
                 ?? builder.Configuration["RABBITMQ_URL"]
                 ?? builder.Configuration["AMQP_URL"];

    builder.Services.AddMassTransit(x =>
    {
        if (!string.IsNullOrWhiteSpace(amqpUrl) && Uri.TryCreate(amqpUrl, UriKind.Absolute, out var uri))
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                Log.Information("RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
                cfg.Host(uri);
            });
        }
        else
        {
            Log.Warning("RabbitMQ URL not configured. Using in-memory transport (events will not be published externally).");
            x.UsingInMemory();
        }
    });

    // OpenTelemetry Distributed Tracing
    var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(otelEndpoint))
    {
        var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "TransactionService";
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

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<TransactionService.Validation.CreatePaymentRequestDtoValidator>(ServiceLifetime.Singleton);

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<TransactionService.Validation.FluentValidationFilter>();
    });
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Use forwarded headers before auth
    app.UseForwardedHeaders();

    // Auto-migrate database (only if Relational)
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        if (context.Database.IsRelational())
        {
            try
            {
                await context.Database.MigrateAsync();
                Log.Information("[DB][EF][Transactions] Database migrated");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DB][EF][Transactions] Migration failed");
            }
        }
        else
        {
            Log.Information("[DB][EF][Transactions] Skipping migration (Non-relational provider / InMemory)");
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

    Log.Information("TransactionService starting up");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TransactionService terminated unexpectedly");
}
finally
{
await Log.CloseAndFlushAsync();
}

static string GetConnectionString(IConfiguration configuration)
{
    // Try specific connection string first
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
        return connectionString;

    // Try DATABASE_URL (Heroku style)
    var databaseUrl = configuration["DATABASE_URL"];
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = uri.UserInfo.Split(':')[0],
            Password = uri.UserInfo.Split(':')[1],
            Database = uri.LocalPath.Trim('/'),
            SslMode = SslMode.Require
        };
        return builder.ToString();
    }

    // Fallback explicit env vars
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
