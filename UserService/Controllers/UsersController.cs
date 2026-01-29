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

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _context;
    private readonly ILogger<UsersController> _logger;
    private readonly IConfiguration _configuration;

    private const string DefaultDemoSigningKey = "demo-signing-key-change-me-0123456789-XYZ987654321";
    private static readonly Regex EmailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PasswordComplexityRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$", RegexOptions.Compiled);

    public UsersController(UserDbContext context, ILogger<UsersController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
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
            PasswordHash = HashPassword(request.Password),
            FirstName = SanitizeInput(request.FirstName) ?? "User",
            LastName = SanitizeInput(request.LastName) ?? "",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {UserId}", user.Id);
        
        var token = GenerateJwt(user);
        return Ok(new { 
            id = user.Id, 
            email = user.Email, 
            firstName = user.FirstName, 
            lastName = user.LastName,
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
        
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", email);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = GenerateJwt(user);
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
            isEmailVerified = user.IsEmailVerified
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

    public static string HashPasswordStatic(string password) => HashPassword(password);

    /// <summary>
    /// Hash password using PBKDF2 with SHA256 (310,000 iterations per OWASP recommendations)
    /// </summary>
    private static string HashPassword(string password)
    {
        const int iterations = 310_000; // OWASP recommendation for PBKDF2-SHA256
        const int saltSize = 16;
        const int keySize = 32;
        var salt = RandomNumberGenerator.GetBytes(saltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keySize);
        return $"v1${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// Verify password with support for both PBKDF2 (v1) and legacy SHA256 formats
    /// </summary>
    private static bool VerifyPassword(string password, string hash)
    {
        // PBKDF2 v1 format
        var parts = hash.Split('$');
        if (parts.Length == 4 && parts[0] == "v1")
        {
            if (!int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // Legacy SHA256 + "salt" format (backward compatibility)
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "salt"));
        var legacy = Convert.ToBase64String(bytes);
        return legacy == hash;
    }

    private string GenerateJwt(User user)
    {
        var signingKey = _configuration["JWT_SIGNING_KEY"] ?? DefaultDemoSigningKey;
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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(8), // Increased from 30 minutes for better UX
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
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

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record LoginRequest(string Email, string Password);
public record VerifyEmailRequest(Guid UserId, string Token);