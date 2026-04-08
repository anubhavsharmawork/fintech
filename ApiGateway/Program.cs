using ApiGateway.Data;
using ApiGateway.Configuration;
using ApiGateway.Services;
using ApiGateway.Services.Underwriting;
using ApiGateway.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Json;
using Npgsql;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using ApiGateway.OcelotConfig;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Responses;
using System.Collections.Generic;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Reflection;
using Microsoft.Extensions.Http.Resilience;
using FluentValidation;
using ApiGateway.Validation;
using ApiGateway.Models;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Diagnostics;

Log.Logger = new LoggerConfiguration()
 .MinimumLevel.Information()
 .WriteTo.Console()
 .CreateLogger();

try
{
 var builder = WebApplication.CreateBuilder(args);

 builder.Host.UseSerilog();

 // Bind to Heroku provided PORT if present (works for Container Registry and heroku.yml)
 var port = Environment.GetEnvironmentVariable("PORT");
 if (!string.IsNullOrEmpty(port))
 {
 builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
 }

 // Ensure content root is where the assembly resides (important when launched from /app)
 var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
 if (!string.IsNullOrEmpty(assemblyDir))
 {
 builder.Host.UseContentRoot(assemblyDir);
 }

 // Configuration
 builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

 // Load Ocelot configuration as-is
var ocelotBase = builder.Configuration.GetSection(string.Empty).Get<FileConfiguration>() ?? new FileConfiguration();

// Log DB target very early
 if (TryGetConnectionString(builder.Configuration, out var earlyCs))
 {
 var csb = new NpgsqlConnectionStringBuilder(earlyCs);
 Log.Information("[DB] Resolved connection on startup via env. Target Host={Host} Db={Db} Port={Port} Username={User} SslMode={SslMode}", csb.Host, csb.Database, csb.Port, csb.Username, csb.SslMode);
 }
 else
 {
 Log.Warning("[DB] No connection string resolved on startup. ApiGateway will run in in-memory mode unless DB env is set.");
 }

 // Add EF Core DbContext for persistence (uses same connection logic)
 if (TryGetConnectionString(builder.Configuration, out var efConn))
 {
 Log.Information("[DB][EF] Configuring LedgerDbContext with provided connection string");
 builder.Services.AddDbContext<LedgerDbContext>(opt => opt.UseNpgsql(efConn));

 Log.Information("[DB][EF] Configuring SanctionDbContext with provided connection string");
 builder.Services.AddDbContext<SanctionDbContext>(opt => opt.UseNpgsql(efConn));

 Log.Information("[DB][EF] Configuring CreditDbContext with provided connection string");
 builder.Services.AddDbContext<CreditDbContext>(opt => opt.UseNpgsql(efConn));
 }
 else
 {
 Log.Warning("[DB][EF] No DB connection string resolved. Using InMemory databases for LedgerDbContext, SanctionDbContext and CreditDbContext.");
 builder.Services.AddDbContext<LedgerDbContext>(opt => opt.UseInMemoryDatabase("LedgerInMemory"));
 builder.Services.AddDbContext<SanctionDbContext>(opt => opt.UseInMemoryDatabase("SanctionInMemory"));
 builder.Services.AddDbContext<CreditDbContext>(opt => opt.UseInMemoryDatabase("CreditInMemory"));
 }

 // Toggle Ocelot via env var (default: true)
 var enableOcelot = builder.Configuration.GetValue<bool>("ENABLE_OCELOT", true);

 // Forwarded headers for reverse proxy (Heroku router)
 builder.Services.Configure<ForwardedHeadersOptions>(options =>
 {
 options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
 options.KnownIPNetworks.Clear();
 options.KnownProxies.Clear();
 });

 // Default demo signing key must be >=32 bytes for HS256
 const string DefaultDemoSigningKey = "demo-signing-key-change-me-0123456789-XYZ987654321";

 // Add services
 builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 var authority = builder.Configuration["JWT_AUTHORITY"];
 var audience = builder.Configuration["JWT_AUDIENCE"];
 var signingKey = builder.Configuration["JWT_SIGNING_KEY"] ?? DefaultDemoSigningKey;
 var issuer = builder.Configuration["JWT_ISSUER"] ?? authority ?? "singleDynofin-local";
 var aud = audience ?? builder.Configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";

 if (!string.IsNullOrEmpty(authority))
 {
 options.Authority = authority;
 options.Audience = audience;
 options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
 // Still enforce lifetime and zero clock skew
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateLifetime = true,
 ClockSkew = TimeSpan.Zero
 };
 }
 else if (!string.IsNullOrEmpty(signingKey))
 {
 options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
 {
 ValidateIssuerSigningKey = true,
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
 ValidateIssuer = true,
 ValidIssuer = issuer,
 ValidateAudience = true,
 ValidAudience = aud,
 ValidateLifetime = true,
 ClockSkew = TimeSpan.Zero
 };
 }
 });

 // Rate limiting for auth endpoints
 builder.Services.AddRateLimiter(options =>
 {
 options.AddFixedWindowLimiter("auth", opt =>
 {
 opt.Window = TimeSpan.FromMinutes(1);
 opt.PermitLimit =10;
 opt.QueueLimit =0;
 });
        
        // Rate limiting for financial operations
        options.AddFixedWindowLimiter("transactions", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 200; // allow higher throughput to reduce 429s
            opt.QueueLimit = 20;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
        
        // Rate limiting for account operations
        options.AddFixedWindowLimiter("accounts", opt =>
        {
   opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 20;
        opt.QueueLimit = 0;
    });

        // Rate limiting for feedback submissions
        options.AddFixedWindowLimiter("feedback", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 10;
            opt.QueueLimit = 0;
        });

        // Rate limiting for corporate payment endpoints
        options.AddFixedWindowLimiter("corporate", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 100;
            opt.QueueLimit = 10;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        // Rate limiting for card operations
        options.AddFixedWindowLimiter("cards", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 30;
            opt.QueueLimit = 0;
        });
        
 // Global rejection response
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
   await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Rate limit exceeded. Please try again later." }, cancellationToken);
     };
 });

 // Swagger (dev only â€” completely inaccessible in production)
 if (builder.Environment.IsDevelopment())
 {
 builder.Services.AddEndpointsApiExplorer();
 builder.Services.AddSwaggerGen();
 }

 if (enableOcelot)
 {
 builder.Services.AddSingleton<IFileConfigurationRepository>(sp => new InMemoryFileConfigRepository(ocelotBase));
 builder.Services.AddOcelot();
 }

 builder.Services.AddHealthChecks();
 builder.Services.AddControllers();

 // FTK Sanctioning services
 builder.Services.Configure<UnderwritingSettings>(builder.Configuration.GetSection(UnderwritingSettings.SectionName));
 builder.Services.AddScoped<IKycService, KycService>();
 builder.Services.AddScoped<IAmlService, AmlService>();
 builder.Services.AddScoped<IFtkLedgerService, FtkLedgerService>();
 builder.Services.AddScoped<IUnderwritingRule, AmountThresholdRule>();
 builder.Services.AddScoped<IUnderwritingRule, KycPassedRule>();
 builder.Services.AddScoped<IUnderwritingRule, AmlPassedRule>();
 builder.Services.AddScoped<IUnderwritingRule, NoPriorApprovalsRule>();
 builder.Services.AddScoped<ISanctioningService, SanctioningService>();
 builder.Services.AddSingleton<IBankProvider, MockBankProvider>();
 builder.Services.AddSingleton<ICardIssuingProvider, SimulatedCardProvider>();
 builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(ServiceLifetime.Singleton);

 // Notification services
 builder.Services.AddDbContext<NotificationDbContext>(opt =>
     opt.UseInMemoryDatabase("NotificationInMemory"));
 builder.Services.AddScoped<NotificationPreferenceService>();
 builder.Services.AddSingleton<RecentNotificationStore>();

 // MassTransit configuration (event bus for credit drawdown/repayment events)
 var amqpUrl = builder.Configuration["CLOUDAMQP_URL"]
              ?? builder.Configuration["RABBITMQ_URL"]
              ?? builder.Configuration["AMQP_URL"];

  builder.Services.AddMassTransit(x =>
  {
      if (!string.IsNullOrWhiteSpace(amqpUrl) && Uri.TryCreate(amqpUrl, UriKind.Absolute, out var uri))
      {
          x.UsingRabbitMq((context, cfg) =>
          {
              Log.Information("[Gateway] RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
              cfg.Host(uri);
          });
      }
      else
      {
          Log.Warning("[Gateway] RabbitMQ URL not configured. Using in-memory transport (events will not be published externally).");
          x.UsingInMemory();
      }
  });

 // OpenTelemetry Distributed Tracing
 var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
 if (!string.IsNullOrWhiteSpace(otelEndpoint))
 {
     var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "ApiGateway";
     var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

     builder.Services.AddOpenTelemetry()
         .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
         .WithTracing(tracing => tracing
             .AddAspNetCoreInstrumentation()
             .AddHttpClientInstrumentation()
             .AddEntityFrameworkCoreInstrumentation()
             .AddSource("MassTransit")
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

 // HttpClient for downstream UserService
 builder.Services.AddHttpClient("users", client =>
 {
 var baseUrl = builder.Configuration["USERS_SERVICE_URL"] ?? "http://127.0.0.1:7001";
 client.BaseAddress = new Uri(baseUrl);
 client.Timeout = TimeSpan.FromSeconds(30);
 })
 .AddStandardResilienceHandler(options =>
 {
  options.Retry.MaxRetryAttempts = 3;
  options.Retry.Delay = TimeSpan.FromMilliseconds(500);
  options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
  options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
  options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
  options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
  options.CircuitBreaker.FailureRatio = 0.5;
  options.CircuitBreaker.MinimumThroughput = 5;
  options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
 });

 // Add NpgSql health check when DB configured
 if (TryGetConnectionString(builder.Configuration, out var hcConn))
 {
 try
 {
 builder.Services.AddHealthChecks().AddNpgSql(hcConn, name: "postgres");
 }
 catch (Exception ex)
 {
 Log.Warning(ex, "Failed to register NpgSql health check - missing package or incompatible version");
 }
 }

 var app = builder.Build();

 // Block suspicious / sensitive paths (must be first middleware)
 app.Use(async (ctx, next) =>
 {
     var path = ctx.Request.Path.Value;
     if (path != null &&
         (path.StartsWith("/.git", StringComparison.OrdinalIgnoreCase) ||
          path.StartsWith("/.env", StringComparison.OrdinalIgnoreCase) ||
          path.StartsWith("/public/.env", StringComparison.OrdinalIgnoreCase) ||
          path.StartsWith("/.vercel", StringComparison.OrdinalIgnoreCase) ||
          path.StartsWith("/actuator", StringComparison.OrdinalIgnoreCase) ||
          path.Contains("..")))
     {
         Log.Warning("[Security] Blocked suspicious path: {Path} from {IP}",
             path, ctx.Connection.RemoteIpAddress);
         ctx.Response.StatusCode = 404;
         return;
     }
     await next();
 });

 // Respect reverse proxy headers early
 app.UseForwardedHeaders();

 // Security headers + HSTS (non-development)
 if (!app.Environment.IsDevelopment())
 {
 app.UseHsts();
 }
 app.Use(async (ctx, next) =>
 {
 var headers = ctx.Response.Headers;
 headers["X-Content-Type-Options"] = "nosniff";
 headers["X-Frame-Options"] = "DENY";
 headers["Referrer-Policy"] = "no-referrer";
 headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
 headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://www.vectorlogo.zone; font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
 await next();
 });

 // Correlation ID + Serilog request logging
 app.Use(async (ctx, next) =>
 {
 if (!ctx.Request.Headers.TryGetValue("X-Correlation-ID", out var cid))
 {
 var newCid = Guid.NewGuid().ToString();
 ctx.Response.Headers["X-Correlation-ID"] = newCid;
 }
 else
 {
 ctx.Response.Headers["X-Correlation-ID"] = cid.ToString();
 }
 var traceId = Activity.Current?.TraceId.ToString();
 if (!string.IsNullOrEmpty(traceId))
 {
 ctx.Response.Headers["X-Trace-ID"] = traceId;
 Log.ForContext("CorrelationId", ctx.Response.Headers["X-Correlation-ID"].ToString())
     .ForContext("TraceId", traceId)
     .Debug("Request correlated: CorrelationId={CorrelationId} TraceId={TraceId}",
         ctx.Response.Headers["X-Correlation-ID"].ToString(), traceId);
 }
 await next();
 });
 app.UseSerilogRequestLogging();

 // RESEED: optionally drop ledger tables and clear EF history on startup when explicitly requested
 var reseed = builder.Configuration.GetValue<bool>("RESEED_DB", true)
 || builder.Configuration.GetValue<bool>("RESEED_LEDGER", false)
 || string.Equals(builder.Configuration["DROP_LEDGER_ON_STARTUP"], "true", StringComparison.OrdinalIgnoreCase);
 if (reseed && TryGetConnectionString(app.Configuration, out _))
 {
 try
 {
 await ResetLedgerSchemaAsync(app.Configuration);
 Log.Warning("[DB][EF] Ledger schema reset requested and completed. Proceeding to migrate and seed.");
 }
 catch (Exception rex)
 {
 Log.Error(rex, "[DB][EF] Ledger schema reset failed");
 }
 }

 // Apply EF Core migrations for ledger tables when DB configured
 using (var scope = app.Services.CreateScope())
 {
 var db = scope.ServiceProvider.GetService<LedgerDbContext>();
 if (db != null)
 {
 try
 {
 await db.Database.MigrateAsync();
 Log.Information("[DB][EF] Database migrated for LedgerDbContext");
 }
 catch (Exception mex)
 {
 Log.Error(mex, "[DB][EF] EF migrations failed");
 }
 }
 else
 {
 Log.Warning("[DB][EF] LedgerDbContext not registered (no DB connection)");
 }

 var sanctionDb = scope.ServiceProvider.GetService<SanctionDbContext>();
 if (sanctionDb != null && sanctionDb.Database.IsRelational())
 {
 try
 {
 await sanctionDb.Database.MigrateAsync();
 Log.Information("[DB][EF] Database migrated for SanctionDbContext");
 }
 catch
 {
 // MigrateAsync may fail if there are no migrations assembly; fall through to raw SQL below
 Log.Warning("[DB][EF] SanctionDbContext MigrateAsync skipped or failed; will ensure tables via raw SQL");
 }
 }

 var creditDb = scope.ServiceProvider.GetService<CreditDbContext>();
 if (creditDb != null && creditDb.Database.IsRelational())
 {
 try
 {
 await creditDb.Database.MigrateAsync();
 Log.Information("[DB][EF] Database migrated for CreditDbContext");
 }
 catch
 {
 Log.Warning("[DB][EF] CreditDbContext MigrateAsync skipped or failed; will ensure tables via raw SQL");
 }
 }
 }

 // Ensure tables exist even if EF migrations failed
 if (TryGetConnectionString(app.Configuration, out _))
 {
 try
 {
 await EnsureLedgerTablesAsync(app.Configuration);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] EnsureLedgerTablesAsync failed");
 }
 try
 {
 await EnsureSanctionTablesAsync(app.Configuration);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] EnsureSanctionTablesAsync failed");
 }
 try
 {
 await EnsureSarTableAsync(app.Configuration);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] EnsureSarTableAsync failed");
 }
 try
 {
 await EnsureCorporateTablesAsync(app.Configuration);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] EnsureCorporateTablesAsync failed");
 }
 try
 {
 await EnsureCreditTablesAsync(app.Configuration);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] EnsureCreditTablesAsync failed");
 }
 }

 // Seed demo data
 var enableSeed = builder.Configuration.GetValue<bool>("ENABLE_DEMO_SEED", true);
 if (enableSeed && TryGetConnectionString(app.Configuration, out _))
 {
 try
 {
 await SeedDemoDataAsync(app.Configuration);
 var status = await GetSeedStatusAsync(app.Configuration);
 Log.Information("[DB][Seed] Accounts={Accounts} Transactions={Transactions} DemoTxPresent={DemoTx}", status.AccountsCount, status.TransactionsCount, status.DemoTransactionsPresent);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB][Seed] Seeding failed");
 }
 }

 // Serve built React UI from wwwroot (Dockerfile copies ui/build there)
 app.UseDefaultFiles();
 app.UseStaticFiles();

 // Swagger in Development
 if (app.Environment.IsDevelopment())
 {
 app.UseSwagger();
 app.UseSwaggerUI();
 }

 // SPA navigation fallback for browser refresh: if requesting HTML for SPA routes, serve index.html (no auth)
 app.Use(async (ctx, next) =>
 {
 if (HttpMethods.IsGet(ctx.Request.Method))
 {
 var path = ctx.Request.Path.Value ?? string.Empty;
 var acceptsHtml = ctx.Request.Headers.TryGetValue("Accept", out var accept) && accept.Any(h => h?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
 if (acceptsHtml && (
 path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/accounts", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/transactions", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/sanctions", StringComparison.OrdinalIgnoreCase) ||
 path.StartsWith("/sanctions/", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/compliance", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/admin", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/corporate", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/corporate/dashboard", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/corporate/batches", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/corporate/approvals", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/credit", StringComparison.OrdinalIgnoreCase) ||
 path.Equals("/settings", StringComparison.OrdinalIgnoreCase)))
 {
 await ctx.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
 return;
 }
 }
 await next();
 });

 app.UseAuthentication();
 app.UseAuthorization();

 // Enable rate limiting
 app.UseRateLimiter();

 // Debug endpoints for DB diagnostics - restrict in production
 app.MapGet("/_debug/db", (IConfiguration cfg, IWebHostEnvironment env) =>
 {
 if (!env.IsDevelopment())
 {
 return Results.NotFound();
}
 
 if (TryGetConnectionString(cfg, out var cs))
 {
 var csb = new NpgsqlConnectionStringBuilder(cs);
 // Don't expose sensitive connection details
 return Results.Ok(new { csb.Host, csb.Database, csb.Port, Username = "***", csb.SslMode });
 }
 return Results.NotFound(new { message = "No DB configured" });
 });

 app.MapGet("/_debug/pingdb", async (IConfiguration cfg, IWebHostEnvironment env) =>
 {
 if (!env.IsDevelopment())
 {
 return Results.NotFound();
 }
 
 if (!TryGetConnectionString(cfg, out var cs))
 return Results.NotFound(new { message = "No DB configured" });
 try
 {
 await using var conn = new NpgsqlConnection(cs);
 await conn.OpenAsync();
 await using var cmd = new NpgsqlCommand("select now(), current_database(), current_user", conn);
 await using var r = await cmd.ExecuteReaderAsync();
 await r.ReadAsync();
 var now = r.GetDateTime(0);
 var dbn = r.GetString(1);
 var usr = r.GetString(2);
 return Results.Ok(new { now, database = dbn, user = "***" });
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB][Ping] Ping failed");
 return Results.Problem("Ping failed", statusCode:500);
 }
 });

 // Seed status endpoint - restrict in production
 app.MapGet("/_debug/seedstatus", async (IConfiguration cfg, IWebHostEnvironment env) =>
 {
 if (!env.IsDevelopment())
 {
 return Results.NotFound();
 }
 
 if (!TryGetConnectionString(cfg, out _)) return Results.NotFound(new { message = "No DB configured" });
 try
 {
 var status = await GetSeedStatusAsync(cfg);
 return Results.Ok(status);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB][Seed] Status failed");
 return Results.Problem("Seed status failed", statusCode:500);
 }
 });
 

 // Minimal demo auth endpoints + DB-backed register/login
 app.MapPost("/users/login", async (HttpContext http, LoginRequest req, IConfiguration cfg, IHttpClientFactory httpFactory, CancellationToken ct) =>
 {
 Log.Information("Login attempt for {Email}", req.Email);

 if (string.Equals(req.Email, "demo", StringComparison.OrdinalIgnoreCase) && req.Password == "Demo@2026")
 {
 var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
 var token = GenerateJwt(cfg, userId, "demo");
 // issue refresh cookie
 var refresh = GenerateRefreshJwt(cfg, userId, "demo");
 AppendRefreshCookie(http, refresh);
 Log.Information("Demo login success");
 await WriteAuditLogAsync(cfg, userId, "Login", "User", userId, "Demo login", GetClientIpAddress(http));
 return Results.Ok(new { message = "Login successful", userId = userId.ToString(), token });
 }

 if (string.Equals(req.Email, "corpadmindemo", StringComparison.OrdinalIgnoreCase) && req.Password == "Corp@2026")
 {
 var userId = Guid.Parse("10101010-1010-1010-1010-101010101010");
 var orgId = Guid.Parse("20202020-2020-2020-2020-202020202020");
 var token = GenerateJwt(cfg, userId, "corpadmindemo", "Corporate", orgId, "Admin");
 var refresh = GenerateRefreshJwt(cfg, userId, "corpadmindemo");
 AppendRefreshCookie(http, refresh);
 Log.Information("Corporate demo login success");
 await WriteAuditLogAsync(cfg, userId, "Login", "User", userId, "Corporate demo login", GetClientIpAddress(http));
 return Results.Ok(new { message = "Login successful", userId = userId.ToString(), token });
 }

 try
 {
 if (TryGetConnectionString(cfg, out _))
 {
 Log.Information("[DB] Using DB for /users/login");
 var user = await FindUserByEmailAsync(cfg, req.Email);
 if (user.HasValue)
 {
 var u = user.Value;
 var ok = VerifyPassword(req.Password, u.PasswordHash);
 Log.Information("Local user lookup for {Email} found={Found} passwordOk={Ok}", req.Email, true, ok);
 if (ok)
 {
 var token = GenerateJwt(cfg, u.Id, u.Email, u.ClientType, u.OrganisationId, u.OrganisationRole);
 var refresh = GenerateRefreshJwt(cfg, u.Id, u.Email);
 AppendRefreshCookie(http, refresh);
 await WriteAuditLogAsync(cfg, u.Id, "Login", "User", u.Id, $"DB login for {u.Email}", GetClientIpAddress(http));
 return Results.Ok(new { message = "Login successful", userId = u.Id, token });
 }
 }
 else
 {
 Log.Information("Local user lookup for {Email} found={Found}", req.Email, false);
 }
 }
 else
 {
 Log.Warning("[DB] No DB configured, using in-memory for /users/login");
 // In-memory fallback
 if (InMemoryUsersStore.Users.TryGetValue(req.Email, out var u))
 {
 var ok = VerifyPassword(req.Password, u.PasswordHash);
 Log.Information("InMemory user lookup for {Email} found={Found} passwordOk={Ok}", req.Email, true, ok);
 if (ok)
 {
 var token = GenerateJwt(cfg, u.Id, u.Email);
 var refresh = GenerateRefreshJwt(cfg, u.Id, u.Email);
 AppendRefreshCookie(http, refresh);
 await WriteAuditLogAsync(cfg, u.Id, "Login", "User", u.Id, $"In-memory login for {u.Email}", GetClientIpAddress(http));
 return Results.Ok(new { message = "Login successful", userId = u.Id, token });
 }
 }
 else
 {
 Log.Information("InMemory user lookup for {Email} found={Found}", req.Email, false);
 }

 // Proxy fallback only if USERS_SERVICE_URL is explicitly set and not localhost
 var usvcUrl = cfg["USERS_SERVICE_URL"];
 if (!string.IsNullOrWhiteSpace(usvcUrl) && !usvcUrl.Contains("127.0.0.1") && !usvcUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
 {
 try
 {
 var client = httpFactory.CreateClient("users");
 Log.Information("Proxying login to {BaseAddress}", client.BaseAddress);
 using var resp = await client.PostAsJsonAsync("/api/users/login", req, ct);
 var body = await resp.Content.ReadAsStringAsync(ct);
 return Results.Content(body, resp.Content.Headers.ContentType?.ToString() ?? "application/json", null, (int)resp.StatusCode);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Login proxy failed for {Email}", req.Email);
 }
 }
 } // close else (no DB)
 } // close try
 catch (Exception ex)
 {
 Log.Error(ex, "Local DB login failed for {Email}", req.Email);
 // Fallback: try in-memory, then remote
 if (InMemoryUsersStore.Users.TryGetValue(req.Email, out var u))
 {
 var ok = VerifyPassword(req.Password, u.PasswordHash);
 Log.Information("Fallback InMemory login for {Email} passwordOk={Ok}", req.Email, ok);
 if (ok)
 {
 var token = GenerateJwt(cfg, u.Id, u.Email);
 var refresh = GenerateRefreshJwt(cfg, u.Id, u.Email);
 AppendRefreshCookie(http, refresh);
 await WriteAuditLogAsync(cfg, u.Id, "Login", "User", u.Id, $"Fallback login for {u.Email}", GetClientIpAddress(http));
 return Results.Ok(new { message = "Login successful", userId = u.Id, token });
 }
 }
 var usvcUrl = cfg["USERS_SERVICE_URL"]; 
 if (!string.IsNullOrWhiteSpace(usvcUrl) && !usvcUrl.Contains("127.0.0.1") && !usvcUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
 {
 try
 {
 var client = httpFactory.CreateClient("users");
 Log.Information("Fallback proxying login to {BaseAddress}", client.BaseAddress);
 using var resp = await client.PostAsJsonAsync("/api/users/login", req, ct);
 var body = await resp.Content.ReadAsStringAsync(ct);
 return Results.Content(body, resp.Content.Headers.ContentType?.ToString() ?? "application/json", null, (int)resp.StatusCode);
 }
 catch (Exception pex)
 {
 Log.Error(pex, "Fallback login proxy failed for {Email}", req.Email);
 }
 }
 }

 return Results.Unauthorized();
 }).AddEndpointFilter<ValidationFilter<LoginRequest>>().RequireRateLimiting("auth");

 app.MapPost("/users/register", async (HttpContext http, RegisterRequest req, IConfiguration cfg, IHttpClientFactory httpFactory, CancellationToken ct) =>
 {
 Log.Information("Register attempt for {Email}", req.Email);

 // Basic validation
 if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
 {
 Log.Warning("Register validation failed for {Email}: missing email or password", req.Email);
 return Results.BadRequest(new { message = "Email and password are required" });
 }

 // If DB not configured, use in-memory fallback unless a remote USERS_SERVICE_URL is configured
 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for /users/register");
 var usvcUrl = cfg["USERS_SERVICE_URL"];
 var canProxy = !string.IsNullOrWhiteSpace(usvcUrl) && !usvcUrl.Contains("127.0.0.1") && !usvcUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);

 if (!canProxy)
 {
 // In-memory registration fallback
 if (InMemoryUsersStore.Users.ContainsKey(req.Email))
 {
 Log.Information("InMemory register conflict for {Email}: already exists", req.Email);
 return Results.BadRequest(new { message = "Email already registered" });
 }

 var newUserId = Guid.NewGuid();
 var fnm = string.IsNullOrWhiteSpace(req.FirstName) ? "User" : req.FirstName;
 var lnm = string.IsNullOrWhiteSpace(req.LastName) ? string.Empty : req.LastName;
 InMemoryUsersStore.Users[req.Email] = new InMemUser(newUserId, req.Email, HashPassword(req.Password), fnm, lnm);

 var jwtInMem = GenerateJwt(cfg, newUserId, req.Email);
 var refresh = GenerateRefreshJwt(cfg, newUserId, req.Email);
 AppendRefreshCookie(http, refresh);
 Log.Warning("DB not configured and USERS_SERVICE_URL not set. Using in-memory user store for {Email}", req.Email);
 await WriteAuditLogAsync(cfg, newUserId, "Register", "User", newUserId, $"In-memory registration for {req.Email}", GetClientIpAddress(http));
 return Results.Ok(new { id = newUserId, email = req.Email, firstName = fnm, lastName = lnm, token = jwtInMem });
 }

 Log.Warning("DB not configured. Proxying register for {Email} to {Url}", req.Email, usvcUrl);
 try
 {
 var client = httpFactory.CreateClient("users");
 Log.Information("Proxying register to {BaseAddress}", client.BaseAddress);
 using var resp = await client.PostAsJsonAsync("/api/users/register", req, ct);
 var body = await resp.Content.ReadAsStringAsync(ct);
 return Results.Content(body, resp.Content.Headers.ContentType?.ToString() ?? "application/json", null, (int)resp.StatusCode);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Register proxy failed for {Email}", req.Email);
 // fall back to in-memory as last resort
 if (!InMemoryUsersStore.Users.ContainsKey(req.Email))
 {
 var newUserId = Guid.NewGuid();
 var fnm = string.IsNullOrWhiteSpace(req.FirstName) ? "User" : req.FirstName;
 var lnm = string.IsNullOrWhiteSpace(req.LastName) ? string.Empty : req.LastName;
 InMemoryUsersStore.Users[req.Email] = new InMemUser(newUserId, req.Email, HashPassword(req.Password), fnm, lnm);
 var jwtInMem = GenerateJwt(cfg, newUserId, req.Email);
 var refresh = GenerateRefreshJwt(cfg, newUserId, req.Email);
 AppendRefreshCookie(http, refresh);
 Log.Warning("Register proxy failed; using in-memory user store for {Email}", req.Email);
 await WriteAuditLogAsync(cfg, newUserId, "Register", "User", newUserId, $"Proxy-fallback registration for {req.Email}", GetClientIpAddress(http));
 return Results.Ok(new { id = newUserId, email = req.Email, firstName = fnm, lastName = lnm, token = jwtInMem });
 }
 return Results.BadRequest(new { message = "Email already registered" });
 }
 }

 try
 {
 Log.Information("[DB] Using DB for /users/register");
 var existing = await FindUserByEmailAsync(cfg, req.Email);
 if (existing.HasValue)
 {
 Log.Information("Register conflict for {Email}: already exists", req.Email);
 return Results.BadRequest(new { message = "Email already registered" });
 }

 var newUserId = Guid.NewGuid();
 var fn = string.IsNullOrWhiteSpace(req.FirstName) ? "User" : req.FirstName;
 var ln = string.IsNullOrWhiteSpace(req.LastName) ? string.Empty : req.LastName;

 await InsertUserAsync(cfg, newUserId, req.Email, HashPassword(req.Password), fn, ln);
 await EnsureDefaultAccountForUserAsync(cfg, newUserId);

 var jwt = GenerateJwt(cfg, newUserId, req.Email);
 var refreshDb = GenerateRefreshJwt(cfg, newUserId, req.Email);
 AppendRefreshCookie(http, refreshDb);
 Log.Information("Register success for {Email} -> {UserId}", req.Email, newUserId);
 await WriteAuditLogAsync(cfg, newUserId, "Register", "User", newUserId, $"DB registration for {req.Email}", GetClientIpAddress(http));
 return Results.Ok(new { id = newUserId, email = req.Email, firstName = fn, lastName = ln, token = jwt });
 }
 catch (PostgresException pex) when (pex.SqlState == "23505")
 {
 Log.Warning(pex, "Register unique constraint violation for {Email}", req.Email);
 return Results.BadRequest(new { message = "Email already registered" });
 }
 catch (PostgresException pex) when (pex.SqlState == "23502")
 {
 Log.Error(pex, "Register NOT NULL violation for {Email}", req.Email);
 return Results.BadRequest(new { message = "Invalid data provided" });
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Register failed for {Email}", req.Email);
 // Fallback to in-memory to ensure registration works even if DB temporarily unavailable
 if (!InMemoryUsersStore.Users.ContainsKey(req.Email))
 {
 var newUserId = Guid.NewGuid();
 var fnm = string.IsNullOrWhiteSpace(req.FirstName) ? "User" : req.FirstName;
 var lnm = string.IsNullOrWhiteSpace(req.LastName) ? string.Empty : req.LastName;
 InMemoryUsersStore.Users[req.Email] = new InMemUser(newUserId, req.Email, HashPassword(req.Password), fnm, lnm);
 var jwtInMem = GenerateJwt(cfg, newUserId, req.Email);
 var refresh = GenerateRefreshJwt(cfg, newUserId, req.Email);
 AppendRefreshCookie(http, refresh);
 Log.Warning("DB registration failed; using in-memory user store for {Email}", req.Email);
 await WriteAuditLogAsync(cfg, newUserId, "Register", "User", newUserId, $"DB-fallback registration for {req.Email}", GetClientIpAddress(http));
 return Results.Ok(new { id = newUserId, email = req.Email, firstName = fnm, lastName = lnm, token = jwtInMem });
 }
 return Results.BadRequest(new { message = "Email already registered" });
 }
 }).AddEndpointFilter<ValidationFilter<RegisterRequest>>().RequireRateLimiting("auth");

 // Refresh access token using HttpOnly refresh cookie
 app.MapPost("/users/refresh", (HttpContext http, IConfiguration cfg) =>
 {
 if (!http.Request.Cookies.TryGetValue("rt", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
 {
 return Results.Unauthorized();
 }
 try
 {
 var signingKey = cfg["JWT_SIGNING_KEY"] ?? DefaultDemoSigningKey;
 var issuer = cfg["JWT_ISSUER"] ?? cfg["JWT_AUTHORITY"] ?? "singleDynofin-local";
 var audience = cfg["JWT_AUDIENCE"] ?? "singleDynofin-client";
 var handler = new JwtSecurityTokenHandler();
 var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
 {
 ValidateIssuerSigningKey = true,
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
 ValidateIssuer = true,
 ValidIssuer = issuer,
 ValidateAudience = true,
 ValidAudience = audience,
 ValidateLifetime = true,
 ClockSkew = TimeSpan.Zero
 }, out var validated);

 // Ensure it's a refresh token
 var tokenUse = principal.FindFirst("token_use")?.Value;
 if (!string.Equals(tokenUse, "refresh", StringComparison.Ordinal))
 {
 return Results.Unauthorized();
 }

 // Reject if this refresh token has been revoked
 var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
 if (!string.IsNullOrEmpty(jti) && RevokedTokenStore.IsRevoked(jti))
 {
 Log.Warning("Attempt to use revoked refresh token {Jti}", jti);
 return Results.Unauthorized();
 }

 var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
 var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? principal.FindFirst(ClaimTypes.Name)?.Value ?? "demo";
 if (!Guid.TryParse(sub, out var userId)) return Results.Unauthorized();

 // Revoke the old refresh token (rotate)
 if (!string.IsNullOrEmpty(jti) && validated is JwtSecurityToken jwt)
 {
 RevokedTokenStore.Revoke(jti, jwt.ValidTo);
 }

 // issue new pair
 var newAccess = GenerateJwt(cfg, userId, email);
 var newRefresh = GenerateRefreshJwt(cfg, userId, email);
 AppendRefreshCookie(http, newRefresh);
 return Results.Ok(new { token = newAccess, userId });
 }
 catch (Exception ex)
 {
 Log.Warning(ex, "Refresh token invalid");
 return Results.Unauthorized();
 }
 }).RequireRateLimiting("auth");

 // Logout: revoke refresh token and clear cookie
 app.MapPost("/users/logout", (HttpContext http, IConfiguration cfg) =>
 {
 try
 {
 // Revoke the refresh token if present so it cannot be reused
 if (http.Request.Cookies.TryGetValue("rt", out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
 {
  try
  {
  var signingKey = cfg["JWT_SIGNING_KEY"] ?? DefaultDemoSigningKey;
  var issuer = cfg["JWT_ISSUER"] ?? cfg["JWT_AUTHORITY"] ?? "singleDynofin-local";
  var audience = cfg["JWT_AUDIENCE"] ?? "singleDynofin-client";
  var handler = new JwtSecurityTokenHandler();
  var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
  {
   ValidateIssuerSigningKey = true,
   IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
   ValidateIssuer = true,
   ValidIssuer = issuer,
   ValidateAudience = true,
   ValidAudience = audience,
   ValidateLifetime = false, // allow revoking expired tokens too
   ClockSkew = TimeSpan.Zero
  }, out var validated);

  var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
  if (!string.IsNullOrEmpty(jti) && validated is JwtSecurityToken jwt)
  {
   RevokedTokenStore.Revoke(jti, jwt.ValidTo);
   Log.Information("Refresh token {Jti} revoked during logout", jti);
  }
  }
  catch
  {
  // Token may be malformed; still clear the cookie
  }
 }

 http.Response.Cookies.Delete("rt", new CookieOptions { Path = "/" });
 }
 catch { }
 return Results.Ok(new { message = "Logged out" });
 }).RequireRateLimiting("auth");

 // Update user timezone preference
 app.MapPut("/users/timezone", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();

 TimezoneUpdatePayload? payload;
 try
 {
  payload = await http.Request.ReadFromJsonAsync<TimezoneUpdatePayload>();
 }
 catch
 {
  return Results.BadRequest(new { message = "Invalid request body" });
 }
 if (payload is null)
  return Results.BadRequest(new { message = "Request body is required" });

 // Validate IANA timezone id format
 if (payload.TimeZoneId is not null && (payload.TimeZoneId.Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(payload.TimeZoneId, @"^[A-Za-z0-9_+\-/]+$")))
  return Results.BadRequest(new { message = "Invalid timezone identifier" });

 if (payload.UtcOffsetMinutes.HasValue && (payload.UtcOffsetMinutes.Value < -720 || payload.UtcOffsetMinutes.Value > 840))
  return Results.BadRequest(new { message = "UTC offset must be between -720 and +840 minutes" });

 if (!TryGetConnectionString(cfg, out _))
 {
  Log.Warning("[DB] No DB configured for PUT /users/timezone");
  return Results.Ok(new { message = "Timezone preference updated", timeZoneId = payload.TimeZoneId, utcOffsetMinutes = payload.UtcOffsetMinutes });
 }

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  // Ensure columns exist (safe for first run before migration)
  try
  {
   await using var alter = new NpgsqlCommand(
    @"ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""TimeZoneId"" varchar(100); ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""UtcOffsetMinutes"" integer;", conn);
   await alter.ExecuteNonQueryAsync();
  }
  catch { /* columns may already exist */ }

  var sql = @"UPDATE users_usvc SET ""TimeZoneId""=@tz, ""UtcOffsetMinutes""=@off, ""UpdatedAt""=@now WHERE ""Id""=@uid";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("tz", (object?)payload.TimeZoneId ?? DBNull.Value);
  cmd.Parameters.AddWithValue("off", payload.UtcOffsetMinutes.HasValue ? (object)payload.UtcOffsetMinutes.Value : DBNull.Value);
  cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
  cmd.Parameters.AddWithValue("uid", userId);
  await cmd.ExecuteNonQueryAsync();

  Log.Information("Timezone updated for user {UserId}: {TimeZoneId} (UTC offset {Offset} min)", userId, payload.TimeZoneId ?? "null", payload.UtcOffsetMinutes?.ToString() ?? "null");
  return Results.Ok(new { message = "Timezone preference updated", timeZoneId = payload.TimeZoneId, utcOffsetMinutes = payload.UtcOffsetMinutes });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "Failed to update timezone for user {UserId}", userId);
  return Results.Problem("Failed to update timezone preference", statusCode: 500);
 }
 }).RequireAuthorization();

 // Get current user profile (serves timezone fields for Settings page)
 app.MapGet("/users/profile", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();

 if (!TryGetConnectionString(cfg, out _))
 {
  return Results.Ok(new { id = userId, timeZoneId = (string?)null, utcOffsetMinutes = (int?)null });
 }

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql2 = @"SELECT ""Email"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""ClientType"", ""OrganisationId"", ""OrganisationRole"", ""CompanyName"" FROM users_usvc WHERE ""Id""=@uid LIMIT 1";
  string? tzId = null;
  int? tzOff = null;
  string? email = null, firstName = null, lastName = null, clientType = null, orgRole = null, companyName = null;
  bool isEmailVerified = false;
  Guid? orgId = null;
  bool found = false;

  // Try with timezone columns first
  try
  {
   var sqlFull = @"SELECT ""Email"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""ClientType"", ""OrganisationId"", ""OrganisationRole"", ""CompanyName"", ""TimeZoneId"", ""UtcOffsetMinutes"" FROM users_usvc WHERE ""Id""=@uid LIMIT 1";
   await using var cmd = new NpgsqlCommand(sqlFull, conn);
   cmd.Parameters.AddWithValue("uid", userId);
   await using var reader = await cmd.ExecuteReaderAsync();
   if (await reader.ReadAsync())
   {
    found = true;
    email = reader.IsDBNull(0) ? null : reader.GetString(0);
    firstName = reader.IsDBNull(1) ? null : reader.GetString(1);
    lastName = reader.IsDBNull(2) ? null : reader.GetString(2);
    isEmailVerified = !reader.IsDBNull(3) && reader.GetBoolean(3);
    clientType = reader.IsDBNull(4) ? null : reader.GetString(4);
    orgId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
    orgRole = reader.IsDBNull(6) ? null : reader.GetString(6);
    companyName = reader.IsDBNull(7) ? null : reader.GetString(7);
    tzId = reader.IsDBNull(8) ? null : reader.GetString(8);
    tzOff = reader.IsDBNull(9) ? null : reader.GetInt32(9);
   }
  }
  catch
  {
   // Timezone columns may not exist yet — fall back
   await using var cmd2 = new NpgsqlCommand(sql2, conn);
   cmd2.Parameters.AddWithValue("uid", userId);
   await using var reader2 = await cmd2.ExecuteReaderAsync();
   if (await reader2.ReadAsync())
   {
    found = true;
    email = reader2.IsDBNull(0) ? null : reader2.GetString(0);
    firstName = reader2.IsDBNull(1) ? null : reader2.GetString(1);
    lastName = reader2.IsDBNull(2) ? null : reader2.GetString(2);
    isEmailVerified = !reader2.IsDBNull(3) && reader2.GetBoolean(3);
    clientType = reader2.IsDBNull(4) ? null : reader2.GetString(4);
    orgId = reader2.IsDBNull(5) ? null : reader2.GetGuid(5);
    orgRole = reader2.IsDBNull(6) ? null : reader2.GetString(6);
    companyName = reader2.IsDBNull(7) ? null : reader2.GetString(7);
   }
  }

  if (!found) return Results.NotFound(new { message = "User not found" });

  return Results.Ok(new
  {
   id = userId,
   email,
   firstName,
   lastName,
   isEmailVerified,
   clientType,
   organisationId = orgId,
   organisationRole = orgRole,
   companyName,
   timeZoneId = tzId,
   utcOffsetMinutes = tzOff
  });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "Failed to fetch profile for user {UserId}", userId);
  return Results.Problem("Failed to load profile", statusCode: 500);
 }
 }).RequireAuthorization();

 // List all users (for payee dropdown), requires auth
 app.MapGet("/users/all", async (HttpContext http, IConfiguration cfg) =>
 {
 if (!http.User?.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();

 try
 {
 if (TryGetConnectionString(cfg, out _))
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"Email\", \"FirstName\", \"LastName\" FROM users_usvc ORDER BY \"Email\"";
 await using var cmd = new NpgsqlCommand(sql, conn);
 await using var reader = await cmd.ExecuteReaderAsync();
 var list = new List<object>();
 while (await reader.ReadAsync())
 {
 list.Add(new { id = reader.GetGuid(0), email = reader.GetString(1), firstName = reader.GetString(2), lastName = reader.GetString(3) });
 }
 return Results.Ok(list);
 }
 else
 {
 var list = InMemoryUsersStore.Users.Values.Select(u => new { id = u.Id, email = u.Email, firstName = u.FirstName, lastName = u.LastName }).ToList<object>();
 return Results.Ok(list);
 }
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 try
 {
 await EnsureUsersTableAsync(cfg);
 // retry once after ensuring table
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 const string sql = "SELECT \"Id\", \"Email\", \"FirstName\", \"LastName\" FROM users_usvc ORDER BY \"Email\"";
 await using var cmd = new NpgsqlCommand(sql, conn);
 await using var reader = await cmd.ExecuteReaderAsync();
 var list = new List<object>();
 while (await reader.ReadAsync())
 {
 list.Add(new { id = reader.GetGuid(0), email = reader.GetString(1), firstName = reader.GetString(2), lastName = reader.GetString(3) });
 }
 return Results.Ok(list);
 }
 catch (Exception ex2)
 {
 Log.Error(ex2, "[DB] Ensure users_usvc + retry failed for GET /users/all");
 return Results.Ok(new List<object>());
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Failed to list users");
 return Results.Problem("Failed to list users", statusCode:500);
 }
 }).RequireAuthorization();

 // Local accounts endpoint backed by DB or in-memory fallback
 app.MapGet("/accounts", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 try
 {
 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for GET /accounts");
 // In-memory fallback: ensure default account
 var list = InMemoryData.AccountsByUser.GetOrAdd(userId, _ => new List<InMemAccount>());
 if (list.Count ==0)
 {
 list.Add(new InMemAccount
 {
 Id = Guid.NewGuid(),
 UserId = userId,
 AccountNumber = GenerateAccountNumber(),
 AccountType = "Checking",
 Balance =0m,
 Currency = "NZD",
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 });
 }
 var result = list.Select(a => new { id = a.Id, accountNumber = MaskAccountNumber(a.AccountNumber), accountType = a.AccountType, balance = a.Balance, currency = a.Currency }).ToList<object>();
 return Results.Ok(result);
 }
 Log.Information("[DB] Using DB for GET /accounts");
 var accounts = await GetAccountsAsync(cfg, userId);
 return Results.Ok(accounts);
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 // relation does not exist -> ensure schema then retry
 try
 {
 await EnsureLedgerTablesAsync(cfg);
 var accounts = await GetAccountsAsync(cfg, userId);
 return Results.Ok(accounts);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] Ensure schema + retry failed for GET /accounts");
 return Results.Ok(new List<object>());
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "GetAccounts failed for {UserId}", userId);
 // Fallback to in-memory if DB path fails
 var list = InMemoryData.AccountsByUser.GetOrAdd(userId, _ => new List<InMemAccount>());
 if (list.Count ==0)
 {
 list.Add(new InMemAccount
 {
 Id = Guid.NewGuid(),
 UserId = userId,
 AccountNumber = GenerateAccountNumber(),
 AccountType = "Checking",
 Balance =0m,
 Currency = "NZD",
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 });
 }
 var result = list.Select(a => new { id = a.Id, accountNumber = MaskAccountNumber(a.AccountNumber), accountType = a.AccountType, balance = a.Balance, currency = a.Currency }).ToList<object>();
 return Results.Ok(result);
 }
 }).RequireAuthorization();

 // Create account endpoint
 app.MapPost("/accounts", async (HttpContext http, IConfiguration cfg, CreateAccountRequest req) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 var accountType = string.IsNullOrWhiteSpace(req.AccountType) ? "Checking" : req.AccountType!;
 var currency = string.IsNullOrWhiteSpace(req.Currency) ? "NZD" : req.Currency!;

 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for POST /accounts");
 var list = InMemoryData.AccountsByUser.GetOrAdd(userId, _ => new List<InMemAccount>());
 var id = Guid.NewGuid();
 var acc = new InMemAccount
 {
 Id = id,
 UserId = userId,
 AccountNumber = GenerateAccountNumber(),
 AccountType = accountType,
 Balance =0m,
 Currency = currency,
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 };
 list.Add(acc);
 await WriteAuditLogAsync(cfg, userId, "AccountCreated", "Account", id, $"In-memory account created: {accountType} {currency}", GetClientIpAddress(http));
 return Results.Created($"/accounts/{id}", new { id, accountNumber = MaskAccountNumber(acc.AccountNumber), accountType = acc.AccountType, balance = acc.Balance, currency = acc.Currency });
 }

 try
 {
 Log.Information("[DB] Using DB for POST /accounts");
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var id = Guid.NewGuid();
 var accNum = GenerateAccountNumber();
 var insert = "INSERT INTO \"LedgerAccounts\" (\"Id\", \"UserId\", \"AccountNumber\", \"AccountType\", \"Balance\", \"Currency\", \"CreatedAt\", \"UpdatedAt\") " +
 "VALUES (@id, @uid, @num, @type, @bal, @cur, NOW(), NOW())";
 await using (var cmd = new NpgsqlCommand(insert, conn))
 {
 cmd.Parameters.AddWithValue("id", id);
 cmd.Parameters.AddWithValue("uid", userId);
 cmd.Parameters.AddWithValue("num", accNum);
 cmd.Parameters.AddWithValue("type", accountType);
 cmd.Parameters.AddWithValue("bal", 0m);
 cmd.Parameters.AddWithValue("cur", currency);
 await cmd.ExecuteNonQueryAsync();
 }
 await WriteAuditLogAsync(cfg, userId, "AccountCreated", "Account", id, $"DB account created: {accountType} {currency}", GetClientIpAddress(http));
 return Results.Created($"/accounts/{id}", new { id, accountNumber = MaskAccountNumber(accNum), accountType = accountType, balance = 0m, currency });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 // relation does not exist -> ensure schema then retry once
 try
 {
 await EnsureLedgerTablesAsync(cfg);
 await using var conn2 = new NpgsqlConnection(GetConnectionString(cfg));
 await conn2.OpenAsync();
 var id = Guid.NewGuid();
 var accNum = GenerateAccountNumber();
 var insert = "INSERT INTO \"LedgerAccounts\" (\"Id\", \"UserId\", \"AccountNumber\", \"AccountType\", \"Balance\", \"Currency\", \"CreatedAt\", \"UpdatedAt\") " +
 "VALUES (@id, @uid, @num, @type, @bal, @cur, NOW(), NOW())";
 await using (var cmd = new NpgsqlCommand(insert, conn2))
 {
 cmd.Parameters.AddWithValue("id", id);
 cmd.Parameters.AddWithValue("uid", userId);
 cmd.Parameters.AddWithValue("num", accNum);
 cmd.Parameters.AddWithValue("type", accountType);
 cmd.Parameters.AddWithValue("bal", 0m);
 cmd.Parameters.AddWithValue("cur", currency);
 await cmd.ExecuteNonQueryAsync();
 }
 return Results.Created($"/accounts/{id}", new { id, accountNumber = MaskAccountNumber(accNum), accountType = accountType, balance = 0m, currency });
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] Ensure schema + retry failed for POST /accounts");
 return Results.Problem("Failed to create account", statusCode:500);
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Create account failed for {UserId}", userId);
 return Results.Problem("Failed to create account", statusCode:500);
 }
 }).AddEndpointFilter<ValidationFilter<CreateAccountRequest>>().RequireAuthorization().RequireRateLimiting("accounts");

 // Deposit from external bank account into internal account
 app.MapPost("/accounts/{accountId}/deposit-from-external", async (HttpContext http, IConfiguration cfg, Guid accountId, DepositFromExternalRequest req) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();

 if (req.Amount <= 0)
  return Results.BadRequest(new { message = "Amount must be greater than zero" });

 if (req.ExternalBankAccountId == Guid.Empty)
  return Results.BadRequest(new { message = "External bank account is required" });

 if (!TryGetConnectionString(cfg, out _))
 {
  Log.Warning("[DB] No DB configured, using in-memory for deposit-from-external");
  // Validate internal account ownership
  var accounts = InMemoryData.AccountsByUser.GetOrAdd(userId, _ => new List<InMemAccount>());
  var account = accounts.FirstOrDefault(a => a.Id == accountId);
  if (account is null) return Results.NotFound(new { message = "Internal account not found" });

  // Validate external bank account ownership and active connection
  var extAccounts = InMemoryData.ExternalBankAccountsByUser.GetOrAdd(userId, _ => new List<InMemExternalBankAccount>());
  var extAccount = extAccounts.FirstOrDefault(a => a.Id == req.ExternalBankAccountId);
  if (extAccount is null) return Results.NotFound(new { message = "External bank account not found" });

  var bankConns = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
  var bankConn = bankConns.FirstOrDefault(bc => bc.Id == extAccount.BankConnectionId);
  if (bankConn is null || !string.Equals(bankConn.Status, "Active", StringComparison.OrdinalIgnoreCase))
   return Results.BadRequest(new { message = "Bank connection is not active" });

  // Credit the internal account
  lock (account)
  {
  account.Balance += req.Amount;
  account.UpdatedAt = DateTime.UtcNow;
  }
  var trx = new InMemTransaction(Guid.NewGuid(), accountId, userId, req.Amount, account.Currency, "credit", $"Deposit from {bankConn.BankName}", DateTime.UtcNow);
  InMemoryData.TransactionsByUser.AddOrUpdate(userId, _ => new List<InMemTransaction> { trx }, (_, l) => { l.Add(trx); return l; });

  Log.Information("In-memory deposit from external bank completed: {Amount} into account {AccountId} for user {UserId}", req.Amount, accountId, userId);
  return Results.Ok(new { depositId = trx.Id, accountId, amount = req.Amount, currency = account.Currency, status = "completed" });
 }

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  // Validate internal account ownership
  string? accountCurrency = null;
  var checkAccountSql = "SELECT \"Currency\" FROM \"LedgerAccounts\" WHERE \"Id\" = @aid AND \"UserId\" = @uid";
  await using (var checkCmd = new NpgsqlCommand(checkAccountSql, conn))
  {
   checkCmd.Parameters.AddWithValue("aid", accountId);
   checkCmd.Parameters.AddWithValue("uid", userId);
   await using var reader = await checkCmd.ExecuteReaderAsync();
   if (!await reader.ReadAsync())
    return Results.NotFound(new { message = "Internal account not found" });
   accountCurrency = reader.GetString(0);
  }

  // Validate external bank account ownership and active connection
  var checkExtSql = @"SELECT b.""Status"", b.""BankName"" 
   FROM ""ExternalBankAccounts"" e 
   JOIN ""BankConnections"" b ON e.""BankConnectionId"" = b.""Id"" 
   WHERE e.""Id"" = @eid AND e.""UserId"" = @uid";
  string? bankStatus = null;
  string? bankName = null;
  await using (var extCmd = new NpgsqlCommand(checkExtSql, conn))
  {
   extCmd.Parameters.AddWithValue("eid", req.ExternalBankAccountId);
   extCmd.Parameters.AddWithValue("uid", userId);
   await using var reader = await extCmd.ExecuteReaderAsync();
   if (!await reader.ReadAsync())
    return Results.NotFound(new { message = "External bank account not found" });
   bankStatus = reader.GetString(0);
   bankName = reader.GetString(1);
  }

  if (!string.Equals(bankStatus, "Active", StringComparison.OrdinalIgnoreCase))
   return Results.BadRequest(new { message = "Bank connection is not active" });

  // Credit the internal account via the existing transaction helper
  var currency = accountCurrency ?? "NZD";
  var result = await CreateTransactionAsync(cfg, userId, accountId, req.Amount, currency, "credit", $"Deposit from {bankName}");

  Log.Information("External bank deposit completed: {Amount} {Currency} into account {AccountId} for user {UserId} from external account {ExternalAccountId}",
   req.Amount, currency, accountId, userId, req.ExternalBankAccountId);

  return Results.Ok(new { depositId = result.Id, accountId, amount = req.Amount, currency, status = "completed" });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  try
  {
   await EnsureLedgerTablesAsync(cfg);
   await EnsureBankConnectionTablesAsync(cfg);
   return Results.BadRequest(new { message = "Please retry your deposit" });
  }
  catch (Exception ex)
  {
   Log.Error(ex, "[DB] Ensure schema failed for deposit-from-external");
   return Results.Problem("Failed to process deposit", statusCode: 500);
  }
 }
 catch (KeyNotFoundException)
 {
  return Results.NotFound(new { message = "Account not found" });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "Deposit from external bank failed for user {UserId}, account {AccountId}", userId, accountId);
  return Results.Problem("Failed to process deposit", statusCode: 500);
  }
  }).AddEndpointFilter<ValidationFilter<DepositFromExternalRequest>>().RequireAuthorization();

 // ============= Bank Connections Endpoints (Open Banking Mock) =============

 // Get available banks
 app.MapGet("/bankconnections/available", async (HttpContext http, IBankProvider bankProvider, string? country) =>
 {
  var banks = await bankProvider.GetAvailableBanksAsync(country);
  return Results.Ok(banks);
 }).RequireAuthorization();

 // Get connected banks
 app.MapGet("/bankconnections", async (HttpContext http, IConfiguration cfg) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();

  if (!TryGetConnectionString(cfg, out _))
  {
   var list = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
   return Results.Ok(list.Select(bc => new { bc.Id, bc.BankId, bc.BankName, bc.BankLogo, bc.Status, bc.ConnectedAt }));
  }

  try
  {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   var sql = "SELECT \"Id\", \"BankId\", \"BankName\", \"BankLogo\", \"Status\", \"ConnectedAt\" FROM \"BankConnections\" WHERE \"UserId\" = @uid";
   await using var cmd = new NpgsqlCommand(sql, conn);
   cmd.Parameters.AddWithValue("uid", userId);
   var results = new List<object>();
   await using var reader = await cmd.ExecuteReaderAsync();
   while (await reader.ReadAsync())
   {
    results.Add(new { 
     Id = reader.GetGuid(0), 
     BankId = reader.GetString(1), 
     BankName = reader.GetString(2), 
     BankLogo = reader.GetString(3), 
     Status = reader.GetString(4), 
     ConnectedAt = reader.GetDateTime(5) 
    });
   }
   return Results.Ok(results);
  }
  catch (PostgresException pex) when (pex.SqlState == "42P01")
  {
   await EnsureBankConnectionTablesAsync(cfg);
   return Results.Ok(new List<object>());
  }
  catch (Exception ex)
  {
   Log.Error(ex, "Get bank connections failed for {UserId}", userId);
   return Results.Ok(new List<object>());
  }
 }).RequireAuthorization();

 // Connect to a bank
 app.MapPost("/bankconnections/connect", async (HttpContext http, IConfiguration cfg, IBankProvider bankProvider, ConnectBankRequest req) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();

  var bank = await bankProvider.GetBankByIdAsync(req.BankId);
  if (bank == null) return Results.BadRequest(new { message = "Invalid bank ID" });

  var bankId = bank.Id;
  var bankName = bank.Name;
  var bankLogo = bank.Logo;

  if (!TryGetConnectionString(cfg, out _))
  {
   var list = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
   if (list.Any(bc => bc.BankId == bankId))
    return Results.Conflict(new { message = "Bank already connected" });

   var connectionId = Guid.NewGuid();
   list.Add(new InMemBankConnection { Id = connectionId, UserId = userId, BankId = bankId, BankName = bankName, BankLogo = bankLogo, Status = "Active", ConnectedAt = DateTime.UtcNow });

   // Generate mock accounts
   var accounts = GenerateMockBankAccounts(connectionId, userId, bankName);
   var accList = InMemoryData.ExternalBankAccountsByUser.GetOrAdd(userId, _ => new List<InMemExternalBankAccount>());
   accList.AddRange(accounts);

   return Results.Ok(new { connectionId, bankId, bankName, status = "Active", accountsImported = accounts.Count });
  }

  try
  {
   await EnsureBankConnectionTablesAsync(cfg);
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();

   // Check existing
   var checkSql = "SELECT COUNT(*) FROM \"BankConnections\" WHERE \"UserId\" = @uid AND \"BankId\" = @bid";
   await using (var checkCmd = new NpgsqlCommand(checkSql, conn))
   {
    checkCmd.Parameters.AddWithValue("uid", userId);
    checkCmd.Parameters.AddWithValue("bid", bankId);
    var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);
    if (count > 0) return Results.Conflict(new { message = "Bank already connected" });
   }

   var connectionId = Guid.NewGuid();
   var insertSql = "INSERT INTO \"BankConnections\" (\"Id\", \"UserId\", \"BankId\", \"BankName\", \"BankLogo\", \"Status\", \"ConnectedAt\", \"UpdatedAt\") VALUES (@id, @uid, @bid, @bname, @blogo, 'Active', NOW(), NOW())";
   await using (var insertCmd = new NpgsqlCommand(insertSql, conn))
   {
    insertCmd.Parameters.AddWithValue("id", connectionId);
    insertCmd.Parameters.AddWithValue("uid", userId);
    insertCmd.Parameters.AddWithValue("bid", bankId);
    insertCmd.Parameters.AddWithValue("bname", bankName);
    insertCmd.Parameters.AddWithValue("blogo", bankLogo);
    await insertCmd.ExecuteNonQueryAsync();
   }

   // Generate mock accounts
   var random = new Random();
   var accountTypes = new[] { "Checking", "Savings", "Credit Card" };
   var accountCount = random.Next(1, 4);
   for (int i = 0; i < accountCount; i++)
   {
    var accId = Guid.NewGuid();
    var accType = accountTypes[i % accountTypes.Length];
    var accSql = "INSERT INTO \"ExternalBankAccounts\" (\"Id\", \"BankConnectionId\", \"UserId\", \"ExternalAccountId\", \"AccountName\", \"AccountType\", \"AccountNumber\", \"Balance\", \"Currency\", \"LastSyncedAt\") VALUES (@id, @bcid, @uid, @extid, @name, @type, @num, @bal, 'NZD', NOW())";
    await using var accCmd = new NpgsqlCommand(accSql, conn);
    accCmd.Parameters.AddWithValue("id", accId);
    accCmd.Parameters.AddWithValue("bcid", connectionId);
    accCmd.Parameters.AddWithValue("uid", userId);
    accCmd.Parameters.AddWithValue("extid", $"ext_{Guid.NewGuid():N}");
    accCmd.Parameters.AddWithValue("name", $"{bankName} {accType}");
    accCmd.Parameters.AddWithValue("type", accType);
    accCmd.Parameters.AddWithValue("num", $"****{random.Next(1000, 9999)}");
    accCmd.Parameters.AddWithValue("bal", Math.Round((decimal)(random.NextDouble() * 10000 + 500), 2));
    await accCmd.ExecuteNonQueryAsync();
   }

   return Results.Ok(new { connectionId, bankId, bankName, status = "Active", accountsImported = accountCount });
  }
  catch (Exception ex)
  {
   Log.Error(ex, "Connect bank failed for {UserId}", userId);
   return Results.Problem("Failed to connect bank", statusCode: 500);
   }
  }).AddEndpointFilter<ValidationFilter<ConnectBankRequest>>().RequireAuthorization();

 // Get external bank accounts
 app.MapGet("/bankconnections/accounts", async (HttpContext http, IConfiguration cfg) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();

  if (!TryGetConnectionString(cfg, out _))
  {
   var accList = InMemoryData.ExternalBankAccountsByUser.GetOrAdd(userId, _ => new List<InMemExternalBankAccount>());
   var connList = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
   return Results.Ok(accList.Select(a => {
    var bc = connList.FirstOrDefault(c => c.Id == a.BankConnectionId);
    return new { a.Id, a.AccountName, a.AccountType, a.AccountNumber, a.Balance, a.Currency, a.LastSyncedAt, bankName = bc?.BankName, bankLogo = bc?.BankLogo };
   }));
  }

  try
  {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   var sql = @"SELECT a.""Id"", a.""AccountName"", a.""AccountType"", a.""AccountNumber"", a.""Balance"", a.""Currency"", a.""LastSyncedAt"", b.""BankName"", b.""BankLogo"" 
        FROM ""ExternalBankAccounts"" a 
        JOIN ""BankConnections"" b ON a.""BankConnectionId"" = b.""Id"" 
        WHERE a.""UserId"" = @uid";
   await using var cmd = new NpgsqlCommand(sql, conn);
   cmd.Parameters.AddWithValue("uid", userId);
   var results = new List<object>();
   await using var reader = await cmd.ExecuteReaderAsync();
   while (await reader.ReadAsync())
   {
    results.Add(new { 
     Id = reader.GetGuid(0), 
     AccountName = reader.GetString(1), 
     AccountType = reader.GetString(2), 
     AccountNumber = reader.GetString(3), 
     Balance = reader.GetDecimal(4), 
     Currency = reader.GetString(5), 
     LastSyncedAt = reader.GetDateTime(6),
     bankName = reader.GetString(7),
     bankLogo = reader.GetString(8)
    });
   }
   return Results.Ok(results);
  }
  catch (PostgresException pex) when (pex.SqlState == "42P01")
  {
   await EnsureBankConnectionTablesAsync(cfg);
   return Results.Ok(new List<object>());
  }
  catch (Exception ex)
  {
   Log.Error(ex, "Get external accounts failed for {UserId}", userId);
   return Results.Ok(new List<object>());
  }
 }).RequireAuthorization();

 // Disconnect a bank
 app.MapDelete("/bankconnections/{connectionId}", async (HttpContext http, IConfiguration cfg, Guid connectionId) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();

  if (!TryGetConnectionString(cfg, out _))
  {
   var list = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
   var removed = list.RemoveAll(bc => bc.Id == connectionId && bc.UserId == userId);
   if (removed == 0) return Results.NotFound();
   var accList = InMemoryData.ExternalBankAccountsByUser.GetOrAdd(userId, _ => new List<InMemExternalBankAccount>());
   accList.RemoveAll(a => a.BankConnectionId == connectionId);
   return Results.NoContent();
  }

  try
  {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   var sql = "DELETE FROM \"BankConnections\" WHERE \"Id\" = @id AND \"UserId\" = @uid";
   await using var cmd = new NpgsqlCommand(sql, conn);
   cmd.Parameters.AddWithValue("id", connectionId);
   cmd.Parameters.AddWithValue("uid", userId);
   var rows = await cmd.ExecuteNonQueryAsync();
   return rows > 0 ? Results.NoContent() : Results.NotFound();
  }
  catch (Exception ex)
  {
   Log.Error(ex, "Disconnect bank failed for {UserId}", userId);
   return Results.Problem("Failed to disconnect bank", statusCode: 500);
  }
 }).RequireAuthorization();

 // Sync bank accounts
 app.MapPost("/bankconnections/{connectionId}/sync", async (HttpContext http, IConfiguration cfg, Guid connectionId) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();

  var random = new Random();

  if (!TryGetConnectionString(cfg, out _))
  {
   var list = InMemoryData.BankConnectionsByUser.GetOrAdd(userId, _ => new List<InMemBankConnection>());
   if (!list.Any(bc => bc.Id == connectionId)) return Results.NotFound();
   var accList = InMemoryData.ExternalBankAccountsByUser.GetOrAdd(userId, _ => new List<InMemExternalBankAccount>());
   foreach (var acc in accList.Where(a => a.BankConnectionId == connectionId))
   {
    var change = (decimal)(random.NextDouble() * 0.1 - 0.05);
    acc.Balance += acc.Balance * change;
    acc.LastSyncedAt = DateTime.UtcNow;
   }
   return Results.Ok(new { message = "Accounts synced successfully", syncedAt = DateTime.UtcNow });
  }

  try
  {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();

   // Verify ownership
   var checkSql = "SELECT COUNT(*) FROM \"BankConnections\" WHERE \"Id\" = @id AND \"UserId\" = @uid";
   await using (var checkCmd = new NpgsqlCommand(checkSql, conn))
   {
    checkCmd.Parameters.AddWithValue("id", connectionId);
    checkCmd.Parameters.AddWithValue("uid", userId);
    var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);
    if (count == 0) return Results.NotFound();
   }

   // Update balances with random changes
   var updateSql = "UPDATE \"ExternalBankAccounts\" SET \"Balance\" = \"Balance\" * (1 + @change), \"LastSyncedAt\" = NOW() WHERE \"BankConnectionId\" = @bcid";
   await using var updateCmd = new NpgsqlCommand(updateSql, conn);
   updateCmd.Parameters.AddWithValue("bcid", connectionId);
   updateCmd.Parameters.AddWithValue("change", (decimal)(random.NextDouble() * 0.1 - 0.05));
   await updateCmd.ExecuteNonQueryAsync();

   return Results.Ok(new { message = "Accounts synced successfully", syncedAt = DateTime.UtcNow });
  }
  catch (Exception ex)
  {
   Log.Error(ex, "Sync bank accounts failed for {UserId}", userId);
   return Results.Problem("Failed to sync accounts", statusCode: 500);
  }
 }).RequireAuthorization();

 // ============= End Bank Connections Endpoints =============

 // Local transactions endpoint backed by DB or in-memory
 app.MapGet("/transactions", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 try
 {
 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for GET /transactions");
 var list = InMemoryData.TransactionsByUser.GetOrAdd(userId, _ => new List<InMemTransaction>());
 var result = list.OrderByDescending(t => t.CreatedAt).Select(t => new { id = t.Id, accountId = t.AccountId, amount = t.Amount, currency = t.Currency, type = t.Type, description = t.Description, createdAt = t.CreatedAt, spendingType = t.SpendingType }).ToList<object>();
 return Results.Ok(result);
 }
 Log.Information("[DB] Using DB for GET /transactions");
 var transactions = await GetTransactionsAsync(cfg, userId);
 return Results.Ok(transactions);
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 try
 {
 await EnsureLedgerTablesAsync(cfg);
 var transactions = await GetTransactionsAsync(cfg, userId);
 return Results.Ok(transactions);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] Ensure schema + retry failed for GET /transactions");
 return Results.Problem("Failed to load transactions", statusCode:500);
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "GetTransactions failed for {UserId}", userId);
 return Results.Problem("Failed to load transactions", statusCode:500);
 }
 }).RequireAuthorization().RequireRateLimiting("transactions");

 // Create a new transaction (credit/debit) and update balance (DB or in-memory)
 app.MapPost("/transactions", async (HttpContext http, CreateTransactionRequest req, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();

 // Idempotency key check â€” reject duplicate submissions
 var idempotencyKey = req.IdempotencyKey ?? http.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
 if (!string.IsNullOrWhiteSpace(idempotencyKey))
 {
  var normalizedKey = $"{userId}:{idempotencyKey}";
  if (IdempotencyStore.TryGet(normalizedKey, out var cachedResponse))
  {
   Log.Information("[Idempotency] Duplicate request detected for key {Key} by user {UserId}", idempotencyKey, userId);
   return Results.Conflict(new { message = "A transaction with this idempotency key has already been processed." });
  }
  if (!IdempotencyStore.TryReserve(normalizedKey))
  {
   return Results.Conflict(new { message = "A transaction with this idempotency key is already being processed." });
  }
 }

 // Basic validation
 var typeNorm = (req.Type ?? string.Empty).Trim().ToLowerInvariant();
 if (req.AccountId == Guid.Empty || req.Amount <=0 || (typeNorm != "credit" && typeNorm != "debit"))
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.BadRequest(new { message = "Invalid transaction request" });
 }

 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for POST /transactions");
 // in-memory with lock to prevent race conditions
 if (!InMemoryData.AccountsByUser.TryGetValue(userId, out var list) || !list.Any(a => a.Id == req.AccountId))
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.NotFound(new { message = "Account not found" });
 }
 var acc = list.First(a => a.Id == req.AccountId);
 lock (acc)
 {
 var delta = typeNorm == "credit" ? req.Amount : -req.Amount;
 if (typeNorm == "debit" && acc.Balance < req.Amount)
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.BadRequest(new { message = "Insufficient funds" });
 }
 acc.Balance += delta;
 acc.UpdatedAt = DateTime.UtcNow;
 }
 var trx = new InMemTransaction(Guid.NewGuid(), req.AccountId, userId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? acc.Currency : req.Currency!, typeNorm, req.Description ?? string.Empty, DateTime.UtcNow);
 InMemoryData.TransactionsByUser.AddOrUpdate(userId, _ => new List<InMemTransaction> { trx }, (_, l) => { l.Add(trx); return l; });
 var inMemResult = Results.Created($"/transactions/{trx.Id}", new { id = trx.Id, accountId = trx.AccountId, amount = trx.Amount, currency = trx.Currency, type = trx.Type, description = trx.Description, createdAt = trx.CreatedAt });
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Complete($"{userId}:{idempotencyKey}", inMemResult);
 return inMemResult;
 }

 try
 {
 Log.Information("[DB] Using DB for POST /transactions");
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? "NZD" : req.Currency!, typeNorm, req.Description ?? string.Empty);
 var dbResult = Results.Created($"/transactions/{created.Id}", created);
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Complete($"{userId}:{idempotencyKey}", dbResult);
 return dbResult;
 }
 catch (KeyNotFoundException)
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.NotFound(new { message = "Account not found" });
 }
 catch (UnauthorizedAccessException)
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.Forbid();
 }
 catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient funds", StringComparison.OrdinalIgnoreCase))
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 return Results.BadRequest(new { message = "Insufficient funds" });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 try
 {
 await EnsureLedgerTablesAsync(cfg);
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? "NZD" : req.Currency!, typeNorm, req.Description ?? string.Empty);
 var retryResult = Results.Created($"/transactions/{created.Id}", created);
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Complete($"{userId}:{idempotencyKey}", retryResult);
 return retryResult;
 }
 catch (Exception ex)
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 Log.Error(ex, "[DB] Ensure schema + retry failed for POST /transactions");
 return Results.Problem("Failed to create transaction", statusCode:500);
 }
 }
 catch (Exception ex)
 {
 if (!string.IsNullOrWhiteSpace(idempotencyKey)) IdempotencyStore.Release($"{userId}:{idempotencyKey}");
 Log.Error(ex, "Create transaction failed for {UserId}", userId);
 return Results.Problem("Failed to create transaction", statusCode:500);
 }
 }).AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>().RequireAuthorization().RequireRateLimiting("transactions");

 // Payees endpoints
 app.MapGet("/payees", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for GET /payees");
 var list = InMemoryData.PayeesByUser.GetOrAdd(userId, _ => new List<InMemPayee>());
 if (list.Count ==0)
 {
 list.Add(new InMemPayee(Guid.NewGuid(), userId, "Demo Payee", "DEMO1234567890", DateTime.UtcNow));
 }
 return Results.Ok(list.Select(p => new { id = p.Id, name = p.Name, accountNumber = MaskAccountNumber(p.AccountNumber) }).ToList());
 }
 Log.Information("[DB] Using DB for GET /payees");
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"Name\", \"AccountNumber\" FROM \"LedgerPayees\" WHERE \"UserId\"=@uid ORDER BY \"CreatedAt\" DESC";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 await using var reader = await cmd.ExecuteReaderAsync();
 var res = new List<object>();
 while (await reader.ReadAsync())
 {
 res.Add(new { id = reader.GetGuid(0), name = reader.GetString(1), accountNumber = MaskAccountNumber(reader.GetString(2)) });
 }
 if (res.Count ==0)
 {
 // If DB present but no payees, create demo one
 await reader.DisposeAsync();
 await cmd.DisposeAsync();
 var id = Guid.NewGuid();
 var ins = "INSERT INTO \"LedgerPayees\" (\"Id\", \"UserId\", \"Name\", \"AccountNumber\", \"CreatedAt\") VALUES (@id, @uid, @n, @a, NOW())";
 await using var ic = new NpgsqlCommand(ins, conn);
 ic.Parameters.AddWithValue("id", id);
 ic.Parameters.AddWithValue("uid", userId);
 ic.Parameters.AddWithValue("n", "Demo Payee");
 ic.Parameters.AddWithValue("a", "DEMO1234567890");
 await ic.ExecuteNonQueryAsync();
 return Results.Ok(new[] { new { id, name = "Demo Payee", accountNumber = MaskAccountNumber("DEMO1234567890") } });
 }
 return Results.Ok(res);
 }).RequireAuthorization().RequireRateLimiting("accounts");

 app.MapPost("/payees", async (HttpContext http, IConfiguration cfg, CreatePayeeRequest req) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.AccountNumber))
 return Results.BadRequest(new { message = "Name and account number are required" });

 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for POST /payees");
 var list = InMemoryData.PayeesByUser.GetOrAdd(userId, _ => new List<InMemPayee>());
 var p = new InMemPayee(Guid.NewGuid(), userId, req.Name!, req.AccountNumber!, DateTime.UtcNow);
 list.Add(p);
 return Results.Created($"/payees/{p.Id}", new { id = p.Id, name = p.Name, accountNumber = MaskAccountNumber(p.AccountNumber) });
 }
 Log.Information("[DB] Using DB for POST /payees");
 await using var conn2 = new NpgsqlConnection(GetConnectionString(cfg));
 await conn2.OpenAsync();
 var id2 = Guid.NewGuid();
 var ins2 = "INSERT INTO \"LedgerPayees\" (\"Id\", \"UserId\", \"Name\", \"AccountNumber\", \"CreatedAt\") VALUES (@id, @uid, @n, @a, NOW())";
 await using var cmd2 = new NpgsqlCommand(ins2, conn2);
 cmd2.Parameters.AddWithValue("id", id2);
 cmd2.Parameters.AddWithValue("uid", userId);
 cmd2.Parameters.AddWithValue("n", req.Name);
 cmd2.Parameters.AddWithValue("a", req.AccountNumber);
 await cmd2.ExecuteNonQueryAsync();
 return Results.Created($"/payees/{id2}", new { id = id2, name = req.Name, accountNumber = MaskAccountNumber(req.AccountNumber) });
 }).AddEndpointFilter<ValidationFilter<CreatePayeeRequest>>().RequireAuthorization().RequireRateLimiting("accounts");

 app.MapPost("/payments", async (HttpContext http, CreatePaymentRequest req, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 if (req.AccountId == Guid.Empty || req.Amount <=0) return Results.BadRequest(new { message = "Invalid payment request" });

 string desc;
 if (!string.IsNullOrWhiteSpace(req.Description))
 {
 desc = req.Description!;
 }
 else if (!string.IsNullOrWhiteSpace(req.PayeeName))
 {
 desc = $"Payment to {req.PayeeName} ({req.PayeeAccountNumber})";
 }
 else
 {
 desc = "Payment";
 }

 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for POST /payments");
 if (!InMemoryData.AccountsByUser.TryGetValue(userId, out var list) || !list.Any(a => a.Id == req.AccountId))
 return Results.NotFound(new { message = "Account not found" });
 var acc = list.First(a => a.Id == req.AccountId);
 if (acc.Balance < req.Amount) return Results.BadRequest(new { message = "Insufficient funds" });
 // debit
 var trx = new InMemTransaction(Guid.NewGuid(), req.AccountId, userId, req.Amount, acc.Currency, "debit", desc, DateTime.UtcNow);
 InMemoryData.TransactionsByUser.AddOrUpdate(userId, _ => new List<InMemTransaction> { trx }, (_, l) => { l.Add(trx); return l; });
 acc.Balance -= req.Amount;
 acc.UpdatedAt = DateTime.UtcNow;
 return Results.Created($"/payments/{trx.Id}", new { id = trx.Id });
 }

 try
 {
 Log.Information("[DB] Using DB for POST /payments");
 // DB-backed: create debit transaction
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, "NZD", "debit", desc);
 return Results.Created($"/payments/{created.Id}", new { id = created.Id });
 }
 catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient funds", StringComparison.OrdinalIgnoreCase))
 {
 return Results.BadRequest(new { message = "Insufficient funds" });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
 try
 {
 await EnsureLedgerTablesAsync(cfg);
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, "NZD", "debit", desc);
 return Results.Created($"/payments/{created.Id}", new { id = created.Id });
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] Ensure schema + retry failed for POST /payments");
 return Results.Problem("Failed to create payment", statusCode:500);
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Create payment failed for {UserId}", userId);
 return Results.Problem("Failed to create payment", statusCode:500);
 }
 }).AddEndpointFilter<ValidationFilter<CreatePaymentRequest>>().RequireAuthorization().RequireRateLimiting("transactions");

 // Budget aggregation endpoint
 app.MapGet("/budget/budget", async (HttpContext http, IConfiguration cfg, Guid accountId, DateTime from, DateTime to) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();

 if (accountId == Guid.Empty)
 {
  return Results.BadRequest(new { error = "accountId is required" });
 }

 if (from == default || to == default || from > to)
 {
  return Results.BadRequest(new { error = "Invalid date range" });
 }

 try
 {
  Log.Information("[Budget] Aggregating budget for account {AccountId} from {From} to {To}", accountId, from, to);
  var budget = await GetBudgetAsync(cfg, accountId, from, to);
  return Results.Ok(new
  {
  fun = budget.Fun,
  @fixed = budget.Fixed,
  future = budget.Future,
  total = budget.Total,
  period = new { from = budget.PeriodFrom, to = budget.PeriodTo }
  });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  // Table doesn't exist yet - ensure schema and retry
  try
  {
  await EnsureLedgerTablesAsync(cfg);
  var budget = await GetBudgetAsync(cfg, accountId, from, to);
  return Results.Ok(new
  {
   fun = budget.Fun,
   @fixed = budget.Fixed,
   future = budget.Future,
   total = budget.Total,
   period = new { from = budget.PeriodFrom, to = budget.PeriodTo }
  });
  }
  catch (Exception ex)
  {
  Log.Error(ex, "[Budget] Ensure schema + retry failed");
  return Results.Problem("Failed to load budget", statusCode: 500);
  }
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Budget] GetBudget failed for account {AccountId}", accountId);
  return Results.Problem("Failed to load budget", statusCode: 500);
 }
 }).RequireAuthorization().RequireRateLimiting("transactions");

 // â”€â”€ Local KYC endpoint â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
 app.MapGet("/api/kyc/status", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 try
 {
 if (!TryGetConnectionString(cfg, out _))
 {
  return Results.Ok(new { userId, status = "Pending" });
 }
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"SELECT ""KycStatus"" FROM users_usvc WHERE ""Id""=@uid LIMIT 1";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 var result = await cmd.ExecuteScalarAsync();
 var status = result as string ?? "Pending";
 return Results.Ok(new { userId, status });
 }
 catch (PostgresException pex) when (pex.SqlState == "42703") // column does not exist
 {
 Log.Warning("[KYC] KycStatus column not found, returning default");
 return Results.Ok(new { userId, status = "Pending" });
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[KYC] Failed to get KYC status for user {UserId}", userId);
 return Results.Problem("Failed to load KYC status", statusCode: 500);
 }
 }).RequireAuthorization();

 // â”€â”€ Local SAR endpoint â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
 app.MapGet("/api/sar", async (HttpContext http, IConfiguration cfg) =>
 {
 var userId = GetUserIdFromToken(http.User);
 if (userId == Guid.Empty) return Results.Unauthorized();
 try
 {
 if (!TryGetConnectionString(cfg, out _))
 {
  return Results.Ok(Array.Empty<object>());
 }
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 // Check if the table exists first
 var checkSql = @"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'SuspiciousActivityReports')";
 await using var checkCmd = new NpgsqlCommand(checkSql, conn);
 var tableExists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
 if (!tableExists)
 {
  return Results.Ok(Array.Empty<object>());
 }
 var sql = @"SELECT ""Id"", ""TransactionId"", ""UserId"", ""Amount"", ""Currency"", ""Reason"", ""RiskLevel"", ""FlaggedAt"", ""Status""
  FROM ""SuspiciousActivityReports""
  WHERE ""UserId""=@uid
  ORDER BY ""FlaggedAt"" DESC";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 await using var reader = await cmd.ExecuteReaderAsync();
 var reports = new List<object>();
 while (await reader.ReadAsync())
 {
  reports.Add(new
  {
  id = reader.GetGuid(0),
  transactionId = reader.GetGuid(1),
  userId = reader.GetGuid(2),
  amount = reader.GetDecimal(3),
  currency = reader.GetString(4),
  reason = reader.GetString(5),
  riskLevel = reader.GetString(6),
  flaggedAt = reader.GetDateTime(7),
  status = reader.GetString(8)
  });
 }
 return Results.Ok(reports);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[SAR] Failed to get SAR reports for user {UserId}", userId);
 return Results.Problem("Failed to load SAR reports", statusCode: 500);
 }
 }).RequireAuthorization();

 // ====== Corporate Banking Inline Endpoints ======
 // These replace Ocelot proxy to port 7004 (CorporateBankingService) for Heroku single-dyno deployment.

 // GET /api/organisations/{organisationId}
 app.MapGet("/api/organisations/{organisationId}", async (HttpContext http, Guid organisationId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var callerOrgId) || callerOrgId != organisationId)
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Ok(new { id = organisationId, name = "Acme Corp Ltd", registrationNumber = "NZ9876543", createdAt = DateTime.UtcNow });

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql = @"SELECT ""Id"", ""Name"", ""RegistrationNumber"", ""CreatedAt"" FROM corp_organisations WHERE ""Id""=@id";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("id", organisationId);
  await using var reader = await cmd.ExecuteReaderAsync();
  if (!await reader.ReadAsync()) return Results.NotFound();
  return Results.Ok(new { id = reader.GetGuid(0), name = reader.GetString(1), registrationNumber = reader.GetString(2), createdAt = reader.GetDateTime(3) });
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  await EnsureCorporateTablesAsync(cfg);
  return Results.Ok(new { id = organisationId, name = "Acme Corp Ltd", registrationNumber = "NZ9876543", createdAt = DateTime.UtcNow });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get organisation {OrgId}", organisationId);
  return Results.Problem("Failed to load organisation", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/organisations/{organisationId}/members
 app.MapGet("/api/organisations/{organisationId}/members", async (HttpContext http, Guid organisationId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var callerOrgId) || callerOrgId != organisationId)
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Ok(new List<object>());

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql = @"SELECT ""Id"", ""UserId"", ""Email"", ""Role"", ""Status"", ""InvitedAt"", ""AcceptedAt"" FROM corp_organisation_members WHERE ""OrganisationId""=@orgid ORDER BY ""InvitedAt""";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("orgid", organisationId);
  var results = new List<object>();
  await using var reader = await cmd.ExecuteReaderAsync();
  while (await reader.ReadAsync())
  {
  results.Add(new { id = reader.GetGuid(0), userId = reader.GetGuid(1), email = reader.GetString(2), role = reader.GetString(3), status = reader.GetString(4), invitedAt = reader.GetDateTime(5), acceptedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6) });
  }
  return Results.Ok(results);
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  await EnsureCorporateTablesAsync(cfg);
  return Results.Ok(new List<object>());
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get members for org {OrgId}", organisationId);
  return Results.Problem("Failed to load members", statusCode: 500);
 }
 }).RequireAuthorization();

 // POST /api/organisations/{organisationId}/members/invite
 app.MapPost("/api/organisations/{organisationId}/members/invite", async (HttpContext http, Guid organisationId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var callerOrgId) || callerOrgId != organisationId)
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 var body = await http.Request.ReadFromJsonAsync<InviteMemberInlineRequest>();
 if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Role))
  return Results.BadRequest(new { message = "Email and role are required." });

 if (!TryGetConnectionString(cfg, out _))
  return Results.Problem("No DB configured", statusCode: 500);

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  // Check duplicate
  var chkSql = @"SELECT COUNT(1) FROM corp_organisation_members WHERE ""OrganisationId""=@orgid AND ""Email""=@email AND ""Status""<>'Removed'";
  await using (var chk = new NpgsqlCommand(chkSql, conn))
  {
  chk.Parameters.AddWithValue("orgid", organisationId);
  chk.Parameters.AddWithValue("email", body.Email.Trim().ToLowerInvariant());
  var count = (long)(await chk.ExecuteScalarAsync() ?? 0L);
  if (count > 0) return Results.BadRequest(new { message = "Member already exists in this organisation." });
  }

  var memberId = Guid.NewGuid();
  var insSql = @"INSERT INTO corp_organisation_members (""Id"", ""OrganisationId"", ""UserId"", ""Email"", ""Role"", ""Status"", ""InvitedAt"")
  VALUES (@id, @orgid, @uid, @email, @role, 'Invited', NOW())";
  await using var cmd = new NpgsqlCommand(insSql, conn);
  cmd.Parameters.AddWithValue("id", memberId);
  cmd.Parameters.AddWithValue("orgid", organisationId);
  cmd.Parameters.AddWithValue("uid", Guid.Empty);
  cmd.Parameters.AddWithValue("email", body.Email.Trim().ToLowerInvariant());
  cmd.Parameters.AddWithValue("role", body.Role);
  await cmd.ExecuteNonQueryAsync();

  return Results.Ok(new { id = memberId, userId = Guid.Empty, email = body.Email.Trim().ToLowerInvariant(), role = body.Role, status = "Invited", invitedAt = DateTime.UtcNow, acceptedAt = (DateTime?)null });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to invite member to org {OrgId}", organisationId);
  return Results.Problem("Failed to invite member", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/paymentbatches
 app.MapGet("/api/paymentbatches", async (HttpContext http, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Ok(new List<object>());

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql = @"SELECT ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""
  FROM corp_payment_batches WHERE ""OrganisationId""=@orgid ORDER BY ""CreatedAt"" DESC";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("orgid", orgId);
  var results = new List<object>();
  await using var reader = await cmd.ExecuteReaderAsync();
  while (await reader.ReadAsync())
  {
  results.Add(new { id = reader.GetGuid(0), organisationId = reader.GetGuid(1), submittedByUserId = reader.GetGuid(2), status = reader.GetString(3), currency = reader.GetString(4), totalAmount = reader.GetDecimal(5), itemCount = reader.GetInt32(6), createdAt = reader.GetDateTime(7), submittedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8), executedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9) });
  }
  return Results.Ok(results);
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  await EnsureCorporateTablesAsync(cfg);
  return Results.Ok(new List<object>());
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get payment batches for org");
  return Results.Problem("Failed to load payment batches", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/paymentbatches/{batchId}
 app.MapGet("/api/paymentbatches/{batchId}", async (HttpContext http, Guid batchId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.NotFound();

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  // Batch header
  var batchSql = @"SELECT ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""
  FROM corp_payment_batches WHERE ""Id""=@bid AND ""OrganisationId""=@orgid";
  await using var bCmd = new NpgsqlCommand(batchSql, conn);
  bCmd.Parameters.AddWithValue("bid", batchId);
  bCmd.Parameters.AddWithValue("orgid", orgId);
  await using var br = await bCmd.ExecuteReaderAsync();
  if (!await br.ReadAsync()) return Results.NotFound();
  var batch = new { id = br.GetGuid(0), organisationId = br.GetGuid(1), submittedByUserId = br.GetGuid(2), status = br.GetString(3), currency = br.GetString(4), totalAmount = br.GetDecimal(5), itemCount = br.GetInt32(6), createdAt = br.GetDateTime(7), submittedAt = br.IsDBNull(8) ? (DateTime?)null : br.GetDateTime(8), executedAt = br.IsDBNull(9) ? (DateTime?)null : br.GetDateTime(9) };
  await br.CloseAsync();

  // Items
  var itemsSql = @"SELECT ""SourceAccountId"", ""PayeeName"", ""PayeeAccountNumber"", ""Amount"", ""Description"" FROM corp_payment_batch_items WHERE ""PaymentBatchId""=@bid";
  await using var iCmd = new NpgsqlCommand(itemsSql, conn);
  iCmd.Parameters.AddWithValue("bid", batchId);
  var items = new List<object>();
  await using var ir = await iCmd.ExecuteReaderAsync();
  while (await ir.ReadAsync())
  {
  items.Add(new { sourceAccountId = ir.GetGuid(0), payeeName = ir.GetString(1), payeeAccountNumber = ir.IsDBNull(2) ? null : ir.GetString(2), amount = ir.GetDecimal(3), description = ir.IsDBNull(4) ? null : ir.GetString(4) });
  }
  await ir.CloseAsync();

  // Approvals
  var appSql = @"SELECT ""Id"", ""ApprovedByUserId"", ""Decision"", ""Comments"", ""DecidedAt"" FROM corp_approval_records WHERE ""PaymentBatchId""=@bid";
  await using var aCmd = new NpgsqlCommand(appSql, conn);
  aCmd.Parameters.AddWithValue("bid", batchId);
  var approvals = new List<object>();
  await using var ar = await aCmd.ExecuteReaderAsync();
  while (await ar.ReadAsync())
  {
  approvals.Add(new { id = ar.GetGuid(0), approvedByUserId = ar.GetGuid(1), decision = ar.GetString(2), comments = ar.IsDBNull(3) ? null : ar.GetString(3), decidedAt = ar.GetDateTime(4) });
  }

  return Results.Ok(new { batch.id, batch.organisationId, batch.submittedByUserId, batch.status, batch.currency, batch.totalAmount, batch.itemCount, batch.createdAt, batch.submittedAt, batch.executedAt, items, approvals });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get batch detail {BatchId}", batchId);
  return Results.Problem("Failed to load batch detail", statusCode: 500);
 }
 }).RequireAuthorization();

 // POST /api/paymentbatches (create)
 app.MapPost("/api/paymentbatches", async (HttpContext http, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "Treasurer", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 var userIdClaim = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst("id")?.Value;
 if (!Guid.TryParse(userIdClaim, out var userId))
  return Results.Unauthorized();

 var body = await http.Request.ReadFromJsonAsync<CreateBatchInlineRequest>();
 if (body is null || body.Items is null || body.Items.Count == 0)
  return Results.BadRequest(new { message = "At least one payment item is required." });

 // Validate currency - only $ and FTK allowed for corporate
 var allowedCurrencies = new[] { "$", "FTK" };
 var currency = (body.Currency ?? "$").Trim().ToUpperInvariant();
 if (currency != "FTK") currency = "$";
 if (!allowedCurrencies.Contains(currency))
  return Results.BadRequest(new { message = $"Invalid currency. Allowed: {string.Join(", ", allowedCurrencies)}" });

 if (!TryGetConnectionString(cfg, out _))
  return Results.Problem("No DB configured", statusCode: 500);

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  var batchId = Guid.NewGuid();
  var totalAmount = body.Items.Sum(i => i.Amount);
  var itemCount = body.Items.Count;

  var insBatch = @"INSERT INTO corp_payment_batches (""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"")
  VALUES (@id, @orgid, @uid, 'Draft', @cur, @total, @cnt, NOW())";
  await using (var bCmd = new NpgsqlCommand(insBatch, conn))
  {
  bCmd.Parameters.AddWithValue("id", batchId);
  bCmd.Parameters.AddWithValue("orgid", orgId);
  bCmd.Parameters.AddWithValue("uid", userId);
  bCmd.Parameters.AddWithValue("cur", currency);
  bCmd.Parameters.AddWithValue("total", totalAmount);
  bCmd.Parameters.AddWithValue("cnt", itemCount);
  await bCmd.ExecuteNonQueryAsync();
  }

  var insItem = @"INSERT INTO corp_payment_batch_items (""Id"", ""PaymentBatchId"", ""SourceAccountId"", ""PayeeName"", ""PayeeAccountNumber"", ""Amount"", ""Description"")
  VALUES (@id, @bid, @srcacc, @payee, @payeeacc, @amt, @desc)";
  foreach (var item in body.Items)
  {
  await using var iCmd = new NpgsqlCommand(insItem, conn);
  iCmd.Parameters.AddWithValue("id", Guid.NewGuid());
  iCmd.Parameters.AddWithValue("bid", batchId);
  iCmd.Parameters.AddWithValue("srcacc", item.SourceAccountId);
  iCmd.Parameters.AddWithValue("payee", item.PayeeName.Trim());
  iCmd.Parameters.AddWithValue("payeeacc", (object?)item.PayeeAccountNumber?.Trim() ?? DBNull.Value);
  iCmd.Parameters.AddWithValue("amt", item.Amount);
  iCmd.Parameters.AddWithValue("desc", (object?)item.Description?.Trim() ?? DBNull.Value);
  await iCmd.ExecuteNonQueryAsync();
  }

  Log.Information("[Corp] Payment batch created: {BatchId} for org {OrgId}, {ItemCount} items, total {Total}", batchId, orgId, itemCount, totalAmount);

  return Results.Ok(new { id = batchId, organisationId = orgId, submittedByUserId = userId, status = "Draft", currency, totalAmount, itemCount, createdAt = DateTime.UtcNow, submittedAt = (DateTime?)null, executedAt = (DateTime?)null });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to create payment batch");
  return Results.Problem("Failed to create payment batch", statusCode: 500);
 }
 }).RequireAuthorization();

 // POST /api/paymentbatches/{batchId}/submit
 app.MapPost("/api/paymentbatches/{batchId}/submit", async (HttpContext http, Guid batchId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "Treasurer", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Problem("No DB configured", statusCode: 500);

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  var sql = @"UPDATE corp_payment_batches SET ""Status""='PendingApproval', ""SubmittedAt""=NOW() WHERE ""Id""=@bid AND ""OrganisationId""=@orgid AND ""Status""='Draft' RETURNING ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("bid", batchId);
  cmd.Parameters.AddWithValue("orgid", orgId);
  await using var reader = await cmd.ExecuteReaderAsync();
  if (!await reader.ReadAsync())
  return Results.BadRequest(new { message = "Batch not found or not in Draft status." });

  return Results.Ok(new { id = reader.GetGuid(0), organisationId = reader.GetGuid(1), submittedByUserId = reader.GetGuid(2), status = reader.GetString(3), currency = reader.GetString(4), totalAmount = reader.GetDecimal(5), itemCount = reader.GetInt32(6), createdAt = reader.GetDateTime(7), submittedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8), executedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9) });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to submit batch {BatchId}", batchId);
  return Results.Problem("Failed to submit batch", statusCode: 500);
 }
 }).RequireAuthorization();

 // POST /api/paymentbatches/{batchId}/execute
 app.MapPost("/api/paymentbatches/{batchId}/execute", async (HttpContext http, Guid batchId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "Treasurer", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Problem("No DB configured", statusCode: 500);

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  var sql = @"UPDATE corp_payment_batches SET ""Status""='Executed', ""ExecutedAt""=NOW() WHERE ""Id""=@bid AND ""OrganisationId""=@orgid AND ""Status""='Approved' RETURNING ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("bid", batchId);
  cmd.Parameters.AddWithValue("orgid", orgId);
  await using var reader = await cmd.ExecuteReaderAsync();
  if (!await reader.ReadAsync())
  return Results.BadRequest(new { message = "Batch not found or not in Approved status." });

  return Results.Ok(new { id = reader.GetGuid(0), organisationId = reader.GetGuid(1), submittedByUserId = reader.GetGuid(2), status = reader.GetString(3), currency = reader.GetString(4), totalAmount = reader.GetDecimal(5), itemCount = reader.GetInt32(6), createdAt = reader.GetDateTime(7), submittedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8), executedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9) });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to execute batch {BatchId}", batchId);
  return Results.Problem("Failed to execute batch", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/approvals/pending
 app.MapGet("/api/approvals/pending", async (HttpContext http, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "Approver", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Ok(new List<object>());

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql = @"SELECT ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""
  FROM corp_payment_batches WHERE ""OrganisationId""=@orgid AND ""Status""='PendingApproval' ORDER BY ""SubmittedAt"" DESC";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("orgid", orgId);
  var results = new List<object>();
  await using var reader = await cmd.ExecuteReaderAsync();
  while (await reader.ReadAsync())
  {
  results.Add(new { id = reader.GetGuid(0), organisationId = reader.GetGuid(1), submittedByUserId = reader.GetGuid(2), status = reader.GetString(3), currency = reader.GetString(4), totalAmount = reader.GetDecimal(5), itemCount = reader.GetInt32(6), createdAt = reader.GetDateTime(7), submittedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8), executedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9) });
  }
  return Results.Ok(results);
 }
 catch (PostgresException pex) when (pex.SqlState == "42P01")
 {
  await EnsureCorporateTablesAsync(cfg);
  return Results.Ok(new List<object>());
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get pending approvals");
  return Results.Problem("Failed to load pending approvals", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/approvals/{batchId}
 app.MapGet("/api/approvals/{batchId}", async (HttpContext http, Guid batchId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.NotFound();

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  var batchSql = @"SELECT ""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"", ""ExecutedAt""
  FROM corp_payment_batches WHERE ""Id""=@bid AND ""OrganisationId""=@orgid";
  await using var bCmd = new NpgsqlCommand(batchSql, conn);
  bCmd.Parameters.AddWithValue("bid", batchId);
  bCmd.Parameters.AddWithValue("orgid", orgId);
  await using var br = await bCmd.ExecuteReaderAsync();
  if (!await br.ReadAsync()) return Results.NotFound();
  var batch = new { id = br.GetGuid(0), organisationId = br.GetGuid(1), submittedByUserId = br.GetGuid(2), status = br.GetString(3), currency = br.GetString(4), totalAmount = br.GetDecimal(5), itemCount = br.GetInt32(6), createdAt = br.GetDateTime(7), submittedAt = br.IsDBNull(8) ? (DateTime?)null : br.GetDateTime(8), executedAt = br.IsDBNull(9) ? (DateTime?)null : br.GetDateTime(9) };
  await br.CloseAsync();

  var itemsSql = @"SELECT ""SourceAccountId"", ""PayeeName"", ""PayeeAccountNumber"", ""Amount"", ""Description"" FROM corp_payment_batch_items WHERE ""PaymentBatchId""=@bid";
  await using var iCmd = new NpgsqlCommand(itemsSql, conn);
  iCmd.Parameters.AddWithValue("bid", batchId);
  var items = new List<object>();
  await using var ir = await iCmd.ExecuteReaderAsync();
  while (await ir.ReadAsync())
  {
  items.Add(new { sourceAccountId = ir.GetGuid(0), payeeName = ir.GetString(1), payeeAccountNumber = ir.IsDBNull(2) ? null : ir.GetString(2), amount = ir.GetDecimal(3), description = ir.IsDBNull(4) ? null : ir.GetString(4) });
  }
  await ir.CloseAsync();

  var appSql = @"SELECT ""Id"", ""ApprovedByUserId"", ""Decision"", ""Comments"", ""DecidedAt"" FROM corp_approval_records WHERE ""PaymentBatchId""=@bid";
  await using var aCmd = new NpgsqlCommand(appSql, conn);
  aCmd.Parameters.AddWithValue("bid", batchId);
  var approvals = new List<object>();
  await using var ardr = await aCmd.ExecuteReaderAsync();
  while (await ardr.ReadAsync())
  {
  approvals.Add(new { id = ardr.GetGuid(0), approvedByUserId = ardr.GetGuid(1), decision = ardr.GetString(2), comments = ardr.IsDBNull(3) ? null : ardr.GetString(3), decidedAt = ardr.GetDateTime(4) });
  }

  return Results.Ok(new { batch.id, batch.organisationId, batch.submittedByUserId, batch.status, batch.currency, batch.totalAmount, batch.itemCount, batch.createdAt, batch.submittedAt, batch.executedAt, items, approvals });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get approval batch detail {BatchId}", batchId);
  return Results.Problem("Failed to load batch detail", statusCode: 500);
 }
 }).RequireAuthorization();

 // POST /api/approvals/{batchId}/decide
 app.MapPost("/api/approvals/{batchId}/decide", async (HttpContext http, Guid batchId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var orgId))
  return Results.Forbid();

 var callerRole = http.User.FindFirst("organisation_role")?.Value;
 if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(callerRole, "Approver", StringComparison.OrdinalIgnoreCase))
  return Results.Forbid();

 var userIdClaim = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst("id")?.Value;
 if (!Guid.TryParse(userIdClaim, out var userId))
  return Results.Unauthorized();

 var body = await http.Request.ReadFromJsonAsync<ApprovalDecisionInlineRequest>();
 if (body is null || string.IsNullOrWhiteSpace(body.Decision))
  return Results.BadRequest(new { message = "Decision is required (Approved or Rejected)." });

 if (!TryGetConnectionString(cfg, out _))
  return Results.Problem("No DB configured", statusCode: 500);

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();

  // Check batch exists and is pending
  var chkSql = @"SELECT ""Status"" FROM corp_payment_batches WHERE ""Id""=@bid AND ""OrganisationId""=@orgid";
  await using (var chk = new NpgsqlCommand(chkSql, conn))
  {
  chk.Parameters.AddWithValue("bid", batchId);
  chk.Parameters.AddWithValue("orgid", orgId);
  var status = await chk.ExecuteScalarAsync() as string;
  if (status is null) return Results.NotFound();
  if (status != "PendingApproval") return Results.BadRequest(new { message = "Batch is not pending approval." });
  }

  // Check not already decided by this user
  var dupSql = @"SELECT COUNT(1) FROM corp_approval_records WHERE ""PaymentBatchId""=@bid AND ""ApprovedByUserId""=@uid";
  await using (var dup = new NpgsqlCommand(dupSql, conn))
  {
  dup.Parameters.AddWithValue("bid", batchId);
  dup.Parameters.AddWithValue("uid", userId);
  var cnt = (long)(await dup.ExecuteScalarAsync() ?? 0L);
  if (cnt > 0) return Results.BadRequest(new { message = "You have already submitted a decision for this batch." });
  }

  // Insert approval record
  var recordId = Guid.NewGuid();
  var insRec = @"INSERT INTO corp_approval_records (""Id"", ""PaymentBatchId"", ""ApprovedByUserId"", ""Decision"", ""Comments"", ""DecidedAt"")
  VALUES (@id, @bid, @uid, @decision, @comments, NOW())";
  await using (var ins = new NpgsqlCommand(insRec, conn))
  {
  ins.Parameters.AddWithValue("id", recordId);
  ins.Parameters.AddWithValue("bid", batchId);
  ins.Parameters.AddWithValue("uid", userId);
  ins.Parameters.AddWithValue("decision", body.Decision);
  ins.Parameters.AddWithValue("comments", (object?)body.Comments?.Trim() ?? DBNull.Value);
  await ins.ExecuteNonQueryAsync();
  }

  // Update batch status
  if (string.Equals(body.Decision, "Rejected", StringComparison.OrdinalIgnoreCase))
  {
  await using var upd = new NpgsqlCommand(@"UPDATE corp_payment_batches SET ""Status""='Rejected' WHERE ""Id""=@bid", conn);
  upd.Parameters.AddWithValue("bid", batchId);
  await upd.ExecuteNonQueryAsync();
  }
  else
  {
  // Check approval count vs policy
  var countSql = @"SELECT COUNT(1) FROM corp_approval_records WHERE ""PaymentBatchId""=@bid AND ""Decision""='Approved'";
  await using var cntCmd = new NpgsqlCommand(countSql, conn);
  cntCmd.Parameters.AddWithValue("bid", batchId);
  var approvalCount = (long)(await cntCmd.ExecuteScalarAsync() ?? 0L);

  var policySql = @"SELECT COALESCE(MIN(""RequiredApprovals""), 1) FROM corp_approval_policies WHERE ""OrganisationId""=@orgid";
  await using var polCmd = new NpgsqlCommand(policySql, conn);
  polCmd.Parameters.AddWithValue("orgid", orgId);
  var required = (int)(await polCmd.ExecuteScalarAsync() ?? 1);

  if (approvalCount >= required)
  {
   await using var upd = new NpgsqlCommand(@"UPDATE corp_payment_batches SET ""Status""='Approved' WHERE ""Id""=@bid", conn);
   upd.Parameters.AddWithValue("bid", batchId);
   await upd.ExecuteNonQueryAsync();
  }
  }

  return Results.Ok(new { id = recordId, approvedByUserId = userId, decision = body.Decision, comments = body.Comments?.Trim(), decidedAt = DateTime.UtcNow });
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to decide on batch {BatchId}", batchId);
  return Results.Problem("Failed to process decision", statusCode: 500);
 }
 }).RequireAuthorization();

 // GET /api/accounts/organisation/{orgId} (corporate accounts by organisation)
 app.MapGet("/api/accounts/organisation/{orgId}", async (HttpContext http, Guid orgId, IConfiguration cfg) =>
 {
 var orgIdClaim = http.User.FindFirst("organisation_id")?.Value;
 if (!Guid.TryParse(orgIdClaim, out var callerOrgId) || callerOrgId != orgId)
  return Results.Forbid();

 if (!TryGetConnectionString(cfg, out _))
  return Results.Ok(new List<object>());

 try
 {
  await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
  await conn.OpenAsync();
  var sql = @"SELECT ""Id"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"" FROM ""LedgerAccounts"" WHERE ""OrganisationId""=@orgid AND ""ClientType""='Corporate'";
  await using var cmd = new NpgsqlCommand(sql, conn);
  cmd.Parameters.AddWithValue("orgid", orgId);
  var results = new List<object>();
  await using var reader = await cmd.ExecuteReaderAsync();
  while (await reader.ReadAsync())
  {
  results.Add(new { id = reader.GetGuid(0), accountNumber = MaskAccountNumber(reader.GetString(1)), accountType = reader.GetString(2), balance = reader.GetDecimal(3), currency = reader.GetString(4) });
  }
  return Results.Ok(results);
 }
 catch (Exception ex)
 {
  Log.Error(ex, "[Corp] Failed to get corporate accounts for org {OrgId}", orgId);
  return Results.Ok(new List<object>());
 }
 }).RequireAuthorization();

 // ── Virtual Card Endpoints ──────────────────────────────────────────
 app.MapGet("/api/cards", async (HttpContext http, ICardIssuingProvider cards) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();
  var result = await cards.ListCardsAsync(userId);
  return Results.Ok(result);
 }).RequireAuthorization().RequireRateLimiting("cards");

 app.MapPost("/api/cards", async (HttpContext http, ICardIssuingProvider cards) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();
  using var doc = await System.Text.Json.JsonDocument.ParseAsync(http.Request.Body);
  var nickname = doc.RootElement.GetProperty("nickname").GetString();
  if (string.IsNullOrWhiteSpace(nickname))
   return Results.BadRequest(new { error = "Nickname is required." });
  var result = await cards.CreateCardAsync(userId, nickname);
  return Results.Created($"/api/cards/{result.Card.Id}", result);
 }).RequireAuthorization().RequireRateLimiting("cards");

 app.MapPatch("/api/cards/{id:guid}/freeze", async (Guid id, HttpContext http, ICardIssuingProvider cards) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();
  try { return Results.Ok(await cards.FreezeCardAsync(userId, id)); }
  catch (KeyNotFoundException) { return Results.NotFound(); }
 }).RequireAuthorization().RequireRateLimiting("cards");

 app.MapPatch("/api/cards/{id:guid}/unfreeze", async (Guid id, HttpContext http, ICardIssuingProvider cards) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();
  try { return Results.Ok(await cards.UnfreezeCardAsync(userId, id)); }
  catch (KeyNotFoundException) { return Results.NotFound(); }
 }).RequireAuthorization().RequireRateLimiting("cards");

 app.MapDelete("/api/cards/{id:guid}", async (Guid id, HttpContext http, ICardIssuingProvider cards) =>
 {
  var userId = GetUserIdFromToken(http.User);
  if (userId == Guid.Empty) return Results.Unauthorized();
  try { await cards.DeleteCardAsync(userId, id); return Results.NoContent(); }
  catch (KeyNotFoundException) { return Results.NotFound(); }
 }).RequireAuthorization().RequireRateLimiting("cards");

 app.MapHealthChecks("/health");
 app.MapControllers();

 // Root explicitly serves index.html
 app.MapGet("/", async context =>
 {
 await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
 });

 // Only send API prefixes to Ocelot; exclude SPA and local endpoints
 if (enableOcelot)
 {
 app.MapWhen(ctx =>
 {
 var path = ctx.Request.Path.Value ?? string.Empty;
 var isLocalHandled = path.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/accounts", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/transactions", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/budget", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/payees", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/payments", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/bankconnections", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/v1/feedback", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/v1/ftk/", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/kyc", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/sar", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/organisations", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/paymentbatches", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/approvals", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/api/accounts/organisation", StringComparison.OrdinalIgnoreCase)
   || path.StartsWith("/api/cards", StringComparison.OrdinalIgnoreCase)
   || path.Equals("/", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/_debug", StringComparison.OrdinalIgnoreCase)
  || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

 // Route only /api/* to Ocelot
 var isOcelotApiPrefix = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

 return !isLocalHandled && isOcelotApiPrefix;
 },
 subApp =>
 {
 subApp.UseOcelot().GetAwaiter().GetResult();
 });
 }

 // SPA fallback AFTER static files but outside the Ocelot branch
 app.MapFallbackToFile("index.html");

 Log.Information("ApiGateway starting up (EnableOcelot={EnableOcelot})", enableOcelot);
 app.Run();
}
catch (Exception ex)
{
 Log.Fatal(ex, "ApiGateway terminated unexpectedly");
}
finally
{
 Log.CloseAndFlush();
}

partial class Program
{
 static Guid GetUserIdFromToken(ClaimsPrincipal user)
 {
 var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
 return Guid.TryParse(id, out var g) ? g : Guid.Empty;
 }

 // RESTORED: DB user lookup for demo auth
 static async Task<(Guid Id, string Email, string PasswordHash, string? ClientType, Guid? OrganisationId, string? OrganisationRole)?> FindUserByEmailAsync(IConfiguration cfg, string email)
 {
 if (!TryGetConnectionString(cfg, out _)) return null;
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"SELECT ""Id"", ""Email"", ""PasswordHash"", ""ClientType"", ""OrganisationId"", ""OrganisationRole"" FROM users_usvc WHERE ""Email""=@e LIMIT 1";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("e", email);
 await using var reader = await cmd.ExecuteReaderAsync();
 if (await reader.ReadAsync())
  return (
   reader.GetGuid(0),
   reader.GetString(1),
   reader.GetString(2),
   reader.IsDBNull(3) ? null : reader.GetString(3),
   reader.IsDBNull(4) ? null : reader.GetGuid(4),
   reader.IsDBNull(5) ? null : reader.GetString(5)
  );
 return null;
 }

 // RESTORED: DB user insert for registration
 static async Task InsertUserAsync(IConfiguration cfg, Guid id, string email, string passwordHash, string firstName, string lastName)
 {
 if (!TryGetConnectionString(cfg, out _))
 throw new InvalidOperationException("DB not configured");
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var fn = string.IsNullOrWhiteSpace(firstName) ? "User" : firstName;
 var ln = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName;
 var sql = @"INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""CreatedAt"", ""UpdatedAt"")
 VALUES (@id, @e, @ph, @fn, @ln, true, NOW(), NOW())";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("id", id);
 cmd.Parameters.AddWithValue("e", email);
 cmd.Parameters.AddWithValue("ph", passwordHash);
 cmd.Parameters.AddWithValue("fn", fn);
 cmd.Parameters.AddWithValue("ln", ln);
 await cmd.ExecuteNonQueryAsync();
 }

 // RESTORED: Account number generator -> now12 digits
 static string GenerateAccountNumber()
 {
 var rng = RandomNumberGenerator.Create();
 var bytes = new byte[8];
 rng.GetBytes(bytes);
 var n = BitConverter.ToUInt64(bytes,0) %900000000000UL +100000000000UL; //12-digit
 return n.ToString();
 }

 static string MaskAccountNumber(string accountNumber)
 {
 if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
 return "****";
 return $"****{accountNumber.Substring(accountNumber.Length - 4)}";
 }
 
 static bool TryGetConnectionString(IConfiguration cfg, out string connectionString)
 {
 //1. ConnectionStrings:DefaultConnection
 var defaultConn = cfg.GetConnectionString("DefaultConnection") ?? cfg["ConnectionStrings:DefaultConnection"];
 if (!string.IsNullOrWhiteSpace(defaultConn))
 {
 try
 {
 var csb0 = new NpgsqlConnectionStringBuilder(defaultConn)
 {
 SslMode = SslMode.Require
 };
 connectionString = csb0.ToString();
 Log.Information("[DB] Connection via ConnectionStrings:DefaultConnection -> Host={Host} Db={Db} Port={Port}", csb0.Host, csb0.Database, csb0.Port);
 return true;
 }
 catch (Exception ex)
 {
 Log.Warning(ex, "[DB] Failed to parse ConnectionStrings:DefaultConnection");
 }
 }

 //2. DATABASE_URL (Heroku)
 var url = cfg["DATABASE_URL"];
 if (!string.IsNullOrWhiteSpace(url))
 {
 connectionString = BuildPgConnectionFromUrl(url);
 try
 {
 var csb = new NpgsqlConnectionStringBuilder(connectionString);
 Log.Information("[DB] Connection via DATABASE_URL -> Host={Host} Db={Db} Port={Port}", csb.Host, csb.Database, csb.Port);
 }
 catch { }
 return true;
 }

 //2b. SHARED_DB_URL (Heroku shared database add-on)
 var shared = cfg["SHARED_DB_URL"];
 if (!string.IsNullOrWhiteSpace(shared))
 {
 connectionString = BuildPgConnectionFromUrl(shared);
 try
 {
 var csb = new NpgsqlConnectionStringBuilder(connectionString);
 Log.Information("[DB] Connection via SHARED_DB_URL -> Host={Host} Db={Db} Port={Port}", csb.Host, csb.Database, csb.Port);
 }
 catch { }
 return true;
 }

 //3. PG* explicit parts
 var host = cfg["PGHOST"];
 if (!string.IsNullOrWhiteSpace(host))
 {
 var csb = new NpgsqlConnectionStringBuilder
 {
 Host = host,
 Port = int.TryParse(cfg["PGPORT"], out var p) ? p :5432,
 Username = cfg["PGUSER"],
 Password = cfg["PGPASSWORD"],
 Database = cfg["PGDATABASE"] ?? "postgres",
 SslMode = SslMode.Require
 };
 connectionString = csb.ToString();
 Log.Information("[DB] Connection via PG* env vars -> Host={Host} Db={Db} Port={Port}", csb.Host, csb.Database, csb.Port);
 return true;
 }

 connectionString = string.Empty;
 return false;
 }

 static string GetConnectionString(IConfiguration cfg)
 {
 if (TryGetConnectionString(cfg, out var cs)) return cs;
 throw new InvalidOperationException("No database connection string found for gateway");
 }

 static string BuildPgConnectionFromUrl(string databaseUrl)
 {
 var uri = new Uri(databaseUrl);
 var userInfo = uri.UserInfo ?? string.Empty;
 var colonIdx = userInfo.IndexOf(':');
 var username = colonIdx >=0 ? userInfo.Substring(0, colonIdx) : userInfo;
 var password = colonIdx >=0 ? userInfo.Substring(colonIdx +1) : string.Empty;
 username = Uri.UnescapeDataString(username);
 password = Uri.UnescapeDataString(password);

 var csb = new NpgsqlConnectionStringBuilder
 {
 Host = uri.Host,
 Port = uri.IsDefaultPort ?5432 : uri.Port,
 Username = username,
 Password = password,
 Database = uri.LocalPath.Trim('/'),
 SslMode = SslMode.Require
 };

 // Honor sslmode if present in query string (e.g., ?sslmode=require)
 var query = uri.Query;
 if (!string.IsNullOrEmpty(query))
 {
 var trimmed = query[0] == '?' ? query.Substring(1) : query;
 foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
 {
 var kv = pair.Split('=',2);
 var key = Uri.UnescapeDataString(kv[0]);
 var val = kv.Length >1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
 if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase)
 && Enum.TryParse<SslMode>(val, true, out var parsed))
 {
 csb.SslMode = parsed;
 }
 }
 }

 return csb.ToString();
 }

 static async Task EnsureDefaultAccountForUserAsync(IConfiguration cfg, Guid userId)
 {
 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("DB not configured. Skip default account for {UserId}", userId);
 return;
 }

 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var existsSql = "SELECT 1 FROM \"LedgerAccounts\" WHERE \"UserId\"=@uid LIMIT 1";
 await using var ecmd = new NpgsqlCommand(existsSql, conn);
 ecmd.Parameters.AddWithValue("uid", userId);
 var exists = await ecmd.ExecuteScalarAsync();
 if (exists != null)
 {
 Log.Information("Default account already exists for {UserId}", userId);
 return;
 }

 var acctNumber = GenerateAccountNumber();
 var insert = @"INSERT INTO ""LedgerAccounts"" (""Id"", ""UserId"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
 VALUES (@id, @uid, @num, 'Checking',0, 'NZD', NOW(), NOW())";
 await using var icmd = new NpgsqlCommand(insert, conn);
 icmd.Parameters.AddWithValue("id", Guid.NewGuid());
 icmd.Parameters.AddWithValue("uid", userId);
 icmd.Parameters.AddWithValue("num", acctNumber);
 await icmd.ExecuteNonQueryAsync();
 Log.Information("Provisioned default LedgerAccount for {UserId}", userId);
 }

 static async Task<List<object>> GetAccountsAsync(IConfiguration cfg, Guid userId)
 {
 if (!TryGetConnectionString(cfg, out _)) return new List<object>();
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"AccountNumber\", \"AccountType\", \"Balance\", \"Currency\" FROM \"LedgerAccounts\" WHERE \"UserId\"=@uid";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 await using var reader = await cmd.ExecuteReaderAsync();
 var list = new List<object>();
 while (await reader.ReadAsync())
 {
 list.Add(new { id = reader.GetGuid(0), accountNumber = MaskAccountNumber(reader.GetString(1)), accountType = reader.GetString(2), balance = reader.GetDecimal(3), currency = reader.GetString(4) });
 }
 return list;
 }

 static async Task<List<object>> GetTransactionsAsync(IConfiguration cfg, Guid userId)
 {
 if (!TryGetConnectionString(cfg, out _)) return new List<object>();
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"AccountId\", \"Amount\", \"Currency\", \"Type\", \"Description\", \"CreatedAt\", \"SpendingType\" FROM \"LedgerTransactions\" WHERE \"UserId\"=@uid ORDER BY \"CreatedAt\" DESC";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 await using var reader = await cmd.ExecuteReaderAsync();
 var list = new List<object>();
 while (await reader.ReadAsync())
 {
  var spendingType = reader.IsDBNull(7) ? null : reader.GetString(7);
  list.Add(new { id = reader.GetGuid(0), accountId = reader.GetGuid(1), amount = reader.GetDecimal(2), currency = reader.GetString(3), type = reader.GetString(4), description = reader.GetString(5), createdAt = reader.GetDateTime(6), spendingType });
 }
 return list;
 }

 static async Task EnsureBankConnectionTablesAsync(IConfiguration cfg)
 {
 if (!TryGetConnectionString(cfg, out _)) return;

 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();

 var createBankConnections = @"
  CREATE TABLE IF NOT EXISTS ""BankConnections"" (
   ""Id"" UUID PRIMARY KEY,
   ""UserId"" UUID NOT NULL,
   ""BankId"" VARCHAR(50) NOT NULL,
   ""BankName"" VARCHAR(100) NOT NULL,
   ""BankLogo"" VARCHAR(500) NOT NULL,
   ""Status"" VARCHAR(20) NOT NULL DEFAULT 'Active',
   ""AccessToken"" VARCHAR(500),
   ""TokenExpiresAt"" TIMESTAMP,
   ""ConnectedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
   ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
   UNIQUE(""UserId"", ""BankId"")
  )";
 await using (var cmd = new NpgsqlCommand(createBankConnections, conn))
 {
  await cmd.ExecuteNonQueryAsync();
 }

 var createExternalAccounts = @"
  CREATE TABLE IF NOT EXISTS ""ExternalBankAccounts"" (
   ""Id"" UUID PRIMARY KEY,
   ""BankConnectionId"" UUID NOT NULL REFERENCES ""BankConnections""(""Id"") ON DELETE CASCADE,
   ""UserId"" UUID NOT NULL,
   ""ExternalAccountId"" VARCHAR(100) NOT NULL UNIQUE,
   ""AccountName"" VARCHAR(100) NOT NULL,
   ""AccountType"" VARCHAR(50) NOT NULL,
   ""AccountNumber"" VARCHAR(50) NOT NULL,
   ""Balance"" NUMERIC(18,2) NOT NULL DEFAULT 0,
   ""Currency"" VARCHAR(3) NOT NULL DEFAULT 'NZD',
   ""LastSyncedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
  )";
 await using (var cmd = new NpgsqlCommand(createExternalAccounts, conn))
 {
  await cmd.ExecuteNonQueryAsync();
 }

 Log.Information("[DB] Bank connection tables ensured");
 }

 static List<InMemExternalBankAccount> GenerateMockBankAccounts(Guid connectionId, Guid userId, string bankName)
 {
 var random = new Random();
 var accounts = new List<InMemExternalBankAccount>();
 var accountTypes = new[] { "Checking", "Savings", "Credit Card" };
 var accountCount = random.Next(1, 4);

 for (int i = 0; i < accountCount; i++)
 {
  var type = accountTypes[i % accountTypes.Length];
  accounts.Add(new InMemExternalBankAccount
  {
  Id = Guid.NewGuid(),
  BankConnectionId = connectionId,
  UserId = userId,
  AccountName = $"{bankName} {type}",
  AccountType = type,
  AccountNumber = $"****{random.Next(1000, 9999)}",
  Balance = Math.Round((decimal)(random.NextDouble() * 10000 + 500), 2),
  Currency = "NZD",
  LastSyncedAt = DateTime.UtcNow
  });
 }
 return accounts;
 }

 // Budget aggregation helper - calculates Fun/Fixed/Future spending categories
 internal record BudgetAggregationResult(decimal Fun, decimal Fixed, decimal Future, decimal Total, string PeriodFrom, string PeriodTo);

 static async Task<BudgetAggregationResult> GetBudgetAsync(IConfiguration cfg, Guid accountId, DateTime from, DateTime to)
 {
 decimal fun = 0m, fixedTotal = 0m, future = 0m;

 if (!TryGetConnectionString(cfg, out _))
 {
  // In-memory fallback - aggregate across all users (simplified for demo)
  foreach (var kvp in InMemoryData.TransactionsByUser)
  {
  foreach (var tx in kvp.Value.Where(t => t.AccountId == accountId && t.CreatedAt >= from && t.CreatedAt <= to))
  {
   if (string.IsNullOrWhiteSpace(tx.SpendingType)) continue;
   var normalized = tx.SpendingType.Trim().ToUpperInvariant();
   switch (normalized)
   {
   case "FUN": fun += tx.Amount; break;
   case "FIXED": fixedTotal += tx.Amount; break;
   case "FUTURE": future += tx.Amount; break;
   }
  }
  }
  return new BudgetAggregationResult(fun, fixedTotal, future, fun + fixedTotal + future, from.ToString("O"), to.ToString("O"));
 }

 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"SELECT ""Amount"", ""SpendingType"" FROM ""LedgerTransactions"" 
    WHERE ""AccountId""=@aid AND ""CreatedAt"" >= @from AND ""CreatedAt"" <= @to AND ""SpendingType"" IS NOT NULL";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("aid", accountId);
 cmd.Parameters.AddWithValue("from", from);
 cmd.Parameters.AddWithValue("to", to);
 await using var reader = await cmd.ExecuteReaderAsync();
 while (await reader.ReadAsync())
 {
  var amount = reader.GetDecimal(0);
  var spendingType = reader.IsDBNull(1) ? null : reader.GetString(1);
  if (string.IsNullOrWhiteSpace(spendingType)) continue;
  var normalized = spendingType.Trim().ToUpperInvariant();
  switch (normalized)
  {
  case "FUN": fun += amount; break;
  case "FIXED": fixedTotal += amount; break;
  case "FUTURE": future += amount; break;
  }
 }
 return new BudgetAggregationResult(fun, fixedTotal, future, fun + fixedTotal + future, from.ToString("O"), to.ToString("O"));
 }


 static async Task<TransactionResponse> CreateTransactionAsync(IConfiguration cfg, Guid userId, Guid accountId, decimal amount, string currency, string type, string description)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 await using var tx = await conn.BeginTransactionAsync();

 decimal currentBalance;
 string existingCurrency;

 var checkSql = "SELECT \"Balance\", \"Currency\" FROM \"LedgerAccounts\" WHERE \"Id\"=@aid AND \"UserId\"=@uid FOR UPDATE";
 await using (var checkCmd = new NpgsqlCommand(checkSql, conn, (NpgsqlTransaction)tx))
 {
 checkCmd.Parameters.AddWithValue("aid", accountId);
 checkCmd.Parameters.AddWithValue("uid", userId);
 await using var r = await checkCmd.ExecuteReaderAsync();
 if (!await r.ReadAsync()) throw new KeyNotFoundException();
 currentBalance = r.GetDecimal(0);
 existingCurrency = r.GetString(1);
 currency = string.IsNullOrWhiteSpace(currency) ? existingCurrency : currency;
 }

 if (type == "debit" && currentBalance < amount)
 throw new InvalidOperationException("Insufficient funds");

 var delta = type == "credit" ? amount : -amount;
 var updSql = "UPDATE \"LedgerAccounts\" SET \"Balance\" = \"Balance\" + @delta, \"UpdatedAt\" = NOW() WHERE \"Id\"=@aid AND \"UserId\"=@uid";
 await using (var upd = new NpgsqlCommand(updSql, conn, (NpgsqlTransaction)tx))
 {
 upd.Parameters.AddWithValue("delta", delta);
 upd.Parameters.AddWithValue("aid", accountId);
 upd.Parameters.AddWithValue("uid", userId);
 var rows = await upd.ExecuteNonQueryAsync();
 if (rows ==0) throw new UnauthorizedAccessException();
 }

 var tid = Guid.NewGuid();
 var insSql = @"INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""CreatedAt"")
 VALUES (@id, @aid, @uid, @amt, @cur, @typ, @desc, NOW())";
 await using (var ins = new NpgsqlCommand(insSql, conn, (NpgsqlTransaction)tx))
 {
 ins.Parameters.AddWithValue("id", tid);
 ins.Parameters.AddWithValue("aid", accountId);
 ins.Parameters.AddWithValue("uid", userId);
 ins.Parameters.AddWithValue("amt", amount);
 ins.Parameters.AddWithValue("cur", currency);
 ins.Parameters.AddWithValue("typ", type);
 ins.Parameters.AddWithValue("desc", description ?? string.Empty);
 await ins.ExecuteNonQueryAsync();
 }

 // Write audit log atomically within the same transaction
 await WriteAuditLogAsync(cfg, userId, "TransactionCreated", "Transaction", tid,
  $"{type} {amount} {currency} on account {accountId}", null, conn, (NpgsqlTransaction)tx);

 await tx.CommitAsync();
 return new TransactionResponse(tid, accountId, amount, currency, type, description ?? string.Empty, DateTime.UtcNow);
 }

 // Append-only audit log writer â€” writes atomically within an existing DB transaction when provided,
 // or opens its own connection for standalone audit events (login, register).
 static async Task WriteAuditLogAsync(
  IConfiguration cfg,
  Guid userId,
  string action,
  string entityType,
  Guid? entityId,
  string? detail,
  string? ipAddress,
  NpgsqlConnection? existingConn = null,
  NpgsqlTransaction? existingTx = null)
 {
  var sql = @"INSERT INTO ""AuditLog"" (""Id"", ""Timestamp"", ""UserId"", ""Action"", ""EntityType"", ""EntityId"", ""Detail"", ""IpAddress"")
  VALUES (@id, @ts, @uid, @action, @etype, @eid, @detail, @ip)";

  if (existingConn != null)
  {
   await using var cmd = new NpgsqlCommand(sql, existingConn, existingTx as NpgsqlTransaction);
   cmd.Parameters.AddWithValue("id", Guid.NewGuid());
   cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
   cmd.Parameters.AddWithValue("uid", userId);
   cmd.Parameters.AddWithValue("action", action);
   cmd.Parameters.AddWithValue("etype", entityType);
   cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
   cmd.Parameters.AddWithValue("detail", (object?)detail ?? DBNull.Value);
   cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
   await cmd.ExecuteNonQueryAsync();
   return;
  }

  if (!TryGetConnectionString(cfg, out _))
  {
   Log.Warning("[Audit] No DB configured, audit entry skipped: {Action} {EntityType} for {UserId}", action, entityType, userId);
   return;
  }

  try
  {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   await using var cmd = new NpgsqlCommand(sql, conn);
   cmd.Parameters.AddWithValue("id", Guid.NewGuid());
   cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
   cmd.Parameters.AddWithValue("uid", userId);
   cmd.Parameters.AddWithValue("action", action);
   cmd.Parameters.AddWithValue("etype", entityType);
   cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
   cmd.Parameters.AddWithValue("detail", (object?)detail ?? DBNull.Value);
   cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
   await cmd.ExecuteNonQueryAsync();
  }
  catch (PostgresException pex) when (pex.SqlState == "42P01")
  {
   await EnsureLedgerTablesAsync(cfg);
   try
   {
    await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", Guid.NewGuid());
    cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
    cmd.Parameters.AddWithValue("uid", userId);
    cmd.Parameters.AddWithValue("action", action);
    cmd.Parameters.AddWithValue("etype", entityType);
    cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("detail", (object?)detail ?? DBNull.Value);
    cmd.Parameters.AddWithValue("ip", (object?)ipAddress ?? DBNull.Value);
    await cmd.ExecuteNonQueryAsync();
   }
   catch (Exception ex)
   {
    Log.Error(ex, "[Audit] Failed to write audit log after table creation for {Action}", action);
   }
  }
  catch (Exception ex)
  {
   Log.Error(ex, "[Audit] Failed to write audit log for {Action}", action);
  }
 }

 static string GetClientIpAddress(HttpContext http)
 {
  return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
 }

 // Strong PBKDF2 password hashing with legacy fallback
 static string HashPassword(string password)
 {
 const int iterations =310_000; // .NET guidance
 const int saltSize =16;
 const int keySize =32;
 var salt = RandomNumberGenerator.GetBytes(saltSize);
 var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keySize);
 return $"v1${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
 }

 static bool VerifyPassword(string password, string hash)
 {
 // PBKDF2 v1 format
 var parts = hash.Split('$');
 if (parts.Length ==4 && parts[0] == "v1")
 {
 if (!int.TryParse(parts[1], out var iterations)) return false;
 var salt = Convert.FromBase64String(parts[2]);
 var expected = Convert.FromBase64String(parts[3]);
 var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
 return CryptographicOperations.FixedTimeEquals(actual, expected);
 }

 // Legacy static SHA256 + "salt" base64 (backward compatibility)
 using var sha256 = SHA256.Create();
 var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "salt"));
 var legacy = Convert.ToBase64String(bytes);
 return legacy == hash;
 }

 static string GenerateJwt(IConfiguration configuration, Guid userId, string email)
 {
 return GenerateJwt(configuration, userId, email, null, null, null);
 }

 static string GenerateJwt(IConfiguration configuration, Guid userId, string email, string? clientType, Guid? organisationId, string? organisationRole)
 {
 var signingKey = configuration["JWT_SIGNING_KEY"] ?? "demo-signing-key-change-me-0123456789-XYZ987654321";
 var issuer = configuration["JWT_ISSUER"] ?? configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
 var audience = configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
 var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
 var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

 var claims = new List<Claim>
 {
 new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
 new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
 new Claim(JwtRegisteredClaimNames.Email, email ?? "demo"),
 new Claim(ClaimTypes.Name, email ?? "demo"),
 new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
 new Claim("token_use", "access")
 };

 if (!string.IsNullOrEmpty(clientType))
  claims.Add(new Claim("client_type", clientType));
 if (organisationId.HasValue)
  claims.Add(new Claim("organisation_id", organisationId.Value.ToString()));
 if (!string.IsNullOrEmpty(organisationRole))
  claims.Add(new Claim("organisation_role", organisationRole));

 var token = new JwtSecurityToken(
 issuer: issuer,
 audience: audience,
 claims: claims,
 notBefore: DateTime.UtcNow,
 expires: DateTime.UtcNow.AddMinutes(15),
 signingCredentials: creds
 );

 return new JwtSecurityTokenHandler().WriteToken(token);
 }

 static string GenerateRefreshJwt(IConfiguration configuration, Guid userId, string email)
 {
 var signingKey = configuration["JWT_SIGNING_KEY"] ?? "demo-signing-key-change-me-0123456789-XYZ987654321";
 var issuer = configuration["JWT_ISSUER"] ?? configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
 var audience = configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
 var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
 var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

 var claims = new List<Claim>
 {
 new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
 new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
 new Claim(JwtRegisteredClaimNames.Email, email ?? "demo"),
 new Claim(ClaimTypes.Name, email ?? "demo"),
 new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
 new Claim("token_use", "refresh")
 };

 var token = new JwtSecurityToken(
 issuer: issuer,
 audience: audience,
 claims: claims,
 notBefore: DateTime.UtcNow,
 expires: DateTime.UtcNow.AddDays(7),
 signingCredentials: creds
 );

 return new JwtSecurityTokenHandler().WriteToken(token);
 }

 static void AppendRefreshCookie(HttpContext http, string refresh)
 {
 var secure = http.Request.IsHttps || string.Equals(http.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);
 var opts = new CookieOptions
 {
 HttpOnly = true,
 Secure = secure,
 SameSite = SameSiteMode.Lax,
 Path = "/",
 Expires = DateTimeOffset.UtcNow.AddDays(7)
 };
 http.Response.Cookies.Append("rt", refresh, opts);
 }


 internal record InMemUser(Guid Id, string Email, string PasswordHash, string FirstName, string LastName);
 internal record TimezoneUpdatePayload(string? TimeZoneId, int? UtcOffsetMinutes);
 internal static class InMemoryUsersStore
 {
 public static readonly ConcurrentDictionary<string, InMemUser> Users = new(StringComparer.OrdinalIgnoreCase);
 }


 internal static class IdempotencyStore
 {
  private static readonly ConcurrentDictionary<string, (IResult Response, DateTimeOffset ExpiresAt)> _processed = new(StringComparer.OrdinalIgnoreCase);

  public static bool TryGet(string key, out IResult cachedResponse)
  {
   if (_processed.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
   {
    cachedResponse = entry.Response;
    return true;
   }
   cachedResponse = default!;
   return false;
  }

  public static bool TryReserve(string key)
  {
   CleanupExpired();
   return _processed.TryAdd(key, (Results.Conflict(new { message = "Request is being processed" }), DateTimeOffset.UtcNow.AddHours(24)));
  }

  public static void Complete(string key, IResult response)
  {
   _processed[key] = (response, DateTimeOffset.UtcNow.AddHours(24));
  }

  public static void Release(string key)
  {
   _processed.TryRemove(key, out _);
  }

  private static void CleanupExpired()
  {
   var now = DateTimeOffset.UtcNow;
   foreach (var kvp in _processed)
   {
    if (kvp.Value.ExpiresAt < now)
     _processed.TryRemove(kvp.Key, out _);
   }
  }
 }

 internal static class RevokedTokenStore
 {
 private static readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new();

 public static void Revoke(string jti, DateTimeOffset expiry)
 {
  _revoked[jti] = expiry;
  CleanupExpired();
 }

 public static bool IsRevoked(string jti) => _revoked.ContainsKey(jti);

 private static void CleanupExpired()
 {
  var now = DateTimeOffset.UtcNow;
  foreach (var kvp in _revoked)
  {
   if (kvp.Value < now)
    _revoked.TryRemove(kvp.Key, out _);
  }
 }
 }

 internal static class InMemoryData
 {
 public static readonly ConcurrentDictionary<Guid, List<InMemAccount>> AccountsByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemTransaction>> TransactionsByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemPayee>> PayeesByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemBankConnection>> BankConnectionsByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemExternalBankAccount>> ExternalBankAccountsByUser = new();
 }

 internal class InMemAccount
 {
 public Guid Id { get; set; }
 public Guid UserId { get; set; }
 public string AccountNumber { get; set; } = string.Empty;
 public string AccountType { get; set; } = "Checking";
 public decimal Balance { get; set; }
 public string Currency { get; set; } = "NZD";
 public DateTime CreatedAt { get; set; }
 public DateTime UpdatedAt { get; set; }
 }

 internal record InMemTransaction(Guid Id, Guid AccountId, Guid UserId, decimal Amount, string Currency, string Type, string Description, DateTime CreatedAt, string? SpendingType = null);
 internal record InMemPayee(Guid Id, Guid UserId, string Name, string AccountNumber, DateTime CreatedAt);

 internal class InMemBankConnection
 {
 public Guid Id { get; set; }
 public Guid UserId { get; set; }
 public string BankId { get; set; } = string.Empty;
 public string BankName { get; set; } = string.Empty;
 public string BankLogo { get; set; } = string.Empty;
 public string Status { get; set; } = "Active";
 public DateTime ConnectedAt { get; set; }
 }

 internal class InMemExternalBankAccount
 {
 public Guid Id { get; set; }
 public Guid BankConnectionId { get; set; }
 public Guid UserId { get; set; }
 public string AccountName { get; set; } = string.Empty;
 public string AccountType { get; set; } = string.Empty;
 public string AccountNumber { get; set; } = string.Empty;
 public decimal Balance { get; set; }
 public string Currency { get; set; } = "NZD";
 public DateTime LastSyncedAt { get; set; }
 }


 // Seed demo data helper
 static async Task SeedDemoDataAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();

 var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

 // Ensure demo user exists if table present
 var userTableExistsSql = "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema='public' AND table_name='users_usvc'";
 await using (var chk = new NpgsqlCommand(userTableExistsSql, conn))
 {
 var exists = (long)(await chk.ExecuteScalarAsync() ??0L);
 if (exists >0)
 {
 var upsertUser = @"INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @uid, 'demo', @ph, 'Demo', 'User', true, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM users_usvc WHERE ""Email""='demo')";
 await using var cmd = new NpgsqlCommand(upsertUser, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 cmd.Parameters.AddWithValue("ph", HashPassword("Demo@2026"));
 await cmd.ExecuteNonQueryAsync();
 }
 }

 // Ensure accounts
 var acc1 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
 var acc2 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
 var insAcc = @"INSERT INTO ""LedgerAccounts"" (""Id"", ""UserId"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @id, @uid, @num, @type, @bal, 'NZD', NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM ""LedgerAccounts"" WHERE ""Id""=@id)";
 await using (var a1 = new NpgsqlCommand(insAcc, conn))
 {
 a1.Parameters.AddWithValue("id", acc1);
 a1.Parameters.AddWithValue("uid", userId);
 a1.Parameters.AddWithValue("num", "123456789012");
 a1.Parameters.AddWithValue("type", "Checking");
 a1.Parameters.AddWithValue("bal",2500.50m);
 await a1.ExecuteNonQueryAsync();
 }
 await using (var a2 = new NpgsqlCommand(insAcc, conn))
 {
 a2.Parameters.AddWithValue("id", acc2);
 a2.Parameters.AddWithValue("uid", userId);
 a2.Parameters.AddWithValue("num", "098765432109");
 a2.Parameters.AddWithValue("type", "Savings");
 a2.Parameters.AddWithValue("bal",10000.00m);
 await a2.ExecuteNonQueryAsync();
 }

 // Ensure transactions with SpendingType for budget tracking
 var t1 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
 var t2 = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
 var t3 = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
 var t4 = Guid.Parse("11111111-1111-1111-1111-111111111111");
 var t5 = Guid.Parse("22222222-2222-2222-2222-222222222222");
 var t6 = Guid.Parse("33333333-3333-3333-3333-333333333333");
 var insTrx = @"INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""SpendingType"", ""CreatedAt"")
 SELECT @id, @aid, @uid, @amt, 'NZD', @typ, @desc, @stype, NOW() - @ago
 WHERE NOT EXISTS (SELECT 1 FROM ""LedgerTransactions"" WHERE ""Id""=@id)";
 // Salary deposit - Fixed income
 await using (var ti = new NpgsqlCommand(insTrx, conn))
 {
 ti.Parameters.AddWithValue("id", t1);
 ti.Parameters.AddWithValue("aid", acc1);
 ti.Parameters.AddWithValue("uid", userId);
 ti.Parameters.AddWithValue("amt",100.00m);
 ti.Parameters.AddWithValue("typ", "credit");
 ti.Parameters.AddWithValue("desc", "Salary deposit");
 ti.Parameters.AddWithValue("stype", "Fixed");
 ti.Parameters.AddWithValue("ago", TimeSpan.FromDays(1));
 await ti.ExecuteNonQueryAsync();
 }
 // Grocery shopping - Fixed expense
 await using (var tg = new NpgsqlCommand(insTrx, conn))
 {
 tg.Parameters.AddWithValue("id", t2);
 tg.Parameters.AddWithValue("aid", acc1);
 tg.Parameters.AddWithValue("uid", userId);
 tg.Parameters.AddWithValue("amt",50.00m);
 tg.Parameters.AddWithValue("typ", "debit");
 tg.Parameters.AddWithValue("desc", "Grocery shopping");
 tg.Parameters.AddWithValue("stype", "Fixed");
 tg.Parameters.AddWithValue("ago", TimeSpan.FromDays(2));
 await tg.ExecuteNonQueryAsync();
 }
 // Transfer to savings - Future
 await using (var tt = new NpgsqlCommand(insTrx, conn))
 {
 tt.Parameters.AddWithValue("id", t3);
 tt.Parameters.AddWithValue("aid", acc2);
 tt.Parameters.AddWithValue("uid", userId);
 tt.Parameters.AddWithValue("amt",500.00m);
 tt.Parameters.AddWithValue("typ", "credit");
 tt.Parameters.AddWithValue("desc", "Transfer from checking");
 tt.Parameters.AddWithValue("stype", "Future");
 tt.Parameters.AddWithValue("ago", TimeSpan.FromDays(3));
 await tt.ExecuteNonQueryAsync();
 }
 // Restaurant dinner - Fun
 await using (var t4cmd = new NpgsqlCommand(insTrx, conn))
 {
 t4cmd.Parameters.AddWithValue("id", t4);
 t4cmd.Parameters.AddWithValue("aid", acc1);
 t4cmd.Parameters.AddWithValue("uid", userId);
 t4cmd.Parameters.AddWithValue("amt", 75.00m);
 t4cmd.Parameters.AddWithValue("typ", "debit");
 t4cmd.Parameters.AddWithValue("desc", "Restaurant dinner");
 t4cmd.Parameters.AddWithValue("stype", "Fun");
 t4cmd.Parameters.AddWithValue("ago", TimeSpan.FromDays(4));
 await t4cmd.ExecuteNonQueryAsync();
 }
 // Movie tickets - Fun
 await using (var t5cmd = new NpgsqlCommand(insTrx, conn))
 {
 t5cmd.Parameters.AddWithValue("id", t5);
 t5cmd.Parameters.AddWithValue("aid", acc1);
 t5cmd.Parameters.AddWithValue("uid", userId);
 t5cmd.Parameters.AddWithValue("amt", 25.00m);
 t5cmd.Parameters.AddWithValue("typ", "debit");
 t5cmd.Parameters.AddWithValue("desc", "Movie tickets");
 t5cmd.Parameters.AddWithValue("stype", "Fun");
 t5cmd.Parameters.AddWithValue("ago", TimeSpan.FromDays(5));
 await t5cmd.ExecuteNonQueryAsync();
 }
 // Investment contribution - Future
 await using (var t6cmd = new NpgsqlCommand(insTrx, conn))
 {
 t6cmd.Parameters.AddWithValue("id", t6);
 t6cmd.Parameters.AddWithValue("aid", acc2);
 t6cmd.Parameters.AddWithValue("uid", userId);
 t6cmd.Parameters.AddWithValue("amt", 200.00m);
 t6cmd.Parameters.AddWithValue("typ", "debit");
 t6cmd.Parameters.AddWithValue("desc", "Investment contribution");
 t6cmd.Parameters.AddWithValue("stype", "Future");
 t6cmd.Parameters.AddWithValue("ago", TimeSpan.FromDays(6));
 await t6cmd.ExecuteNonQueryAsync();
 }

 // --- Corporate demo user + accounts ---
 var corpUserId = Guid.Parse("10101010-1010-1010-1010-101010101010");
 var corpOrgId = Guid.Parse("20202020-2020-2020-2020-202020202020");

 // Seed corporate user
 await using (var chk2 = new NpgsqlCommand(userTableExistsSql, conn))
 {
 var exists = (long)(await chk2.ExecuteScalarAsync() ?? 0L);
 if (exists > 0)
 {
 var upsertCorpUser = @"INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""ClientType"", ""OrganisationId"", ""OrganisationRole"", ""CompanyName"", ""RegistrationNumber"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @uid, 'corpadmindemo', @ph, 'Corporate', 'Admin', true, 'Corporate', @orgid, 'Admin', 'Acme Corp Ltd', 'NZ9876543', NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM users_usvc WHERE ""Email""='corpadmindemo')";
 await using var cmd = new NpgsqlCommand(upsertCorpUser, conn);
 cmd.Parameters.AddWithValue("uid", corpUserId);
 cmd.Parameters.AddWithValue("ph", HashPassword("Corp@2026"));
 cmd.Parameters.AddWithValue("orgid", corpOrgId);
 await cmd.ExecuteNonQueryAsync();
 }
 }

 // Seed corporate accounts
 var corpAcc1 = Guid.Parse("30303030-3030-3030-3030-303030303030");
 var corpAcc2 = Guid.Parse("40404040-4040-4040-4040-404040404040");
 var insCorpAcc = @"INSERT INTO ""LedgerAccounts"" (""Id"", ""UserId"", ""AccountNumber"", ""AccountType"", ""Balance"", ""Currency"", ""ClientType"", ""OrganisationId"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @id, @uid, @num, @type, @bal, '$', 'Corporate', @orgid, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM ""LedgerAccounts"" WHERE ""Id""=@id)";
 await using (var ca1 = new NpgsqlCommand(insCorpAcc, conn))
 {
 ca1.Parameters.AddWithValue("id", corpAcc1);
 ca1.Parameters.AddWithValue("uid", corpUserId);
 ca1.Parameters.AddWithValue("num", "550012340001");
 ca1.Parameters.AddWithValue("type", "Checking");
 ca1.Parameters.AddWithValue("bal", 75000.00m);
 ca1.Parameters.AddWithValue("orgid", corpOrgId);
 await ca1.ExecuteNonQueryAsync();
 }
 await using (var ca2 = new NpgsqlCommand(insCorpAcc, conn))
 {
 ca2.Parameters.AddWithValue("id", corpAcc2);
 ca2.Parameters.AddWithValue("uid", corpUserId);
 ca2.Parameters.AddWithValue("num", "550012340002");
 ca2.Parameters.AddWithValue("type", "Savings");
 ca2.Parameters.AddWithValue("bal", 250000.00m);
 ca2.Parameters.AddWithValue("orgid", corpOrgId);
 await ca2.ExecuteNonQueryAsync();
 }

 // Seed corporate transactions
 var ct1 = Guid.Parse("50505050-5050-5050-5050-505050505050");
 var ct2 = Guid.Parse("60606060-6060-6060-6060-606060606060");
 await using (var cti1 = new NpgsqlCommand(insTrx, conn))
 {
 cti1.Parameters.AddWithValue("id", ct1);
 cti1.Parameters.AddWithValue("aid", corpAcc1);
 cti1.Parameters.AddWithValue("uid", corpUserId);
 cti1.Parameters.AddWithValue("amt", 25000.00m);
 cti1.Parameters.AddWithValue("typ", "credit");
 cti1.Parameters.AddWithValue("desc", "Client invoice payment");
 cti1.Parameters.AddWithValue("stype", "Fixed");
 cti1.Parameters.AddWithValue("ago", TimeSpan.FromDays(1));
 await cti1.ExecuteNonQueryAsync();
 }
 await using (var cti2 = new NpgsqlCommand(insTrx, conn))
 {
 cti2.Parameters.AddWithValue("id", ct2);
 cti2.Parameters.AddWithValue("aid", corpAcc1);
 cti2.Parameters.AddWithValue("uid", corpUserId);
 cti2.Parameters.AddWithValue("amt", 8500.00m);
 cti2.Parameters.AddWithValue("typ", "debit");
 cti2.Parameters.AddWithValue("desc", "Supplier payment - batch");
 cti2.Parameters.AddWithValue("stype", "Fixed");
 cti2.Parameters.AddWithValue("ago", TimeSpan.FromDays(2));
 await cti2.ExecuteNonQueryAsync();
 }

 // --- Seed corporate tables (organisation, member, policy, batch, items) ---
 var corpTableExists = "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema='public' AND table_name='corp_organisations'";
 await using (var chkCorp = new NpgsqlCommand(corpTableExists, conn))
 {
 var exists = (long)(await chkCorp.ExecuteScalarAsync() ?? 0L);
 if (exists > 0)
 {
 // Seed organisation
 var insOrg = @"INSERT INTO corp_organisations (""Id"", ""Name"", ""RegistrationNumber"", ""CreatedByUserId"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @id, 'Acme Corp Ltd', 'NZ9876543', @uid, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM corp_organisations WHERE ""Id""=@id)";
 await using (var orgCmd = new NpgsqlCommand(insOrg, conn))
 {
 orgCmd.Parameters.AddWithValue("id", corpOrgId);
 orgCmd.Parameters.AddWithValue("uid", corpUserId);
 await orgCmd.ExecuteNonQueryAsync();
 }

 // Seed organisation member
 var memberId = Guid.Parse("21212121-2121-2121-2121-212121212121");
 var insMember = @"INSERT INTO corp_organisation_members (""Id"", ""OrganisationId"", ""UserId"", ""Email"", ""Role"", ""Status"", ""InvitedAt"", ""AcceptedAt"")
 SELECT @id, @orgid, @uid, 'corpadmindemo', 'Admin', 'Active', NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM corp_organisation_members WHERE ""Id""=@id)";
 await using (var memCmd = new NpgsqlCommand(insMember, conn))
 {
 memCmd.Parameters.AddWithValue("id", memberId);
 memCmd.Parameters.AddWithValue("orgid", corpOrgId);
 memCmd.Parameters.AddWithValue("uid", corpUserId);
 await memCmd.ExecuteNonQueryAsync();
 }

 // Seed approval policy
 var policyId = Guid.Parse("22222220-2222-2222-2222-222222222222");
 var insPolicy = @"INSERT INTO corp_approval_policies (""Id"", ""OrganisationId"", ""RequiredApprovals"", ""MonetaryThreshold"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @id, @orgid, 1, NULL, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM corp_approval_policies WHERE ""Id""=@id)";
 await using (var polCmd = new NpgsqlCommand(insPolicy, conn))
 {
 polCmd.Parameters.AddWithValue("id", policyId);
 polCmd.Parameters.AddWithValue("orgid", corpOrgId);
 await polCmd.ExecuteNonQueryAsync();
 }

 // Seed sample payment batch (PendingApproval)
 var batchId = Guid.Parse("70707070-7070-7070-7070-707070707070");
 var insBatch = @"INSERT INTO corp_payment_batches (""Id"", ""OrganisationId"", ""SubmittedByUserId"", ""Status"", ""Currency"", ""TotalAmount"", ""ItemCount"", ""CreatedAt"", ""SubmittedAt"")
 SELECT @id, @orgid, @uid, 'PendingApproval', '$', 15500.00, 2, NOW() - INTERVAL '1 day', NOW() - INTERVAL '12 hours'
 WHERE NOT EXISTS (SELECT 1 FROM corp_payment_batches WHERE ""Id""=@id)";
 await using (var batchCmd = new NpgsqlCommand(insBatch, conn))
 {
 batchCmd.Parameters.AddWithValue("id", batchId);
 batchCmd.Parameters.AddWithValue("orgid", corpOrgId);
 batchCmd.Parameters.AddWithValue("uid", corpUserId);
 await batchCmd.ExecuteNonQueryAsync();
 }

 // Seed batch items
 var item1Id = Guid.Parse("71717171-7171-7171-7171-717171717171");
 var item2Id = Guid.Parse("72727272-7272-7272-7272-727272727272");
 var insItem = @"INSERT INTO corp_payment_batch_items (""Id"", ""PaymentBatchId"", ""SourceAccountId"", ""PayeeName"", ""PayeeAccountNumber"", ""Amount"", ""Description"")
 SELECT @id, @bid, @srcacc, @payee, @payeeacc, @amt, @desc
 WHERE NOT EXISTS (SELECT 1 FROM corp_payment_batch_items WHERE ""Id""=@id)";
 await using (var i1Cmd = new NpgsqlCommand(insItem, conn))
 {
 i1Cmd.Parameters.AddWithValue("id", item1Id);
 i1Cmd.Parameters.AddWithValue("bid", batchId);
 i1Cmd.Parameters.AddWithValue("srcacc", corpAcc1);
 i1Cmd.Parameters.AddWithValue("payee", "NZ Power Ltd");
 i1Cmd.Parameters.AddWithValue("payeeacc", "12-3456-7890123-00");
 i1Cmd.Parameters.AddWithValue("amt", 8500.00m);
 i1Cmd.Parameters.AddWithValue("desc", "Monthly electricity");
 await i1Cmd.ExecuteNonQueryAsync();
 }
 await using (var i2Cmd = new NpgsqlCommand(insItem, conn))
 {
 i2Cmd.Parameters.AddWithValue("id", item2Id);
 i2Cmd.Parameters.AddWithValue("bid", batchId);
 i2Cmd.Parameters.AddWithValue("srcacc", corpAcc1);
 i2Cmd.Parameters.AddWithValue("payee", "Office Supplies Co");
 i2Cmd.Parameters.AddWithValue("payeeacc", "03-1234-5678901-00");
 i2Cmd.Parameters.AddWithValue("amt", 7000.00m);
 i2Cmd.Parameters.AddWithValue("desc", "Q1 office supplies");
 await i2Cmd.ExecuteNonQueryAsync();
 }
 }
 }
 }

 static async Task<SeedStatus> GetSeedStatusAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 
 async Task<long> CountAsync(string sql)
 {
 await using var cmd = new NpgsqlCommand(sql, conn);
 var v = await cmd.ExecuteScalarAsync();
 return v is long l ? l : (v is int i ? i :0);
 }
 
 var accountsCount = await CountAsync("SELECT COUNT(1) FROM \"LedgerAccounts\"");
 var transactionsCount = await CountAsync("SELECT COUNT(1) FROM \"LedgerTransactions\"");
 var demoTxCount = await CountAsync("SELECT COUNT(1) FROM \"LedgerTransactions\" WHERE \"Id\" IN ('dddddddd-dddd-dddd-dddd-dddddddddddd','eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee','ffffffff-ffff-ffff-ffff-ffffffffffff')");
  var appliedMigrations = new List<string>();
  await using (var cmd = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", conn))
  {
   await using (var rdr = await cmd.ExecuteReaderAsync())
   {
    while (await rdr.ReadAsync())
     appliedMigrations.Add(rdr.GetString(0));
   }
  }
  return new SeedStatus(accountsCount, transactionsCount, demoTxCount >0, appliedMigrations);
  }

 // Destructive reset of ledger schema on demand (drop tables and clear EF history entries for this context's migrations)
 static async Task ResetLedgerSchemaAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 await using var tx = await conn.BeginTransactionAsync();
 try
 {
 // Drop ledger tables if they exist
 var dropSql = @"DROP TABLE IF EXISTS ""LedgerTransactions"" CASCADE;
DROP TABLE IF EXISTS ""LedgerPayees"" CASCADE;
DROP TABLE IF EXISTS ""LedgerAccounts"" CASCADE;";
 await using (var dropCmd = new NpgsqlCommand(dropSql, conn, (NpgsqlTransaction)tx))
 {
 await dropCmd.ExecuteNonQueryAsync();
 }

 // Clear EF migration history entries for this DbContext's migrations, if history table exists
 var histExistsSql = "SELECT COUNT(1) FROM information_schema.tables WHERE table_schema='public' AND table_name='__EFMigrationsHistory'";
 await using (var he = new NpgsqlCommand(histExistsSql, conn, (NpgsqlTransaction)tx))
 {
 var exists = (long)(await he.ExecuteScalarAsync() ??0L);
 if (exists >0)
 {
 var clearSql = @"DELETE FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" IN ('20241012_InitialLedger','20251101_SeedDemoLedgerData');";
 await using var clr = new NpgsqlCommand(clearSql, conn, (NpgsqlTransaction)tx);
 await clr.ExecuteNonQueryAsync();
 }
 }

 await tx.CommitAsync();
 }
 catch
 {
 await tx.RollbackAsync();
 throw;
 }
 }

 // Ensure ledger tables exist (defensive creation matching EF model)
 static async Task EnsureLedgerTablesAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"CREATE TABLE IF NOT EXISTS ""LedgerAccounts"" (
 ""Id"" uuid NOT NULL PRIMARY KEY,
 ""UserId"" uuid NOT NULL,
 ""AccountNumber"" character varying(20) NOT NULL,
 ""AccountType"" character varying(50) NOT NULL,
 ""Balance"" numeric(18,2) NOT NULL,
 ""Currency"" character varying(3) NOT NULL,
 ""CreatedAt"" timestamp with time zone NOT NULL,
 ""UpdatedAt"" timestamp with time zone NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LedgerAccounts_AccountNumber"" ON ""LedgerAccounts"" (""AccountNumber"");

  CREATE TABLE IF NOT EXISTS ""LedgerTransactions"" (
 ""Id"" uuid NOT NULL PRIMARY KEY,
 ""AccountId"" uuid NOT NULL,
 ""UserId"" uuid NOT NULL,
 ""Amount"" numeric(18,2) NOT NULL,
 ""Currency"" character varying(3) NOT NULL,
 ""Type"" character varying(10) NOT NULL,
 ""Description"" character varying(500) NOT NULL,
 ""SpendingType"" character varying(20),
 ""CreatedAt"" timestamp with time zone NOT NULL
 );
 CREATE INDEX IF NOT EXISTS ""IX_LedgerTransactions_AccountId"" ON ""LedgerTransactions"" (""AccountId"");
 CREATE INDEX IF NOT EXISTS ""IX_LedgerTransactions_UserId"" ON ""LedgerTransactions"" (""UserId"");
 -- Add SpendingType column if missing (for existing tables)
 DO $$ BEGIN
   IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='LedgerTransactions' AND column_name='SpendingType') THEN
     ALTER TABLE ""LedgerTransactions"" ADD COLUMN ""SpendingType"" character varying(20);
   END IF;
 END $$;
 -- Add corporate columns to LedgerAccounts if missing
 DO $$ BEGIN
  ALTER TABLE ""LedgerAccounts"" ADD COLUMN IF NOT EXISTS ""ClientType"" character varying(20) NOT NULL DEFAULT 'Individual';
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE ""LedgerAccounts"" ADD COLUMN IF NOT EXISTS ""OrganisationId"" uuid;
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;

 CREATE TABLE IF NOT EXISTS ""LedgerPayees"" (
 ""Id"" uuid NOT NULL PRIMARY KEY,
 ""UserId"" uuid NOT NULL,
 ""Name"" character varying(200) NOT NULL,
 ""AccountNumber"" character varying(50) NOT NULL,
 ""CreatedAt"" timestamp with time zone NOT NULL
 );
  CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LedgerPayees_UserId_AccountNumber"" ON ""LedgerPayees"" (""UserId"", ""AccountNumber"");

  CREATE TABLE IF NOT EXISTS ""AuditLog"" (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""Timestamp"" timestamp with time zone NOT NULL,
  ""UserId"" uuid NOT NULL,
  ""Action"" character varying(100) NOT NULL,
  ""EntityType"" character varying(100) NOT NULL,
  ""EntityId"" uuid,
  ""Detail"" character varying(1000),
  ""IpAddress"" character varying(45)
  );
  CREATE INDEX IF NOT EXISTS ""IX_AuditLog_UserId"" ON ""AuditLog"" (""UserId"");
  CREATE INDEX IF NOT EXISTS ""IX_AuditLog_Timestamp"" ON ""AuditLog"" (""Timestamp"");";
 await using var cmd = new NpgsqlCommand(sql, conn);
 await cmd.ExecuteNonQueryAsync();
 Log.Information("[DB] Ensured ledger tables exist");
 }

 // Ensure users_usvc table exists (defensive for shared DB) and seed demo user
 static async Task EnsureUsersTableAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"CREATE TABLE IF NOT EXISTS users_usvc (
 ""Id"" uuid NOT NULL PRIMARY KEY,
 ""Email"" character varying(254) NOT NULL,
 ""PasswordHash"" text NOT NULL,
 ""FirstName"" character varying(100) NOT NULL,
 ""LastName"" character varying(100) NOT NULL,
 ""IsEmailVerified"" boolean NOT NULL,
 ""KycStatus"" character varying(20) NOT NULL DEFAULT 'Pending',
 ""ClientType"" character varying(20) NOT NULL DEFAULT 'Individual',
 ""OrganisationId"" uuid,
 ""OrganisationRole"" character varying(20),
 ""CompanyName"" character varying(200),
 ""RegistrationNumber"" character varying(50),
 ""CreatedAt"" timestamp with time zone NOT NULL,
 ""UpdatedAt"" timestamp with time zone NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_users_usvc_Email"" ON users_usvc (""Email"");
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""KycStatus"" character varying(20) NOT NULL DEFAULT 'Pending';
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""ClientType"" character varying(20) NOT NULL DEFAULT 'Individual';
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""OrganisationId"" uuid;
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""OrganisationRole"" character varying(20);
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""CompanyName"" character varying(200);
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""RegistrationNumber"" character varying(50);
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""TimeZoneId"" character varying(100);
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;
 DO $$ BEGIN
  ALTER TABLE users_usvc ADD COLUMN IF NOT EXISTS ""UtcOffsetMinutes"" integer;
 EXCEPTION WHEN duplicate_column THEN NULL;
 END $$;";
 await using (var cmd = new NpgsqlCommand(sql, conn))
 {
 await cmd.ExecuteNonQueryAsync();
 }
 // ensure demo user exists
 var upsertDemo = @"INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @uid, 'demo', @ph, 'Demo', 'User', true, NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM users_usvc WHERE ""Email""='demo')";
 await using (var up = new NpgsqlCommand(upsertDemo, conn))
 {
 up.Parameters.AddWithValue("uid", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
 up.Parameters.AddWithValue("ph", HashPassword("Demo@2026"));
 await up.ExecuteNonQueryAsync();
 }
 // ensure corporate demo user exists
 var upsertCorp = @"INSERT INTO users_usvc (""Id"", ""Email"", ""PasswordHash"", ""FirstName"", ""LastName"", ""IsEmailVerified"", ""ClientType"", ""OrganisationId"", ""OrganisationRole"", ""CompanyName"", ""RegistrationNumber"", ""CreatedAt"", ""UpdatedAt"")
 SELECT @uid, 'corpadmindemo', @ph, 'Corporate', 'Admin', true, 'Corporate', @orgid, 'Admin', 'Acme Corp Ltd', 'NZ9876543', NOW(), NOW()
 WHERE NOT EXISTS (SELECT 1 FROM users_usvc WHERE ""Email""='corpadmindemo')";
 await using (var cp = new NpgsqlCommand(upsertCorp, conn))
 {
 cp.Parameters.AddWithValue("uid", Guid.Parse("10101010-1010-1010-1010-101010101010"));
 cp.Parameters.AddWithValue("ph", HashPassword("Corp@2026"));
 cp.Parameters.AddWithValue("orgid", Guid.Parse("20202020-2020-2020-2020-202020202020"));
 await cp.ExecuteNonQueryAsync();
 }
 Log.Information("[DB] Ensured users_usvc table, demo user, and corporate demo user");
 }

 // Ensure corporate banking tables exist (defensive for shared DB)
 static async Task EnsureCorporateTablesAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"
 CREATE TABLE IF NOT EXISTS corp_organisations (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""Name"" character varying(200) NOT NULL,
  ""RegistrationNumber"" character varying(50) NOT NULL,
  ""CreatedByUserId"" uuid NOT NULL,
  ""CreatedAt"" timestamp with time zone NOT NULL,
  ""UpdatedAt"" timestamp with time zone NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_corp_organisations_RegNum"" ON corp_organisations (""RegistrationNumber"");

 CREATE TABLE IF NOT EXISTS corp_organisation_members (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""OrganisationId"" uuid NOT NULL REFERENCES corp_organisations(""Id"") ON DELETE CASCADE,
  ""UserId"" uuid NOT NULL,
  ""Email"" character varying(254) NOT NULL,
  ""Role"" character varying(20) NOT NULL,
  ""Status"" character varying(20) NOT NULL,
  ""InvitedAt"" timestamp with time zone NOT NULL,
  ""AcceptedAt"" timestamp with time zone
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_corp_org_members_OrgUser"" ON corp_organisation_members (""OrganisationId"", ""UserId"");

 CREATE TABLE IF NOT EXISTS corp_approval_policies (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""OrganisationId"" uuid NOT NULL REFERENCES corp_organisations(""Id"") ON DELETE CASCADE,
  ""RequiredApprovals"" integer NOT NULL DEFAULT 1,
  ""MonetaryThreshold"" numeric(18,2),
  ""CreatedAt"" timestamp with time zone NOT NULL,
  ""UpdatedAt"" timestamp with time zone NOT NULL
 );
 CREATE INDEX IF NOT EXISTS ""IX_corp_approval_policies_OrgId"" ON corp_approval_policies (""OrganisationId"");

 CREATE TABLE IF NOT EXISTS corp_payment_batches (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""OrganisationId"" uuid NOT NULL REFERENCES corp_organisations(""Id"") ON DELETE CASCADE,
  ""SubmittedByUserId"" uuid NOT NULL,
  ""Status"" character varying(20) NOT NULL,
  ""Currency"" character varying(3) NOT NULL,
  ""TotalAmount"" numeric(18,2) NOT NULL,
  ""ItemCount"" integer NOT NULL,
  ""CreatedAt"" timestamp with time zone NOT NULL,
  ""SubmittedAt"" timestamp with time zone,
  ""ExecutedAt"" timestamp with time zone
 );
 CREATE INDEX IF NOT EXISTS ""IX_corp_payment_batches_OrgId"" ON corp_payment_batches (""OrganisationId"");
 CREATE INDEX IF NOT EXISTS ""IX_corp_payment_batches_Status"" ON corp_payment_batches (""Status"");

 CREATE TABLE IF NOT EXISTS corp_payment_batch_items (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""PaymentBatchId"" uuid NOT NULL REFERENCES corp_payment_batches(""Id"") ON DELETE CASCADE,
  ""SourceAccountId"" uuid NOT NULL,
  ""PayeeName"" character varying(200) NOT NULL,
  ""PayeeAccountNumber"" character varying(50),
  ""Amount"" numeric(18,2) NOT NULL,
  ""Description"" character varying(500)
 );
 CREATE INDEX IF NOT EXISTS ""IX_corp_batch_items_BatchId"" ON corp_payment_batch_items (""PaymentBatchId"");

 CREATE TABLE IF NOT EXISTS corp_approval_records (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""PaymentBatchId"" uuid NOT NULL REFERENCES corp_payment_batches(""Id"") ON DELETE CASCADE,
  ""ApprovedByUserId"" uuid NOT NULL,
  ""Decision"" character varying(20) NOT NULL,
  ""Comments"" character varying(500),
  ""DecidedAt"" timestamp with time zone NOT NULL
 );
 CREATE INDEX IF NOT EXISTS ""IX_corp_approval_records_BatchId"" ON corp_approval_records (""PaymentBatchId"");
 ";
 await using var cmd = new NpgsqlCommand(sql, conn);
 await cmd.ExecuteNonQueryAsync();
 Log.Information("[DB] Ensured corporate banking tables exist");
 }

 static async Task EnsureSanctionTablesAsync(IConfiguration cfg)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = @"
 CREATE TABLE IF NOT EXISTS ""SanctionRequests"" (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""ExternalProjectId"" character varying(200) NOT NULL,
  ""ExternalTenantId"" character varying(200) NOT NULL,
  ""UserId"" uuid NOT NULL,
  ""AccountId"" uuid NOT NULL,
  ""RequestedAmount"" numeric(18,2) NOT NULL,
  ""Currency"" character varying(10) NOT NULL,
  ""Purpose"" character varying(1000) NOT NULL,
  ""RiskScore"" integer NOT NULL DEFAULT 0,
  ""KycStatus"" character varying(20) NOT NULL DEFAULT 'Pending',
  ""AmlStatus"" character varying(20) NOT NULL DEFAULT 'Pending',
  ""Status"" character varying(20) NOT NULL DEFAULT 'Draft',
  ""ApprovedAmount"" numeric(18,2),
  ""DecisionReason"" character varying(2000),
  ""FtkTransactionRef"" character varying(200),
  ""IdempotencyKey"" character varying(200) NOT NULL,
  ""CreatedAt"" timestamp with time zone NOT NULL,
  ""UpdatedAt"" timestamp with time zone NOT NULL,
  ""CreatedBy"" character varying(200) NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SanctionRequests_IdempotencyKey"" ON ""SanctionRequests"" (""IdempotencyKey"");
 CREATE INDEX IF NOT EXISTS ""IX_SanctionRequests_ExternalProjectId_UserId"" ON ""SanctionRequests"" (""ExternalProjectId"", ""UserId"");
 CREATE INDEX IF NOT EXISTS ""IX_SanctionRequests_Status"" ON ""SanctionRequests"" (""Status"");

 CREATE TABLE IF NOT EXISTS ""SanctionAuditLogs"" (
  ""Id"" uuid NOT NULL PRIMARY KEY,
  ""SanctionRequestId"" uuid NOT NULL REFERENCES ""SanctionRequests""(""Id"") ON DELETE CASCADE,
  ""FromStatus"" character varying(20) NOT NULL,
  ""ToStatus"" character varying(20) NOT NULL,
  ""ChangedBy"" character varying(200) NOT NULL,
  ""Reason"" character varying(2000) NOT NULL,
  ""Timestamp"" timestamp with time zone NOT NULL,
  ""CorrelationId"" character varying(500) NOT NULL
 );
 CREATE INDEX IF NOT EXISTS ""IX_SanctionAuditLogs_SanctionRequestId"" ON ""SanctionAuditLogs"" (""SanctionRequestId"");
 ";
   await using var cmd = new NpgsqlCommand(sql, conn);
   await cmd.ExecuteNonQueryAsync();
   Log.Information("[DB] Ensured SanctionRequests and SanctionAuditLogs tables exist");
   }

   static async Task EnsureSarTableAsync(IConfiguration cfg)
   {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   var sql = @"
   CREATE TABLE IF NOT EXISTS ""SuspiciousActivityReports"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""TransactionId"" uuid NOT NULL,
    ""UserId"" uuid NOT NULL,
    ""AccountId"" uuid NOT NULL,
    ""Amount"" numeric(18,2) NOT NULL,
    ""Currency"" character varying(3) NOT NULL DEFAULT 'USD',
    ""Reason"" character varying(500) NOT NULL,
    ""RiskLevel"" character varying(10) NOT NULL,
    ""Status"" character varying(20) NOT NULL DEFAULT 'Open',
    ""FlaggedAt"" timestamp with time zone NOT NULL,
    ""ResolvedAt"" timestamp with time zone,
    ""Notes"" character varying(1000)
   );
   CREATE INDEX IF NOT EXISTS ""IX_SuspiciousActivityReports_TransactionId"" ON ""SuspiciousActivityReports"" (""TransactionId"");
   CREATE INDEX IF NOT EXISTS ""IX_SuspiciousActivityReports_UserId"" ON ""SuspiciousActivityReports"" (""UserId"");
   ";
   await using var cmd = new NpgsqlCommand(sql, conn);
   await cmd.ExecuteNonQueryAsync();
   Log.Information("[DB] Ensured SuspiciousActivityReports table exists");
   }

   static async Task EnsureCreditTablesAsync(IConfiguration cfg)
   {
   await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
   await conn.OpenAsync();
   var sql = @"
   CREATE TABLE IF NOT EXISTS ""CreditFacilities"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""UserId"" uuid NOT NULL,
    ""WalletAddress"" character varying(200) NOT NULL,
    ""CreditLimit"" numeric(18,2) NOT NULL DEFAULT 0,
    ""DrawnAmount"" numeric(18,2) NOT NULL DEFAULT 0,
    ""OutstandingBalance"" numeric(18,2) NOT NULL DEFAULT 0,
    ""Currency"" character varying(10) NOT NULL DEFAULT 'FTK',
    ""Status"" character varying(20) NOT NULL DEFAULT 'Active',
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL
   );
   CREATE INDEX IF NOT EXISTS ""IX_CreditFacilities_UserId"" ON ""CreditFacilities"" (""UserId"");
   CREATE INDEX IF NOT EXISTS ""IX_CreditFacilities_WalletAddress"" ON ""CreditFacilities"" (""WalletAddress"");

   CREATE TABLE IF NOT EXISTS ""CreditRepayments"" (
    ""Id"" uuid NOT NULL PRIMARY KEY,
    ""FacilityId"" uuid NOT NULL REFERENCES ""CreditFacilities""(""Id"") ON DELETE CASCADE,
    ""UserId"" uuid NOT NULL,
    ""Amount"" numeric(18,2) NOT NULL,
    ""Currency"" character varying(10) NOT NULL DEFAULT 'FTK',
    ""Status"" character varying(20) NOT NULL DEFAULT 'Completed',
    ""CreatedAt"" timestamp with time zone NOT NULL
   );
   CREATE INDEX IF NOT EXISTS ""IX_CreditRepayments_FacilityId"" ON ""CreditRepayments"" (""FacilityId"");
   CREATE INDEX IF NOT EXISTS ""IX_CreditRepayments_UserId"" ON ""CreditRepayments"" (""UserId"");
   ";
   await using var cmd = new NpgsqlCommand(sql, conn);
   await cmd.ExecuteNonQueryAsync();
   Log.Information("[DB] Ensured CreditFacilities and CreditRepayments tables exist");
   }
 }
