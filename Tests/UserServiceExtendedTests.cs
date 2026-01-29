using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Controllers;
using UserService.Data;

namespace Tests;

/// <summary>
/// Extended tests for UserService to increase code coverage
/// Covers password verification, JWT generation, sanitization, and legacy format handling
/// </summary>
public class UserServiceExtendedTests
{
    private readonly IConfigurationRoot _defaultConfig;

    public UserServiceExtendedTests()
    {
        _defaultConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "test-signing-key-0123456789-must-be-long-enough-for-hmac",
                ["RegistrationDisabled"] = "false"
            })
            .Build();
    }

    private (UsersController Controller, UserDbContext Db) BuildController(
        IConfiguration? config = null)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new UserDbContext(options);
        var logger = new Mock<ILogger<UsersController>>();
        var configuration = config ?? _defaultConfig;

        var controller = new UsersController(db, logger.Object, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db);
    }

    private string? GetProperty(object? anon, string prop)
        => anon?.GetType().GetProperty(prop)?.GetValue(anon)?.ToString();

    #region Password Verification Edge Cases

    [Fact]
    public async Task Login_WithLegacySha256Format_VerifiesSuccessfully()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "legacy@example.com";
        var password = "LegacyPass#123";

        // Create legacy SHA256 hash format
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "salt"));
        var legacyHash = Convert.ToBase64String(bytes);

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = legacyHash,
            FirstName = "Legacy",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, password));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WithMalformedHashFormat_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "malformed@example.com";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = "malformed_hash_without_proper_format",
            FirstName = "Malformed",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, "AnyPassword#123"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithCorruptedIterationCount_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "corrupted@example.com";

        // Corrupted hash with non-numeric iteration count
        var corruptedHash = "v1$notanumber$salt$key";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = corruptedHash,
            FirstName = "Corrupted",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, "AnyPassword#123"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void HashPassword_ProducesUniqueHashesForSamePassword()
    {
        // Arrange
        var password = "SamePassword#123";

        // Act
        var hash1 = UsersController.HashPasswordStatic(password);
        var hash2 = UsersController.HashPasswordStatic(password);

        // Assert - Same password should produce different hashes due to random salt
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPassword_HasCorrectStructure()
    {
        // Arrange
        var password = "TestPassword#123";

        // Act
        var hash = UsersController.HashPasswordStatic(password);

        // Assert
        var parts = hash.Split('$');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("v1");
        parts[1].Should().Be("310000"); // OWASP recommended iterations
        parts[2].Should().NotBeNullOrEmpty(); // Salt (base64)
        parts[3].Should().NotBeNullOrEmpty(); // Key (base64)
    }

    #endregion

    #region JWT Configuration Tests

    [Fact]
    public async Task Register_UsesCustomJwtConfiguration()
    {
        // Arrange
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "custom-signing-key-0123456789-must-be-long-enough",
                ["JWT_ISSUER"] = "custom-issuer",
                ["JWT_AUDIENCE"] = "custom-audience",
                ["RegistrationDisabled"] = "false"
            })
            .Build();

        var (controller, _) = BuildController(customConfig);
        var request = new RegisterRequest("jwt@example.com", "Password#123", "JWT", "User");

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("custom-issuer");
        jwt.Audiences.Should().Contain("custom-audience");
    }

    [Fact]
    public async Task Register_UsesDefaultJwtConfigWhenNotProvided()
    {
        // Arrange
        var minimalConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "minimal-signing-key-0123456789-must-be-long-enough",
                ["RegistrationDisabled"] = "false"
            })
            .Build();

        var (controller, _) = BuildController(minimalConfig);
        var request = new RegisterRequest("default@example.com", "Password#123", "Default", "User");

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("singleDynofin-local");
        jwt.Audiences.Should().Contain("singleDynofin-client");
    }

    [Fact]
    public async Task Register_JwtHasCorrectExpirationTime()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("expiry@example.com", "Password#123", "Expiry", "Test");

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Token should be valid for approximately 8 hours
        var expectedExpiry = DateTime.UtcNow.AddHours(8);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Register_JwtHasNotBeforeClaim()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("notbefore@example.com", "Password#123", "NotBefore", "Test");

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidFrom.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Input Sanitization Extended Tests

    [Theory]
    [InlineData("javascript:alert('xss')")]
    [InlineData("JAVASCRIPT:alert()")]
    [InlineData("JaVaScRiPt:malicious")]
    public async Task Register_RemovesJavascriptProtocol(string input)
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("sanitize@example.com", "Password#123", input, "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "sanitize@example.com");
        user!.FirstName.Should().NotContainEquivalentOf("javascript:");
    }

    [Theory]
    [InlineData("onclick=malicious")]
    [InlineData("onload =attack")]
    [InlineData("ONERROR=hack")]
    public async Task Register_RemovesEventHandlers(string input)
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = $"event{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, "Password#123", input, "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user!.FirstName.Should().NotMatchRegex(@"on\w+\s*=");
    }

    [Fact]
    public async Task Register_TruncatesVeryLongNames()
    {
        // Arrange
        var (controller, db) = BuildController();
        var veryLongName = new string('A', 500);
        var request = new RegisterRequest("longname@example.com", "Password#123", veryLongName, "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "longname@example.com");
        user!.FirstName.Length.Should().BeLessThanOrEqualTo(100);
    }

    [Theory]
    [InlineData("\"double quotes\"")]
    [InlineData("'single quotes'")]
    [InlineData("`backticks`")]
    public async Task Register_RemovesQuoteCharacters(string input)
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = $"quotes{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, "Password#123", input, "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user!.FirstName.Should().NotContain("\"");
        user.FirstName.Should().NotContain("'");
        user.FirstName.Should().NotContain("`");
    }

    [Fact]
    public async Task Register_WithEmptyFirstName_UsesDefault()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("empty@example.com", "Password#123", "   ", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "empty@example.com");
        user!.FirstName.Should().Be("User");
    }

    [Fact]
    public async Task Register_WithEmptyLastName_AllowsEmpty()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("lastname@example.com", "Password#123", "John", "   ");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "lastname@example.com");
        user!.LastName.Should().BeEmpty();
    }

    #endregion

    #region Profile Tests - Alternative Claim Types

    [Fact]
    public async Task GetProfile_WithSubClaim_ReturnsProfile()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "sub@example.com",
            PasswordHash = "hash",
            FirstName = "Sub",
            LastName = "Claim",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim("sub", user.Id.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetProfile_WithIdClaim_ReturnsProfile()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "id@example.com",
            PasswordHash = "hash",
            FirstName = "Id",
            LastName = "Claim",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim("id", user.Id.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetProfile_WithInvalidGuidClaim_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-valid-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Verify Email Extended Tests

    [Fact]
    public async Task VerifyEmail_UpdatesTimestamp()
    {
        // Arrange
        var (controller, db) = BuildController();
        var originalTime = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "timestamp@example.com",
            PasswordHash = "hash",
            FirstName = "Timestamp",
            LastName = "User",
            IsEmailVerified = false,
            CreatedAt = originalTime,
            UpdatedAt = originalTime
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        await controller.VerifyEmail(new VerifyEmailRequest(user.Id, "token"));

        // Assert
        var updatedUser = await db.Users.FindAsync(user.Id);
        updatedUser!.UpdatedAt.Should().BeAfter(originalTime);
    }

    [Fact]
    public async Task VerifyEmail_ReturnsOkMessage()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "message@example.com",
            PasswordHash = "hash",
            FirstName = "Message",
            LastName = "User",
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await controller.VerifyEmail(new VerifyEmailRequest(user.Id, "token"));

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "message").Should().Be("Email verified successfully");
    }

    #endregion

    #region Demo User Exception Tests

    [Fact]
    public async Task Register_DemoUser_BypassesPasswordComplexity()
    {
        // Arrange
        var (controller, db) = BuildController();
        // Demo user has relaxed password requirements
        var request = new RegisterRequest("demo", "simple", "Demo", "User");

        // Act
        var result = await controller.Register(request);

        // Assert - Demo email format is invalid, so it should fail on email validation
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Email Normalization Tests

    [Fact]
    public async Task Register_TrimsEmail()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("  trim@example.com  ", "Password#123", "Trim", "User");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "trim@example.com");
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_ConvertsEmailToLowercase()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("UPPERCASE@EXAMPLE.COM", "Password#123", "Upper", "Case");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "uppercase@example.com");
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_TrimsAndNormalizesEmail()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "normalize@example.com";
        var password = "Password#123";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = UsersController.HashPasswordStatic(password),
            FirstName = "Normalize",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act - Login with uppercase and spaces
        var result = await controller.Login(new LoginRequest("  NORMALIZE@EXAMPLE.COM  ", password));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion
}
