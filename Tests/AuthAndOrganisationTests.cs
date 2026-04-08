using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccountService.Controllers;
using AccountService.Data;
using AccountService.Policy;
using AccountService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using UserService.Controllers;
using UserService.Data;
using UserService.Services;

namespace Tests;

// ── UsersController – Login / Refresh / Logout / VerifyEmail ──────────────

public class UsersControllerAuthTests
{
    private static readonly string SigningKey = "test-signing-key-0123456789-must-be-long";

    private static IConfigurationRoot BuildConfig(bool disabled = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = SigningKey,
                ["JWT_ISSUER"] = "test-issuer",
                ["JWT_AUDIENCE"] = "test-audience",
                ["RegistrationDisabled"] = disabled ? "true" : "false"
            })
            .Build();

    private static (UsersController Controller, UserDbContext Db) BuildController(
        IConfiguration? cfg = null)
    {
        var db = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var httpContext = new DefaultHttpContext();
        var controller = new UsersController(
            db,
            new Mock<ILogger<UsersController>>().Object,
            cfg ?? BuildConfig(),
            new PasswordHasherService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        return (controller, db);
    }

    private static User SeedUser(UserDbContext db, string password, bool corporate = false)
    {
        var hasher = new PasswordHasherService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user_{Guid.NewGuid():N}@test.com",
            PasswordHash = hasher.Hash(password),
            FirstName = "Test",
            LastName = "User",
            IsEmailVerified = false,
            ClientType = corporate ? "Corporate" : "Individual",
            OrganisationId = corporate ? Guid.NewGuid() : null,
            OrganisationRole = corporate ? "Admin" : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private string BuildRefreshToken(Guid userId, string email, bool useRefreshTokenUse = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("token_use", useRefreshTokenUse ? "refresh" : "access")
        };
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-10),
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Login ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsOk_WithToken_WhenCredentialsValid()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");

        var result = await ctrl.Login(new LoginRequest(user.Email, "P@ssw0rd1!"));

        result.Should().BeOfType<OkObjectResult>();
        var val = ((OkObjectResult)result).Value;
        val.Should().NotBeNull();
        GetProp(val!, "token").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenEmailNotFound()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.Login(new LoginRequest("nobody@test.com", "P@ssw0rd1!"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordWrong()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");

        var result = await ctrl.Login(new LoginRequest(user.Email, "WrongPass1!"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenEmailMissing()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.Login(new LoginRequest("", "P@ssw0rd1!"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordMissing()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");

        var result = await ctrl.Login(new LoginRequest(user.Email, ""));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_NormalizesEmail_BeforeLookup()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");

        // login with uppercase email
        var result = await ctrl.Login(new LoginRequest(user.Email.ToUpper(), "P@ssw0rd1!"));

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    [Fact]
    public void Refresh_ReturnsUnauthorized_WhenNoCookiePresent()
    {
        var (ctrl, _) = BuildController();
        // No cookie set

        var result = ctrl.Refresh();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void Refresh_ReturnsUnauthorized_WhenTokenIsGarbage()
    {
        var (ctrl, _) = BuildController();
        SetRefreshCookie(ctrl, "not.a.jwt.token.at.all");

        var result = ctrl.Refresh();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void Refresh_ReturnsUnauthorized_WhenTokenUseIsAccess()
    {
        var (ctrl, _) = BuildController();
        var token = BuildRefreshToken(Guid.NewGuid(), "x@x.com", useRefreshTokenUse: false);
        SetRefreshCookie(ctrl, token);

        var result = ctrl.Refresh();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void Refresh_ReturnsOk_WithNewToken_WhenValid()
    {
        var (ctrl, _) = BuildController();
        var userId = Guid.NewGuid();
        var token = BuildRefreshToken(userId, "user@test.com");
        SetRefreshCookie(ctrl, token);

        var result = ctrl.Refresh();

        result.Should().BeOfType<OkObjectResult>();
        var val = ((OkObjectResult)result).Value;
        GetProp(val!, "token").Should().NotBeNullOrEmpty();
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public void Logout_ReturnsOk_WhenNoCookiePresent()
    {
        var (ctrl, _) = BuildController();

        var result = ctrl.Logout();

        result.Should().BeOfType<OkObjectResult>();
        GetProp(((OkObjectResult)result).Value!, "message").Should().Be("Logged out");
    }

    [Fact]
    public void Logout_ReturnsOk_WhenValidRefreshTokenCookiePresent()
    {
        var (ctrl, _) = BuildController();
        var token = BuildRefreshToken(Guid.NewGuid(), "user@test.com");
        SetRefreshCookie(ctrl, token);

        var result = ctrl.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void Logout_ReturnsOk_WhenRefreshCookieHasMalformedToken()
    {
        var (ctrl, _) = BuildController();
        SetRefreshCookie(ctrl, "garbage-token");

        var result = ctrl.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── VerifyEmail ────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.VerifyEmail(new VerifyEmailRequest(Guid.Empty, "token"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.VerifyEmail(new VerifyEmailRequest(Guid.NewGuid(), "token"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_SetsIsEmailVerified_AndReturnsOk()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");
        user.IsEmailVerified = false;
        await db.SaveChangesAsync();

        var result = await ctrl.VerifyEmail(new VerifyEmailRequest(user.Id, "token"));

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FindAsync(user.Id);
        updated!.IsEmailVerified.Should().BeTrue();
    }

    // ── Register – additional paths ────────────────────────────────────────

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordTooShort()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.Register(new RegisterRequest(
            "test@example.com", "abc", "First", "Last", "Individual", null, null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordLacksComplexity()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.Register(new RegisterRequest(
            "test@example.com", "simplepwd", "First", "Last", "Individual", null, null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailInvalid()
    {
        var (ctrl, _) = BuildController();

        var result = await ctrl.Register(new RegisterRequest(
            "not-an-email", "P@ssw0rd1!", "First", "Last", "Individual", null, null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailAlreadyRegistered()
    {
        var (ctrl, db) = BuildController();
        var user = SeedUser(db, "P@ssw0rd1!");

        var result = await ctrl.Register(new RegisterRequest(
            user.Email, "P@ssw0rd1!", "First", "Last", "Individual", null, null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string? GetProp(object obj, string name) =>
        obj.GetType().GetProperty(name)?.GetValue(obj)?.ToString();

    private static void SetRefreshCookie(UsersController ctrl, string value)
    {
        var cookieValue = value;
        var cookies = new Mock<IRequestCookieCollection>();
        cookies.Setup(c => c.TryGetValue("rt", out cookieValue))
            .Returns(true);
        ctrl.HttpContext.Request.Cookies = cookies.Object;
    }
}

// ── AccountsController – GetOrganisationAccounts ──────────────────────────

public class AccountsOrganisationTests
{
    private static AccountDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AccountsController BuildController(
        AccountDbContext db, Guid? userId = null, Guid? orgId = null)
    {
        var mockPolicy = new Mock<IAccountLimitPolicy>();
        mockPolicy.Setup(p => p.CanCreateAccountAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(LimitCheckResult.Allow());

        var mockCache = new Mock<ICacheService>();
        mockCache.Setup(c => c.GetAsync<List<AccountDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AccountDto>?)null);

        var svc = new AccountService.Services.AccountService(
            db,
            new Mock<ILogger<AccountService.Services.AccountService>>().Object,
            mockCache.Object,
            mockPolicy.Object);

        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim("sub", userId.Value.ToString()));
        }
        if (orgId.HasValue)
            claims.Add(new Claim("organisation_id", orgId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var ctrl = new AccountsController(svc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        return ctrl;
    }

    private static Account SeedAccount(AccountDbContext db, Guid userId, Guid? orgId = null) =>
        SeedAccountWithBalance(db, userId, orgId, 1000m);

    private static Account SeedAccountWithBalance(AccountDbContext db, Guid userId, Guid? orgId, decimal balance)
    {
        var acc = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = Guid.NewGuid().ToString("N")[..10],
            AccountType = "Current",
            Balance = balance,
            Currency = "NZD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ClientType = "Corporate",
            OrganisationId = orgId
        };
        db.Accounts.Add(acc);
        db.SaveChanges();
        return acc;
    }

    [Fact]
    public async Task GetOrganisationAccounts_ReturnsUnauthorized_WhenNoOrgClaim()
    {
        var db = BuildDb();
        var ctrl = BuildController(db, userId: Guid.NewGuid(), orgId: null);

        var result = await ctrl.GetOrganisationAccounts(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetOrganisationAccounts_ReturnsForbid_WhenOrgIdMismatch()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var ctrl = BuildController(db, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisationAccounts(Guid.NewGuid()); // Different org

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetOrganisationAccounts_ReturnsOk_WhenOrgIdMatches()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedAccount(db, userId, orgId);
        var ctrl = BuildController(db, userId: userId, orgId: orgId);

        var result = await ctrl.GetOrganisationAccounts(orgId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrganisationAccounts_ReturnsOnlyOrgAccounts()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        SeedAccount(db, userId, orgId);
        SeedAccount(db, userId, orgId);
        SeedAccount(db, Guid.NewGuid(), Guid.NewGuid()); // Different org

        var ctrl = BuildController(db, userId: userId, orgId: orgId);

        var result = await ctrl.GetOrganisationAccounts(orgId);

        var ok = (OkObjectResult)result;
        var accounts = (ok.Value as IEnumerable<object>)?.ToList();
        accounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOrganisationAccounts_ReturnsEmpty_WhenNoAccountsForOrg()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var ctrl = BuildController(db, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisationAccounts(orgId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var accounts = (ok.Value as IEnumerable<object>)?.ToList();
        accounts.Should().BeEmpty();
    }
}
