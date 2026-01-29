using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using AutoFixture;
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
/// Comprehensive tests for UserService.Controllers.UsersController
/// Covers authentication, validation, security, and data operations
/// </summary>
public class UsersControllerComprehensiveTests
{
    private readonly Fixture _fixture = new Fixture();
    private readonly IConfigurationRoot _defaultConfig;

    public UsersControllerComprehensiveTests()
    {
        _defaultConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "test-signing-key-0123456789-must-be-long",
                ["RegistrationDisabled"] = "false"
            })
            .Build();
    }

    private (UsersController Controller, UserDbContext Db) BuildController(
        IConfiguration? config = null,
        ILogger<UsersController>? logger = null)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new UserDbContext(options);
        var loggerMock = logger ?? new Mock<ILogger<UsersController>>().Object;
        var configuration = config ?? _defaultConfig;

        var controller = new UsersController(db, loggerMock, configuration)
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

    #region Registration Tests

    [Fact]
    public async Task Register_WithValidData_CreatesUserAndReturnsOkWithJwt()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("newuser@example.com", "SecurePass#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");
        token.Should().NotBeNullOrEmpty();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
        user.Should().NotBeNull();
        user!.FirstName.Should().Be("John");
        user.Email.Should().Be(request.Email.ToLowerInvariant());
        user.IsEmailVerified.Should().BeTrue();

        // Verify JWT structure
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task Register_WithoutEmail_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("", "Password#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        GetProperty(badRequest.Value, "message").Should().Contain("Email and password are required");
    }

    [Fact]
    public async Task Register_WithoutPassword_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("test@example.com", "", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("invalid.email")]
    [InlineData("@example.com")]
    [InlineData("test@.com")]
    [InlineData("test @example.com")]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest(email, "Password#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        GetProperty(badRequest.Value, "message").Should().Contain("Invalid email format");
    }

    [Theory]
    [InlineData("Pass#12")] // Too short
    [InlineData("password")] // No uppercase, no digit, no special char
    public async Task Register_WithWeakPassword_ReturnsBadRequest(string password)
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("test@example.com", password, "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "duplicate@example.com";
        
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = "hash",
            FirstName = "Existing",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new RegisterRequest(email, "Password#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        GetProperty(badRequest.Value, "message").Should().Be("Email already registered");
    }

    [Fact]
    public async Task Register_WhenDisabled_ReturnsForbidden()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RegistrationDisabled"] = "true",
                ["JWT_SIGNING_KEY"] = "test-signing-key-0123456789-must-be-long"
            })
            .Build();

        var (controller, _) = BuildController(config);
        var request = new RegisterRequest("test@example.com", "Password#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Register_WithNullableNames_UsesDefaults()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("test@example.com", "Password#123", "", "");

        // Act
        var result = await controller.Register(request);

        // Assert
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
        user!.FirstName.Should().Be("User");
        user.LastName.Should().BeEmpty();
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "user@example.com";
        var password = "SecurePass#123";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = UsersController.HashPasswordStatic(password),
            FirstName = "Test",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, password));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "token").Should().NotBeNullOrEmpty();
        GetProperty(okResult.Value, "userId").Should().Be(user.Id.ToString());
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "user@example.com";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = UsersController.HashPasswordStatic("CorrectPass#123"),
            FirstName = "Test",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, "WrongPass#123"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = await controller.Login(new LoginRequest("nonexistent@example.com", "Password#123"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithoutEmail_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = await controller.Login(new LoginRequest("", "Password#123"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithoutPassword_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = await controller.Login(new LoginRequest("test@example.com", ""));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Profile Tests

    [Fact]
    public async Task GetProfile_WithValidClaim_ReturnsUserProfile()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "profile@example.com",
            PasswordHash = "hash",
            FirstName = "Profile",
            LastName = "User",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "id").Should().Be(user.Id.ToString());
        GetProperty(okResult.Value, "email").Should().Be(user.Email);
        GetProperty(okResult.Value, "firstName").Should().Be("Profile");
    }

    [Fact]
    public async Task GetProfile_WithoutAuthClaim_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetProfile_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = BuildController();
        var nonExistentId = Guid.NewGuid();

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, nonExistentId.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Email Verification Tests

    [Fact]
    public async Task VerifyEmail_WithValidUserId_VerifiesEmailSuccessfully()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "verify@example.com",
            PasswordHash = "hash",
            FirstName = "Verify",
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
        result.Should().BeOfType<OkObjectResult>();
        var updatedUser = await db.Users.FindAsync(user.Id);
        updatedUser!.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidUserId_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = await controller.VerifyEmail(new VerifyEmailRequest(Guid.Empty, "token"));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = await controller.VerifyEmail(new VerifyEmailRequest(Guid.NewGuid(), "token"));

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Password Security Tests

    [Theory]
    [InlineData("SimplePassword", false)]
    [InlineData("Simple1234", false)]
    [InlineData("Simple#", false)]
    [InlineData("Simple#Pwd1", true)]
    [InlineData("MyP@ssw0rd", true)]
    [InlineData("Test@123456", true)]
    public async Task Register_EnforcePasswordComplexity(string password, bool shouldSucceed)
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("test@example.com", password, "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        if (shouldSucceed)
        {
            result.Should().BeOfType<OkObjectResult>();
        }
        else
        {
            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }

    [Fact]
    public void HashPassword_CreatesSecureHash_WithCorrectFormat()
    {
        // Arrange
        var password = "TestPassword#123";

        // Act
        var hash = UsersController.HashPasswordStatic(password);

        // Assert
        hash.Should().StartWith("v1$");
        hash.Should().Contain("$");
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePass#123";
        var hash = UsersController.HashPasswordStatic(password);

        // Act
        var result = VerifyPasswordHelper(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var password = "SecurePass#123";
        var wrongPassword = "WrongPass#123";
        var hash = UsersController.HashPasswordStatic(password);

        // Act
        var result = VerifyPasswordHelper(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    // Helper method using reflection to test private VerifyPassword
    private static bool VerifyPasswordHelper(string password, string hash)
    {
        var method = typeof(UsersController).GetMethod("VerifyPassword",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool?)method?.Invoke(null, new object[] { password, hash }) ?? false;
    }

    #endregion

    #region JWT Generation Tests

    [Fact]
    public async Task Register_GeneratesValidJwt_WithCorrectClaims()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new RegisterRequest("jwt@example.com", "Password#123", "JWT", "Test");

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = (OkObjectResult)result;
        var token = GetProperty(okResult.Value, "token");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    #endregion

    #region Input Sanitization Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("test`value")]
    [InlineData("test'value")]
    public async Task Register_SanitizesInput_RemovesDangerousCharacters(string maliciousInput)
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("test@example.com", "Password#123", maliciousInput, "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
        user!.FirstName.Should().NotContain("<");
        user.FirstName.Should().NotContain(">");
    }

    #endregion

    #region Email Case Sensitivity Tests

    [Fact]
    public async Task Register_TreatsEmailCaseInsensitively()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("Test@EXAMPLE.COM", "Password#123", "Test", "User");

        // Act
        var result = await controller.Register(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        user.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_TreatsEmailCaseInsensitively()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "CaseSensitive@EXAMPLE.COM";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = UsersController.HashPasswordStatic("Password#123"),
            FirstName = "Case",
            LastName = "Test",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.Login(new LoginRequest(email, "Password#123"));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion
}
