using System.Security.Claims;
using AutoFixture;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AccountService.Controllers;
using AccountService.Data;

namespace Tests;

/// <summary>
/// Comprehensive tests for AccountService.Controllers.AccountsController
/// Covers CRUD operations, authorization, validation, and data integrity
/// </summary>
public class AccountsControllerComprehensiveTests
{
    private readonly Fixture _fixture = new Fixture();

    private (AccountsController Controller, AccountDbContext Db) BuildController(
        ILogger<AccountsController>? logger = null)
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AccountDbContext(options);
        var loggerMock = logger ?? new Mock<ILogger<AccountsController>>().Object;

        var controller = new AccountsController(db, loggerMock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db);
    }

    private ClaimsPrincipal CreateUserPrincipal(Guid userId, string claimType = "sub")
    {
        var identity = new ClaimsIdentity(new[] { new Claim(claimType, userId.ToString()) }, "Test");
        return new ClaimsPrincipal(identity);
    }

    private string? GetProperty(object? anon, string prop)
        => anon?.GetType().GetProperty(prop)?.GetValue(anon)?.ToString();

    #region GetAccounts Tests

    [Fact]
    public async Task GetAccounts_WithValidAuth_ReturnsUserAccounts()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        var account1 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 1000m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var account2 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "0987654321",
            AccountType = "Savings",
            Balance = 5000m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Accounts.Add(account1);
        db.Accounts.Add(account2);
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccounts_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAccounts_WithInvalidUserId_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAccounts_OnlyReturnsUserOwnAccounts()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1111111111",
            AccountType = "Checking",
            Balance = 1000m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            AccountNumber = "2222222222",
            AccountType = "Checking",
            Balance = 9999m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAccounts_ReturnsEmptyListWhenNoAccounts()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(0);
    }

    #endregion

    #region CreateAccount Tests

    [Fact]
    public async Task CreateAccount_WithValidRequest_CreatesNewAccount()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Checking", "USD");

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "id").Should().NotBeNullOrEmpty();
        GetProperty(okResult.Value, "accountNumber").Should().NotBeNullOrEmpty();
        GetProperty(okResult.Value, "accountType").Should().Be("Checking");
        GetProperty(okResult.Value, "currency").Should().Be("USD");
        GetProperty(okResult.Value, "balance").Should().Be("0");

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        account.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAccount_WithoutCurrency_UsesDefault()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Savings", null);

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("USD");
    }

    [Fact]
    public async Task CreateAccount_WithCustomCurrency_UsesSpecifiedCurrency()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Checking", "EUR");

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("EUR");
    }

    [Fact]
    public async Task CreateAccount_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new CreateAccountRequest("Checking", "USD");

        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateAccount_WithInvalidUserId_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var request = new CreateAccountRequest("Checking", "USD");

        var identity = new ClaimsIdentity(new[] { new Claim("sub", "invalid-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateAccount_GeneratesUniqueAccountNumbers()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));
        await controller.CreateAccount(new CreateAccountRequest("Savings", "USD"));

        // Assert
        var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync();
        accounts.Select(a => a.AccountNumber).Should().HaveCount(2);
        accounts.Select(a => a.AccountNumber).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAccount_SetsInitialBalanceToZero()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));

        // Assert
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        account!.Balance.Should().Be(0);
    }

    [Fact]
    public async Task CreateAccount_SetCreatedAtAndUpdatedAt()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var beforeTime = DateTime.UtcNow;
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));

        // Assert
        var afterTime = DateTime.UtcNow;
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        account!.CreatedAt.Should().BeOnOrAfter(beforeTime).And.BeOnOrBefore(afterTime);
        account.UpdatedAt.Should().BeOnOrAfter(beforeTime).And.BeOnOrBefore(afterTime);
    }

    #endregion

    #region GetBalance Tests

    [Fact]
    public async Task GetBalance_WithValidAccountId_ReturnsBalance()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 5000m,
            Currency = "EUR",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "balance").Should().Be("5000");
        GetProperty(okResult.Value, "currency").Should().Be("EUR");
    }

    [Fact]
    public async Task GetBalance_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var accountId = Guid.NewGuid();

        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetBalance_WithNonExistentAccount_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBalance_PreventAccessToOtherUsersAccounts()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = otherUserId,
            AccountNumber = "9999999999",
            AccountType = "Checking",
            Balance = 9999m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Alternate Claim Type Tests

    [Fact]
    public async Task GetAccounts_WithIdClaimType_Works()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1111111111",
            AccountType = "Checking",
            Balance = 1000m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim("id", userId.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Data Persistence Tests

    [Fact]
    public async Task CreateAccount_PersistsToDatabase()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));

        // Assert
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
        account.Should().NotBeNull();
        account!.AccountType.Should().Be("Checking");
    }

    [Fact]
    public async Task GetAccounts_ReadsFromDatabase()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        // Add account directly to DB
        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Savings",
            Balance = 10000m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(1);
    }

    #endregion
}
