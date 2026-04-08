using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using UserService.Data;
using Npgsql;
using Microsoft.AspNetCore.HttpOverrides;
using UserService.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using MassTransit;
using FluentValidation;
using UserService.Services;
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

 // Bind to specific port when running locally (default7001) or Heroku PORT
 var userPort = Environment.GetEnvironmentVariable("USERS_SERVICE_PORT");
 var port = Environment.GetEnvironmentVariable("PORT");
 if (!string.IsNullOrEmpty(userPort))
 {
 builder.WebHost.UseUrls($"http://0.0.0.0:{userPort}");
 }
 else if (!string.IsNullOrEmpty(port))
 {
 builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
 }
 else
 {
 builder.WebHost.UseUrls("http://0.0.0.0:7001");
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
 builder.Services.AddDbContext<UserDbContext>(options =>
 options.UseNpgsql(connectionString));

 // Authentication - stricter validation
 builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 var authority = builder.Configuration["JWT_AUTHORITY"];
 var audience = builder.Configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
 var issuer = builder.Configuration["JWT_ISSUER"] ?? authority ?? "singleDynofin-local";
 var signingKey = builder.Configuration["JWT_SIGNING_KEY"];

 if (string.IsNullOrEmpty(authority) && string.IsNullOrEmpty(signingKey))
     throw new InvalidOperationException(
         "JWT configuration is required. Set JWT_AUTHORITY or JWT_SIGNING_KEY in environment variables.");

 if (!string.IsNullOrEmpty(authority))
 {
 options.Authority = authority;
 options.Audience = audience;
 options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateLifetime = true,
 ClockSkew = TimeSpan.Zero
 };
 }
 else if (!string.IsNullOrEmpty(signingKey))
 {
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateIssuerSigningKey = true,
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
 ValidateIssuer = true,
 ValidIssuer = issuer,
 ValidateAudience = true,
 ValidAudience = audience,
 ValidateLifetime = true,
 ClockSkew = TimeSpan.Zero
 };
 }
 });

 // IPasswordHasher
 builder.Services.AddSingleton<IPasswordHasher, PasswordHasherService>();

 // FluentValidation
 builder.Services.AddValidatorsFromAssemblyContaining<UserService.Validation.RegisterRequestValidator>(ServiceLifetime.Singleton);

 // Controllers, Swagger, Health
 builder.Services.AddControllers(options =>
 {
     options.Filters.Add<UserService.Validation.FluentValidationFilter>();
 })
 .AddJsonOptions(options =>
 {
     options.JsonSerializerOptions.Converters.Add(new UserService.Converters.Iso8601DateTimeConverter());
 });
 if (builder.Environment.IsDevelopment())
 {
     builder.Services.AddEndpointsApiExplorer();
     builder.Services.AddSwaggerGen();
 }
 builder.Services.AddHealthChecks();

 // Rate limiting for auth endpoints
 builder.Services.AddRateLimiter(options =>
 {
 options.AddFixedWindowLimiter("auth", opt =>
 {
 opt.Window = TimeSpan.FromMinutes(1);
 opt.PermitLimit =10;
 opt.QueueLimit =0;
 });
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
             Log.Information("UserService RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
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
     var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "UserService";
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

 var app = builder.Build();

 // Use forwarded headers before auth
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

 // Auto-migrate or create database and seed demo user
 using (var scope = app.Services.CreateScope())
 {
 var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();

 if ((await context.Database.GetPendingMigrationsAsync()).Any())
 await context.Database.MigrateAsync();
 else
 await context.Database.EnsureCreatedAsync();

 var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

 // Seed demo user
 if (!context.Users.Any(u => u.Email == "demo"))
 {
 var demoUser = new User
 {
 Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
 Email = "demo",
 PasswordHash = hasher.Hash("Demo@2026"),
 FirstName = "Demo",
 LastName = "User",
 IsEmailVerified = true,
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 };
 context.Users.Add(demoUser);
 context.SaveChanges();
 }
 else
 {
 // If demo user exists, ensure password is updated to Demo@2026 for consistency
 var demo = await context.Users.FirstAsync(u => u.Email == "demo");
 var desiredHash = hasher.Hash("Demo@2026");
 if (!hasher.Verify("Demo@2026", demo.PasswordHash))
 {
 demo.PasswordHash = desiredHash;
 demo.UpdatedAt = DateTime.UtcNow;
 await context.SaveChangesAsync();
 }
 }

 // Seed corporate demo user
 if (!context.Users.Any(u => u.Email == "corpadmindemo"))
 {
 var corpUser = new User
 {
 Id = Guid.Parse("10101010-1010-1010-1010-101010101010"),
 Email = "corpadmindemo",
 PasswordHash = hasher.Hash("Corp@2026"),
 FirstName = "Corporate",
 LastName = "Admin",
 IsEmailVerified = true,
 ClientType = "Corporate",
 OrganisationId = Guid.Parse("20202020-2020-2020-2020-202020202020"),
 OrganisationRole = "Admin",
 CompanyName = "Acme Corp Ltd",
 RegistrationNumber = "NZ9876543",
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 };
 context.Users.Add(corpUser);
 context.SaveChanges();
 }
 else
 {
 var corp = await context.Users.FirstAsync(u => u.Email == "corpadmindemo");
 var corpHash = hasher.Hash("Corp@2026");
 if (!hasher.Verify("Corp@2026", corp.PasswordHash))
 {
 corp.PasswordHash = corpHash;
 corp.UpdatedAt = DateTime.UtcNow;
 await context.SaveChangesAsync();
 }
 }
 }

 if (app.Environment.IsDevelopment())
 {
 app.UseSwagger();
 app.UseSwaggerUI();
 }

 app.UseAuthentication();
 app.UseAuthorization();

 // Enable rate limiting
 app.UseRateLimiter();

 app.MapHealthChecks("/health");
 app.MapControllers();

 Log.Information("UserService starting up");
 app.Run();
}
catch (Exception ex)
{
 Log.Fatal(ex, "UserService terminated unexpectedly");
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
 var databaseUrl = configuration["DATABASE_URL"]; // e.g. postgres://user:pass@host:port/db
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

 // Fallback to explicit env var for demo db
 var pgHost = configuration["PGHOST"];
 if (!string.IsNullOrEmpty(pgHost))
 {
 var cs = new NpgsqlConnectionStringBuilder
 {
 Host = pgHost,
 Port = int.TryParse(configuration["PGPORT"], out var p) ? p :5432,
 Username = configuration["PGUSER"],
 Password = configuration["PGPASSWORD"],
 Database = configuration["PGDATABASE"] ?? "postgres",
 SslMode = SslMode.Require
 };
 return cs.ToString();
 }

 throw new InvalidOperationException("No database connection string found");
}
