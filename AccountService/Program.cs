using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using AccountService.Data;
using AccountService.Consumers;
using Npgsql;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using FluentValidation;
using AccountService.Services;
using AccountService.Policy;
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
    var accountsPort = Environment.GetEnvironmentVariable("ACCOUNTS_SERVICE_PORT");
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(accountsPort))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{accountsPort}");
    }
    else if (!string.IsNullOrEmpty(port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }
    else
    {
        builder.WebHost.UseUrls("http://0.0.0.0:7002");
    }

    // Forwarded headers for reverse proxy (Heroku router)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Database connection
    var connectionString = GetConnectionString(builder.Configuration);
    builder.Services.AddDbContext<AccountDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Authentication
    var authority = builder.Configuration["JWT_AUTHORITY"];
    var audience = builder.Configuration["JWT_AUDIENCE"];
    var signingKey = builder.Configuration["JWT_SIGNING_KEY"];

    if (string.IsNullOrEmpty(authority) && string.IsNullOrEmpty(signingKey))
        throw new InvalidOperationException(
            "JWT configuration is required. Set JWT_AUTHORITY or JWT_SIGNING_KEY in environment variables.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
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

    // IEncryptionService for data-at-rest encryption of sensitive fields
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

    // Account limit policy — externalised thresholds from AccountLimits config section
    builder.Services.Configure<AccountLimitsOptions>(
        builder.Configuration.GetSection(AccountLimitsOptions.SectionName));
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient<IKycStatusClient, HttpKycStatusClient>(client =>
    {
        var baseUrl = builder.Configuration["UserService:BaseUrl"]
                   ?? Environment.GetEnvironmentVariable("USERS_SERVICE_URL")
                   ?? "http://127.0.0.1:7001";
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(5);
    });
    builder.Services.AddScoped<IAccountLimitPolicy, AccountLimitPolicy>();
    builder.Services.AddScoped<IAccountService, AccountService.Services.AccountService>();

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
                options.InstanceName = "accounts:";
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

    // MassTransit configuration
    var amqpUrl = builder.Configuration["CLOUDAMQP_URL"]
                 ?? builder.Configuration["RABBITMQ_URL"]
                 ?? builder.Configuration["AMQP_URL"];

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<ExternalBankDepositConsumer>();

        if (!string.IsNullOrWhiteSpace(amqpUrl) && Uri.TryCreate(amqpUrl, UriKind.Absolute, out var uri))
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                Log.Information("RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
                cfg.Host(uri);
                cfg.ConfigureEndpoints(context);
            });
        }
        else
        {
            Log.Warning("RabbitMQ URL not configured. Using in-memory transport (events will not be published externally).");
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        }
    });

    // OpenTelemetry Distributed Tracing
    var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(otelEndpoint))
    {
        var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "AccountService";
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
    builder.Services.AddValidatorsFromAssemblyContaining<AccountService.Validation.CreateAccountRequestValidator>(ServiceLifetime.Singleton);

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AccountService.Validation.FluentValidationFilter>();
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

    // Auto-migrate database
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        try
        {
            await context.Database.MigrateAsync();
            Log.Information("[DB][EF][Accounts] Database migrated");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DB][EF][Accounts] Migration failed");
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

    Log.Information("AccountService starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AccountService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
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
