using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Controllers;
using UserService.Data;
using UserService.Services;

namespace Tests;

public class UserServiceAdditionalTests
{
    private const string TestJwtKey = "super-secret-test-signing-key-32-chars!!";

    private (UsersController Controller, UserDbContext Db)
        BuildController(bool registrationDisabled = false, Guid? claimUserId = null)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new UserDbContext(options);
        var logger = new Mock<ILogger<UsersController>>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = TestJwtKey,
                ["JWT_ISSUER"] = "test-issuer",
                ["JWT_AUDIENCE"] = "test-audience",
                ["RegistrationDisabled"] = registrationDisabled ? "true" : "false"
            })
            .Build();

        var passwordHasher = new PasswordHasherService();
        var controller = new UsersController(db, logger.Object, config, passwordHasher)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (claimUserId.HasValue)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, claimUserId.Value.ToString())
            }, "Test");
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
        }

        return (controller, db);
    }

    private User MakeUser(string password = "Pass@1234") => new()
    {
        Id = Guid.NewGuid(),
        Email = $"user-{Guid.NewGuid()}@example.com",
        PasswordHash = new PasswordHasherService().Hash(password),
        FirstName = "Test",
        LastName = "User",
        KycStatus = "Verified",
        ClientType = "Individual",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── Registration disabled ──────────────────────────────────────────────

    [Fact]
    public async Task Register_Returns403_WhenRegistrationDisabled()
    {
        var (controller, _) = BuildController(registrationDisabled: true);
        var request = new RegisterRequest("test@example.com", "Pass@1234X", "First", "Last");

        var result = await controller.Register(request);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
    }

    // ── Corporate registration ─────────────────────────────────────────────

    [Fact]
    public async Task Register_Corporate_StoresCompanyAndRegistrationNumberAndOrgFields()
    {
        var (controller, db) = BuildController(registrationDisabled: false);
        var request = new RegisterRequest(
            "corp@example.com", "Corp@1234X", "Alice", "Smith",
            ClientType: "Corporate",
            CompanyName: "Acme Ltd",
            RegistrationNumber: "REG-001");

        var result = await controller.Register(request);

        result.Should().BeOfType<OkObjectResult>();
        var user = await db.Users.FirstAsync(u => u.Email == "corp@example.com");
        user.ClientType.Should().Be("Corporate");
        user.CompanyName.Should().Be("Acme Ltd");
        user.RegistrationNumber.Should().Be("REG-001");
        user.OrganisationId.Should().NotBeNull();
        user.OrganisationRole.Should().Be("Admin");
    }

    [Fact]
    public async Task Register_Individual_HasNullOrgFields()
    {
        var (controller, db) = BuildController(registrationDisabled: false);
        var request = new RegisterRequest(
            "ind@example.com", "Ind@1234X", "Bob", "Jones",
            ClientType: "Individual");

        await controller.Register(request);

        var user = await db.Users.FirstAsync(u => u.Email == "ind@example.com");
        user.ClientType.Should().Be("Individual");
        user.OrganisationId.Should().BeNull();
        user.OrganisationRole.Should().BeNull();
        user.CompanyName.Should().BeNull();
    }

    // ── GetProfile ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetProfile();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.GetProfile();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WithProfileData()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildController(claimUserId: userId);
        var user = MakeUser();
        user.Id = userId;
        user.ClientType = "Corporate";
        user.CompanyName = "TestCorp";
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.GetProfile();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!.ToString()!;
        body.Should().Contain(userId.ToString());
        body.Should().Contain("Corporate");
    }

    // ── UpdateTimezone ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTimezone_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.UpdateTimezone(new UpdateTimezoneRequest("Pacific/Auckland", 720));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateTimezone_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.UpdateTimezone(new UpdateTimezoneRequest("UTC", 0));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateTimezone_PersistsTimeZoneIdAndOffset()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildController(claimUserId: userId);
        var user = MakeUser();
        user.Id = userId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.UpdateTimezone(new UpdateTimezoneRequest("Pacific/Auckland", 720));

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FindAsync(userId);
        updated!.TimeZoneId.Should().Be("Pacific/Auckland");
        updated.UtcOffsetMinutes.Should().Be(720);
    }

    [Fact]
    public async Task UpdateTimezone_AcceptsNullValues()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildController(claimUserId: userId);
        var user = MakeUser();
        user.Id = userId;
        user.TimeZoneId = "UTC";
        user.UtcOffsetMinutes = 0;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.UpdateTimezone(new UpdateTimezoneRequest(null, null));

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FindAsync(userId);
        updated!.TimeZoneId.Should().BeNull();
        updated.UtcOffsetMinutes.Should().BeNull();
    }

    // ── ChangePassword ────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.ChangePassword(new ChangePasswordRequest("old", "New@1234X"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsEmpty()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.ChangePassword(new ChangePasswordRequest("", "New@1234X"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordIsTooWeak()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.ChangePassword(new ChangePasswordRequest("Pass@1234", "short"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenNewPasswordLacksComplexity()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.ChangePassword(new ChangePasswordRequest("Pass@1234", "alllowercase1"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (controller, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.ChangePassword(new ChangePasswordRequest("Pass@1234", "New@1234X"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsIncorrect()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildController(claimUserId: userId);
        var user = MakeUser(password: "Pass@1234");
        user.Id = userId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.ChangePassword(new ChangePasswordRequest("WrongPass@1234", "New@1234X"));

        result.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)result;
        bad.Value!.ToString().Should().Contain("incorrect");
    }

    [Fact]
    public async Task ChangePassword_ReturnsOk_AndUpdatesHash_WhenValid()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildController(claimUserId: userId);
        var user = MakeUser(password: "Pass@1234");
        user.Id = userId;
        var originalHash = user.PasswordHash;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.ChangePassword(new ChangePasswordRequest("Pass@1234", "New@1234X!"));

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FindAsync(userId);
        updated!.PasswordHash.Should().NotBe(originalHash);
        new PasswordHasherService().Verify("New@1234X!", updated.PasswordHash).Should().BeTrue();
    }
}
