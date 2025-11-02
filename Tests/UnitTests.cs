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
using Xunit;

namespace Tests;

public class UserServiceTests
{
    private static (UsersController Controller, UserDbContext Db) BuildController()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new UserDbContext(options);

        var logger = new Mock<ILogger<UsersController>>();
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "test-signing-key-012345678901234567890123456789"
            })
            .Build();

        var controller = new UsersController(db, logger.Object, inMemoryConfig)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db);
    }

    private static string? GetAnonProp(object? anon, string prop)
        => anon?.GetType().GetProperty(prop)?.GetValue(anon)?.ToString();

    [Fact]
    public async Task Register_CreatesUser_AndReturnsJwt()
    {
        // Arrange
        var (controller, db) = BuildController();
        var request = new RegisterRequest("test@example.com", "Password#123", "John", "Doe");

        // Act
        var result = await controller.Register(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var token = GetAnonProp(ok.Value, "token");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Equal("John", user!.FirstName);
        Assert.True(user.IsEmailVerified);

        // Minimal sanity check on JWT structure
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == request.Email);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailExists()
    {
        // Arrange
        var (controller, db) = BuildController();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "dup@example.com",
            PasswordHash = UsersController.HashPasswordStatic("secret"),
            FirstName = "Jane",
            LastName = "Smith",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await controller.Register(new RegisterRequest("dup@example.com", "Password#123", "John", "Doe"));

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var message = GetAnonProp(bad.Value, "message");
        Assert.Equal("Email already registered", message);
    }

    [Fact]
    public async Task Login_ReturnsOk_WithToken_ForValidCredentials()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "user@example.com";
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = UsersController.HashPasswordStatic("Password#123"),
            FirstName = "User",
            LastName = "One",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await controller.Login(new LoginRequest(email, "Password#123"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var token = GetAnonProp(ok.Value, "token");
        Assert.False(string.IsNullOrWhiteSpace(token));
        var userId = GetAnonProp(ok.Value, "userId");
        Assert.False(string.IsNullOrWhiteSpace(userId));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForInvalidCredentials()
    {
        // Arrange
        var (controller, db) = BuildController();
        var email = "user2@example.com";
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = UsersController.HashPasswordStatic("correct"),
            FirstName = "User",
            LastName = "Two",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await controller.Login(new LoginRequest(email, "wrong"));

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var message = GetAnonProp(unauthorized.Value, "message");
        Assert.Equal("Invalid credentials", message);
    }

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNoClaims()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetProfile();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsProfile_ForValidUserIdClaim()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "p@example.com",
            PasswordHash = UsersController.HashPasswordStatic("x"),
            FirstName = "Pro",
            LastName = "File",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        }, "TestAuth");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetProfile();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(user.Email, GetAnonProp(ok.Value, "email"));
        Assert.Equal(user.FirstName, GetAnonProp(ok.Value, "firstName"));
        Assert.Equal("True", GetAnonProp(ok.Value, "isEmailVerified"));
    }

    [Fact]
    public async Task VerifyEmail_SetsFlag_AndPersists()
    {
        // Arrange
        var (controller, db) = BuildController();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "verify@example.com",
            PasswordHash = UsersController.HashPasswordStatic("x"),
            FirstName = "Ver",
            LastName = "Ify",
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await controller.VerifyEmail(new VerifyEmailRequest(user.Id, "dummy"));

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Email verified successfully", GetAnonProp(ok.Value, "message"));

        var updated = await db.Users.FindAsync(new object[] { user.Id }, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.True(updated!.IsEmailVerified);
        Assert.True(updated.UpdatedAt >= user.UpdatedAt);
    }
}