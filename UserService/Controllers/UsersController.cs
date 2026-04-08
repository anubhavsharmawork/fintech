using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using UserService.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _context;
    private readonly ILogger<UsersController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher _passwordHasher;

    private static readonly Regex EmailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PasswordComplexityRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _revokedTokens = new();

    public UsersController(UserDbContext context, ILogger<UsersController> logger, IConfiguration configuration, IPasswordHasher passwordHasher)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (_configuration.GetValue<bool>("RegistrationDisabled", true))
        {
            return StatusCode(403, new { message = "Registration is currently disabled for security reasons." });
        }

        // Input validation
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Registration attempt with missing email or password");
            return BadRequest(new { message = "Email and password are required" });
        }

        // Sanitize and validate email
        var email = request.Email.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(email))
        {
            _logger.LogWarning("Registration attempt with invalid email format: {Email}", email);
            return BadRequest(new { message = "Invalid email format" });
        }

        // Password complexity validation
        if (request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters long" });
        }

        // For production: enforce password complexity
        if (!string.Equals(email, "demo", StringComparison.OrdinalIgnoreCase) && 
            !PasswordComplexityRegex.IsMatch(request.Password))
        {
            return BadRequest(new { message = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character" });
        }

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            _logger.LogWarning("Registration attempt for existing email");
            return BadRequest(new { message = "Email already registered" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = SanitizeInput(request.FirstName) ?? "User",
            LastName = SanitizeInput(request.LastName) ?? "",
            IsEmailVerified = true,
            ClientType = string.Equals(request.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase) ? "Corporate" : "Individual",
            OrganisationId = string.Equals(request.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase) ? Guid.NewGuid() : null,
            OrganisationRole = string.Equals(request.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase) ? "Admin" : null,
            CompanyName = string.Equals(request.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase) ? SanitizeInput(request.CompanyName) : null,
            RegistrationNumber = string.Equals(request.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase) ? SanitizeInput(request.RegistrationNumber) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {UserId}", user.Id);

        var token = GenerateJwt(user);
        var refreshToken = GenerateRefreshJwt(user);
        AppendRefreshCookie(refreshToken);
        return Ok(new { 
            id = user.Id, 
            email = user.Email, 
            firstName = user.FirstName, 
            lastName = user.LastName,
            clientType = user.ClientType,
            organisationId = user.OrganisationId,
            organisationRole = user.OrganisationRole,
            token
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with missing credentials");
            return Unauthorized(new { message = "Email and password are required" });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", email);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = GenerateJwt(user);
        var refreshToken = GenerateRefreshJwt(user);
        AppendRefreshCookie(refreshToken);
        _logger.LogInformation("User logged in: {UserId}", user.Id);

        return Ok(new { 
            message = "Login successful",
            userId = user.Id,
            token
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("GetProfile called with invalid userId claim");
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("GetProfile: User not found {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        return Ok(new { 
            id = user.Id, 
            email = user.Email, 
            firstName = user.FirstName, 
            lastName = user.LastName,
            isEmailVerified = user.IsEmailVerified,
            clientType = user.ClientType,
            organisationId = user.OrganisationId,
            organisationRole = user.OrganisationRole,
            companyName = user.CompanyName,
            timeZoneId = user.TimeZoneId,
            utcOffsetMinutes = user.UtcOffsetMinutes
        });
    }

    [HttpPost("verify-email")]
    [Authorize]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { message = "Invalid user ID" });
        }

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
        {
            _logger.LogWarning("Email verification attempted for non-existent user: {UserId}", request.UserId);
            return NotFound(new { message = "User not found" });
        }

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified for user: {UserId}", user.Id);
        return Ok(new { message = "Email verified successfully" });
    }

    [HttpPut("timezone")]
    [Authorize]
    public async Task<IActionResult> UpdateTimezone([FromBody] UpdateTimezoneRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("UpdateTimezone called with invalid userId claim");
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("UpdateTimezone: User not found {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        user.TimeZoneId = request.TimeZoneId;
        user.UtcOffsetMinutes = request.UtcOffsetMinutes;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Timezone updated for user {UserId}: {TimeZoneId} (UTC offset {Offset} min)",
            userId, request.TimeZoneId ?? "null", request.UtcOffsetMinutes?.ToString() ?? "null");

        return Ok(new
        {
            message = "Timezone preference updated",
            timeZoneId = user.TimeZoneId,
            utcOffsetMinutes = user.UtcOffsetMinutes
        });
    }


    private string GenerateJwt(User user)
    {
        var signingKey = _configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
        var audience = _configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("token_use", "access"),
            new Claim("client_type", user.ClientType ?? "Individual")
        };

        if (user.OrganisationId.HasValue)
            claims.Add(new Claim("organisation_id", user.OrganisationId.Value.ToString()));

        if (!string.IsNullOrEmpty(user.OrganisationRole))
            claims.Add(new Claim("organisation_role", user.OrganisationRole));

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

    private string GenerateRefreshJwt(User user)
    {
        var signingKey = _configuration["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
        var audience = _configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Email),
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

    private void AppendRefreshCookie(string refreshToken)
    {
        var secure = HttpContext.Request.IsHttps ||
                     string.Equals(HttpContext.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        HttpContext.Response.Cookies.Append("rt", refreshToken, opts);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public IActionResult Refresh()
    {
        if (!HttpContext.Request.Cookies.TryGetValue("rt", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized();

        try
        {
            var signingKey = _configuration["JWT_SIGNING_KEY"]
                ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");
            var issuer = _configuration["JWT_ISSUER"] ?? _configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
            var audience = _configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
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

            var tokenUse = principal.FindFirst("token_use")?.Value;
            if (!string.Equals(tokenUse, "refresh", StringComparison.Ordinal))
                return Unauthorized();

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti) && _revokedTokens.ContainsKey(jti))
            {
                _logger.LogWarning("Attempt to use revoked refresh token {Jti}", jti);
                return Unauthorized();
            }

            var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? "demo";
            if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

            // Revoke old refresh token
            if (!string.IsNullOrEmpty(jti) && validated is JwtSecurityToken jwt)
                RevokeToken(jti, jwt.ValidTo);

            var user = new User 
            { 
                Id = userId, 
                Email = email,
                FirstName = principal.FindFirst(JwtRegisteredClaimNames.GivenName)?.Value ?? "",
                LastName = principal.FindFirst(JwtRegisteredClaimNames.FamilyName)?.Value ?? "",
                ClientType = "Individual",
                PasswordHash = "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var newAccess = GenerateJwt(user);
            var newRefresh = GenerateRefreshJwt(user);
            AppendRefreshCookie(newRefresh);

            return Ok(new { token = newAccess, userId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh token invalid");
            return Unauthorized();
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public IActionResult Logout()
    {
        try
        {
            if (HttpContext.Request.Cookies.TryGetValue("rt", out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    var signingKey = _configuration["JWT_SIGNING_KEY"];
                    if (string.IsNullOrEmpty(signingKey)) return Ok(new { message = "Logged out" });
                    var issuer = _configuration["JWT_ISSUER"] ?? _configuration["JWT_AUTHORITY"] ?? "singleDynofin-local";
                    var audience = _configuration["JWT_AUDIENCE"] ?? "singleDynofin-client";
                    var handler = new JwtSecurityTokenHandler();
                    var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateLifetime = false,
                        ClockSkew = TimeSpan.Zero
                    }, out var validated);

                    var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti) && validated is JwtSecurityToken jwt)
                    {
                        RevokeToken(jti, jwt.ValidTo);
                        _logger.LogInformation("Refresh token {Jti} revoked during logout", jti);
                    }
                }
                catch
                {
                    // Token may be malformed; still clear the cookie
                }
            }

            HttpContext.Response.Cookies.Delete("rt", new CookieOptions { Path = "/" });
        }
        catch { }

        return Ok(new { message = "Logged out" });
    }

    private static void RevokeToken(string jti, DateTime expiry)
    {
        _revokedTokens[jti] = expiry;
        // Cleanup expired entries
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _revokedTokens)
        {
            if (kvp.Value < now)
                _revokedTokens.TryRemove(kvp.Key, out _);
        }
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Current password and new password are required." });

        if (request.NewPassword.Length < 8 || !PasswordComplexityRegex.IsMatch(request.NewPassword))
            return BadRequest(new { message = "New password must be at least 8 characters and contain uppercase, lowercase, number, and special character." });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found." });

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Failed password change attempt for user {UserId}", userId);
            return BadRequest(new { message = "Current password is incorrect." });
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password changed for user {UserId}", userId);
        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>
    /// Sanitize user input to prevent XSS and injection attacks
    /// </summary>
    private static string? SanitizeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        
        // Remove potentially dangerous characters
        var sanitized = input.Trim();
        sanitized = Regex.Replace(sanitized, @"[<>""'`]", "");
        sanitized = Regex.Replace(sanitized, @"javascript:", "", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"on\w+\s*=", "", RegexOptions.IgnoreCase);
        
        // Limit length
        if (sanitized.Length > 100)
          sanitized = sanitized.Substring(0, 100);
        
        return sanitized;
    }
}

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? ClientType = "Individual",
    string? CompanyName = null,
    string? RegistrationNumber = null);
public record LoginRequest(string Email, string Password);
public record VerifyEmailRequest(Guid UserId, string Token);
public record UpdateTimezoneRequest(string? TimeZoneId, int? UtcOffsetMinutes);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
