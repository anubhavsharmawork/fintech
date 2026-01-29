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
 var signingKey = builder.Configuration["JWT_SIGNING_KEY"] ?? "demo-signing-key-change-me-0123456789-XYZ987654321";

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

 // Controllers, Swagger, Health
 builder.Services.AddControllers();
 builder.Services.AddEndpointsApiExplorer();
 builder.Services.AddSwaggerGen();
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
 headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
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

 // Seed demo user
 if (!context.Users.Any(u => u.Email == "demo"))
 {
 var demoUser = new User
 {
 Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
 Email = "demo",
 PasswordHash = UsersController.HashPasswordStatic("Demo@2026"),
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
 var desiredHash = UsersController.HashPasswordStatic("Demo@2026");
 if (demo.PasswordHash != desiredHash)
 {
 demo.PasswordHash = desiredHash;
 demo.UpdatedAt = DateTime.UtcNow;
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
