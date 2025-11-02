using ApiGateway.Data;
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

 // Configuration
 builder.Configuration
 .SetBasePath(builder.Environment.ContentRootPath)
 .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
 .AddEnvironmentVariables();

 // Build a FileConfiguration with downstream port bound to current app
 var ocelotBase = builder.Configuration.GetSection(string.Empty).Get<FileConfiguration>() ?? new FileConfiguration();
 // Determine current port (Heroku provides PORT; local defaults to5000 or Kestrel value)
 var dsPort = !string.IsNullOrEmpty(port) && int.TryParse(port, out var herokuPort) ? herokuPort :5000;
 foreach (var route in ocelotBase.Routes ?? new List<FileRoute>())
 {
 if (route.DownstreamHostAndPorts != null)
 {
 foreach (var hp in route.DownstreamHostAndPorts)
 {
 hp.Host = "127.0.0.1";
 hp.Port = dsPort;
 }
 }
 // Make downstream path hit our local minimal API endpoints (strip /api prefix if present)
 if (!string.IsNullOrEmpty(route.DownstreamPathTemplate) && route.DownstreamPathTemplate.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
 {
 route.DownstreamPathTemplate = route.DownstreamPathTemplate.Substring(4); // remove '/api'
 }
 }

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
 }
 else
 {
 Log.Warning("[DB][EF] Skipping LedgerDbContext configuration because no DB connection string was resolved.");
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
          opt.PermitLimit = 30;
          opt.QueueLimit = 0;
        });
        
        // Rate limiting for account operations
        options.AddFixedWindowLimiter("accounts", opt =>
        {
   opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 20;
        opt.QueueLimit = 0;
    });
        
 // Global rejection response
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
   await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Rate limit exceeded. Please try again later." }, cancellationToken);
     };
 });

 // Swagger (dev)
 builder.Services.AddEndpointsApiExplorer();
 builder.Services.AddSwaggerGen();

 if (enableOcelot)
 {
 builder.Services.AddSingleton<IFileConfigurationRepository>(sp => new InMemoryFileConfigRepository(ocelotBase));
 builder.Services.AddOcelot();
 }

 builder.Services.AddHealthChecks();
 builder.Services.AddControllers();

 // HttpClient for downstream UserService (local default, overridable via USERS_SERVICE_URL)
 builder.Services.AddHttpClient("users", client =>
 {
 var baseUrl = builder.Configuration["USERS_SERVICE_URL"] ?? "http://127.0.0.1:7001";
 client.BaseAddress = new Uri(baseUrl);
 client.Timeout = TimeSpan.FromSeconds(10);
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
 headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
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
 await next();
 });
 app.UseSerilogRequestLogging();

 // RESEED: optionally drop ledger tables and clear EF history on startup when explicitly requested
 var reseed = builder.Configuration.GetValue<bool>("RESEED_DB", false)
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
 }

 // Ensure tables exist even if EF migrations failed (defensive for Heroku/ephemeral envs)
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
 }

 // Seed demo data idempotently after migrations (default true)
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
 path.Equals("/transactions", StringComparison.OrdinalIgnoreCase)))
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
 var token = GenerateJwt(cfg, u.Id, u.Email);
 var refresh = GenerateRefreshJwt(cfg, u.Id, u.Email);
 AppendRefreshCookie(http, refresh);
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
 }).RequireRateLimiting("auth");

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
 return Results.Ok(new { id = newUserId, email = req.Email, firstName = fnm, lastName = lnm, token = jwtInMem });
 }
 return Results.BadRequest(new { message = "Email already registered" });
 }
 }).RequireRateLimiting("auth");

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
 var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
 var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? principal.FindFirst(ClaimTypes.Name)?.Value ?? "demo";
 if (!Guid.TryParse(sub, out var userId)) return Results.Unauthorized();

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

 // Logout: clear refresh cookie
 app.MapPost("/users/logout", (HttpContext http) =>
 {
 try
 {
 http.Response.Cookies.Delete("rt", new CookieOptions { Path = "/" });
 }
 catch { }
 return Results.Ok(new { message = "Logged out" });
 }).RequireRateLimiting("auth");

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
 var result = list.Select(a => new { id = a.Id, accountNumber = a.AccountNumber, accountType = a.AccountType, balance = a.Balance, currency = a.Currency }).ToList<object>();
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
 var result = list.Select(a => new { id = a.Id, accountNumber = a.AccountNumber, accountType = a.AccountType, balance = a.Balance, currency = a.Currency }).ToList<object>();
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
 var initial = req.InitialDeposit.GetValueOrDefault(0);

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
 if (initial >0)
 {
 // credit initial
 var trx = new InMemTransaction(Guid.NewGuid(), id, userId, initial, currency, "credit", "Initial deposit", DateTime.UtcNow);
 InMemoryData.TransactionsByUser.AddOrUpdate(userId, _ => new List<InMemTransaction> { trx }, (_, l) => { l.Add(trx); return l; });
 acc.Balance += initial;
 acc.UpdatedAt = DateTime.UtcNow;
 }
 return Results.Created($"/accounts/{id}", new { id, accountNumber = acc.AccountNumber, accountType = acc.AccountType, balance = acc.Balance, currency = acc.Currency });
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
 cmd.Parameters.AddWithValue("bal", initial);
 cmd.Parameters.AddWithValue("cur", currency);
 await cmd.ExecuteNonQueryAsync();
 }
 if (initial >0)
 {
 // also insert a credit transaction
 await CreateTransactionAsync(cfg, userId, id, initial, currency, "credit", "Initial deposit");
 }
 return Results.Created($"/accounts/{id}", new { id, accountNumber = accNum, accountType = accountType, balance = initial, currency });
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
 cmd.Parameters.AddWithValue("bal", initial);
 cmd.Parameters.AddWithValue("cur", currency);
 await cmd.ExecuteNonQueryAsync();
 }
 if (initial >0)
 {
 await CreateTransactionAsync(cfg, userId, id, initial, currency, "credit", "Initial deposit");
 }
 return Results.Created($"/accounts/{id}", new { id, accountNumber = accNum, accountType = accountType, balance = initial, currency });
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
 }).RequireAuthorization().RequireRateLimiting("accounts");

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
 var result = list.OrderByDescending(t => t.CreatedAt).Select(t => new { id = t.Id, accountId = t.AccountId, amount = t.Amount, currency = t.Currency, type = t.Type, description = t.Description, createdAt = t.CreatedAt }).ToList<object>();
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

 // Basic validation
 var typeNorm = (req.Type ?? string.Empty).Trim().ToLowerInvariant();
 if (req.AccountId == Guid.Empty || req.Amount <=0 || (typeNorm != "credit" && typeNorm != "debit"))
 {
 return Results.BadRequest(new { message = "Invalid transaction request" });
 }

 if (!TryGetConnectionString(cfg, out _))
 {
 Log.Warning("[DB] No DB configured, using in-memory for POST /transactions");
 // in-memory
 if (!InMemoryData.AccountsByUser.TryGetValue(userId, out var list) || !list.Any(a => a.Id == req.AccountId))
 return Results.NotFound(new { message = "Account not found" });
 var acc = list.First(a => a.Id == req.AccountId);
 var delta = typeNorm == "credit" ? req.Amount : -req.Amount;
 if (typeNorm == "debit" && acc.Balance < req.Amount) return Results.BadRequest(new { message = "Insufficient funds" });
 acc.Balance += delta;
 acc.UpdatedAt = DateTime.UtcNow;
 var trx = new InMemTransaction(Guid.NewGuid(), req.AccountId, userId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? acc.Currency : req.Currency!, typeNorm, req.Description ?? string.Empty, DateTime.UtcNow);
 InMemoryData.TransactionsByUser.AddOrUpdate(userId, _ => new List<InMemTransaction> { trx }, (_, l) => { l.Add(trx); return l; });
 return Results.Created($"/transactions/{trx.Id}", new { id = trx.Id, accountId = trx.AccountId, amount = trx.Amount, currency = trx.Currency, type = trx.Type, description = trx.Description, createdAt = trx.CreatedAt });
 }

 try
 {
 Log.Information("[DB] Using DB for POST /transactions");
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? "NZD" : req.Currency!, typeNorm, req.Description ?? string.Empty);
 return Results.Created($"/transactions/{created.Id}", created);
 }
 catch (KeyNotFoundException)
 {
 return Results.NotFound(new { message = "Account not found" });
 }
 catch (UnauthorizedAccessException)
 {
 return Results.Forbid();
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
 var created = await CreateTransactionAsync(cfg, userId, req.AccountId, req.Amount, string.IsNullOrWhiteSpace(req.Currency) ? "NZD" : req.Currency!, typeNorm, req.Description ?? string.Empty);
 return Results.Created($"/transactions/{created.Id}", created);
 }
 catch (Exception ex)
 {
 Log.Error(ex, "[DB] Ensure schema + retry failed for POST /transactions");
 return Results.Problem("Failed to create transaction", statusCode:500);
 }
 }
 catch (Exception ex)
 {
 Log.Error(ex, "Create transaction failed for {UserId}", userId);
 return Results.Problem("Failed to create transaction", statusCode:500);
 }
 }).RequireAuthorization().RequireRateLimiting("transactions");

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
 return Results.Ok(list.Select(p => new { id = p.Id, name = p.Name, accountNumber = p.AccountNumber }).ToList());
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
 res.Add(new { id = reader.GetGuid(0), name = reader.GetString(1), accountNumber = reader.GetString(2) });
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
 return Results.Ok(new[] { new { id, name = "Demo Payee", accountNumber = "DEMO1234567890" } });
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
 return Results.Created($"/payees/{p.Id}", new { id = p.Id, name = p.Name, accountNumber = p.AccountNumber });
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
 return Results.Created($"/payees/{id2}", new { id = id2, name = req.Name, accountNumber = req.AccountNumber });
 }).RequireAuthorization().RequireRateLimiting("accounts");

 app.MapPost("/payments", async (HttpContext http, IConfiguration cfg, CreatePaymentRequest req) =>
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
 }).RequireAuthorization().RequireRateLimiting("transactions");

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
 static async Task<(Guid Id, string Email, string PasswordHash)?> FindUserByEmailAsync(IConfiguration cfg, string email)
 {
 if (!TryGetConnectionString(cfg, out _)) return null;
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"Email\", \"PasswordHash\" FROM users_usvc WHERE \"Email\"=@e LIMIT 1";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("e", email);
 await using var reader = await cmd.ExecuteReaderAsync();
 if (await reader.ReadAsync())
 return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
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
 list.Add(new { id = reader.GetGuid(0), accountNumber = reader.GetString(1), accountType = reader.GetString(2), balance = reader.GetDecimal(3), currency = reader.GetString(4) });
 }
 return list;
 }

 static async Task<List<object>> GetTransactionsAsync(IConfiguration cfg, Guid userId)
 {
 if (!TryGetConnectionString(cfg, out _)) return new List<object>();
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 var sql = "SELECT \"Id\", \"AccountId\", \"Amount\", \"Currency\", \"Type\", \"Description\", \"CreatedAt\" FROM \"LedgerTransactions\" WHERE \"UserId\"=@uid ORDER BY \"CreatedAt\" DESC";
 await using var cmd = new NpgsqlCommand(sql, conn);
 cmd.Parameters.AddWithValue("uid", userId);
 await using var reader = await cmd.ExecuteReaderAsync();
 var list = new List<object>();
 while (await reader.ReadAsync())
 {
 list.Add(new { id = reader.GetGuid(0), accountId = reader.GetGuid(1), amount = reader.GetDecimal(2), currency = reader.GetString(3), type = reader.GetString(4), description = reader.GetString(5), createdAt = reader.GetDateTime(6) });
 }
 return list;
 }

 internal record TransactionResponse(Guid Id, Guid AccountId, decimal Amount, string Currency, string Type, string Description, DateTime CreatedAt);

 static async Task<TransactionResponse> CreateTransactionAsync(IConfiguration cfg, Guid userId, Guid accountId, decimal amount, string currency, string type, string description)
 {
 await using var conn = new NpgsqlConnection(GetConnectionString(cfg));
 await conn.OpenAsync();
 await using var tx = await conn.BeginTransactionAsync();

 decimal currentBalance;
 string existingCurrency;

 var checkSql = "SELECT \"Balance\", \"Currency\" FROM \"LedgerAccounts\" WHERE \"Id\"=@aid AND \"UserId\"=@uid";
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

 await tx.CommitAsync();
 return new TransactionResponse(tid, accountId, amount, currency, type, description ?? string.Empty, DateTime.UtcNow);
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

 var token = new JwtSecurityToken(
 issuer: issuer,
 audience: audience,
 claims: claims,
 notBefore: DateTime.UtcNow,
 expires: DateTime.UtcNow.AddMinutes(30),
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
 expires: DateTime.UtcNow.AddDays(30),
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
 Expires = DateTimeOffset.UtcNow.AddDays(30)
 };
 http.Response.Cookies.Append("rt", refresh, opts);
 }

 internal record LoginRequest(string Email, string Password);
 internal record RegisterRequest(string Email, string Password, string FirstName, string LastName);
 internal record SeedStatus(long AccountsCount, long TransactionsCount, bool DemoTransactionsPresent, List<string> Migrations);

 internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
 {
 public int TemperatureF =>32 + (int)(TemperatureC /0.5556);
 }

 internal record InMemUser(Guid Id, string Email, string PasswordHash, string FirstName, string LastName);
 internal static class InMemoryUsersStore
 {
 public static readonly ConcurrentDictionary<string, InMemUser> Users = new(StringComparer.OrdinalIgnoreCase);
 }

 internal record CreateTransactionRequest(Guid AccountId, decimal Amount, string? Currency, string Type, string? Description);

 internal static class InMemoryData
 {
 public static readonly ConcurrentDictionary<Guid, List<InMemAccount>> AccountsByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemTransaction>> TransactionsByUser = new();
 public static readonly ConcurrentDictionary<Guid, List<InMemPayee>> PayeesByUser = new();
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

 internal record InMemTransaction(Guid Id, Guid AccountId, Guid UserId, decimal Amount, string Currency, string Type, string Description, DateTime CreatedAt);
 internal record InMemPayee(Guid Id, Guid UserId, string Name, string AccountNumber, DateTime CreatedAt);

 internal record CreateAccountRequest(string? AccountType, string? Currency, decimal? InitialDeposit);
 internal record CreatePayeeRequest(string? Name, string? AccountNumber);
 internal record CreatePaymentRequest(Guid AccountId, decimal Amount, string? PayeeName, string? PayeeAccountNumber, string? Description);

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

 // Ensure transactions
 var t1 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
 var t2 = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
 var t3 = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
 var insTrx = @"INSERT INTO ""LedgerTransactions"" (""Id"", ""AccountId"", ""UserId"", ""Amount"", ""Currency"", ""Type"", ""Description"", ""CreatedAt"")
 SELECT @id, @aid, @uid, @amt, 'NZD', @typ, @desc, NOW() - @ago
 WHERE NOT EXISTS (SELECT 1 FROM ""LedgerTransactions"" WHERE ""Id""=@id)";
 await using (var ti = new NpgsqlCommand(insTrx, conn))
 {
 ti.Parameters.AddWithValue("id", t1);
 ti.Parameters.AddWithValue("aid", acc1);
 ti.Parameters.AddWithValue("uid", userId);
 ti.Parameters.AddWithValue("amt",100.00m);
 ti.Parameters.AddWithValue("typ", "credit");
 ti.Parameters.AddWithValue("desc", "Salary deposit");
 ti.Parameters.AddWithValue("ago", TimeSpan.FromDays(1));
 await ti.ExecuteNonQueryAsync();
 }
 await using (var tg = new NpgsqlCommand(insTrx, conn))
 {
 tg.Parameters.AddWithValue("id", t2);
 tg.Parameters.AddWithValue("aid", acc1);
 tg.Parameters.AddWithValue("uid", userId);
 tg.Parameters.AddWithValue("amt",50.00m);
 tg.Parameters.AddWithValue("typ", "debit");
 tg.Parameters.AddWithValue("desc", "Grocery shopping");
 tg.Parameters.AddWithValue("ago", TimeSpan.FromDays(2));
 await tg.ExecuteNonQueryAsync();
 }
 await using (var tt = new NpgsqlCommand(insTrx, conn))
 {
 tt.Parameters.AddWithValue("id", t3);
 tt.Parameters.AddWithValue("aid", acc2);
 tt.Parameters.AddWithValue("uid", userId);
 tt.Parameters.AddWithValue("amt",500.00m);
 tt.Parameters.AddWithValue("typ", "credit");
 tt.Parameters.AddWithValue("desc", "Transfer from checking");
 tt.Parameters.AddWithValue("ago", TimeSpan.FromDays(3));
 await tt.ExecuteNonQueryAsync();
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
 await using (var rdr = await cmd.ExecuteReaderAsync())
 {
 while (await rdr.ReadAsync()) appliedMigrations.Add(rdr.GetString(0));
 }
 return new SeedStatus(accountsCount, transactionsCount, demoTxCount >0, appliedMigrations);
 }

 // Destructive reset of ledger schema on demand (drop tables and clear EF history entries for this context)
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
 ""CreatedAt"" timestamp with time zone NOT NULL
 );
 CREATE INDEX IF NOT EXISTS ""IX_LedgerTransactions_AccountId"" ON ""LedgerTransactions"" (""AccountId"");
 CREATE INDEX IF NOT EXISTS ""IX_LedgerTransactions_UserId"" ON ""LedgerTransactions"" (""UserId"");

 CREATE TABLE IF NOT EXISTS ""LedgerPayees"" (
 ""Id"" uuid NOT NULL PRIMARY KEY,
 ""UserId"" uuid NOT NULL,
 ""Name"" character varying(200) NOT NULL,
 ""AccountNumber"" character varying(50) NOT NULL,
 ""CreatedAt"" timestamp with time zone NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LedgerPayees_UserId_AccountNumber"" ON ""LedgerPayees"" (""UserId"", ""AccountNumber"");";
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
 ""CreatedAt"" timestamp with time zone NOT NULL,
 ""UpdatedAt"" timestamp with time zone NOT NULL
 );
 CREATE UNIQUE INDEX IF NOT EXISTS ""IX_users_usvc_Email"" ON users_usvc (""Email"");";
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
 Log.Information("[DB] Ensured users_usvc table and demo user");
 }
}
