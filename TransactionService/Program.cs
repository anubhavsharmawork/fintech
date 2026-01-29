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

    builder.Services.AddScoped<BudgetAggregationService>();

    // Authentication
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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
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
