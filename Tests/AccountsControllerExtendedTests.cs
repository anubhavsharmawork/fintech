using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AccountService.Controllers;
using AccountService.Data;

namespace Tests;

/// <summary>
/// Extended tests for AccountsController to increase code coverage
/// Covers GetBalance edge cases, CreateAccount variations, and authorization scenarios
/// </summary>
public class AccountsControllerExtendedTests
{
    private (AccountsController Controller, AccountDbContext Db) BuildController()
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AccountDbContext(options);
        var logger = new Mock<ILogger<AccountsController>>();

        var controller = new AccountsController(db, logger.Object)
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

    #region GetBalance Tests

    [Fact]
    public async Task GetBalance_WithValidAccountId_ReturnsCorrectBalance()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var balance = 1234.56m;

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = balance,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "balance").Should().Be(balance.ToString());
        GetProperty(okResult.Value, "currency").Should().Be("USD");
    }

    [Fact]
    public async Task GetBalance_WithNonExistentAccount_ReturnsNotFound()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var nonExistentAccountId = Guid.NewGuid();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(nonExistentAccountId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBalance_WithOtherUsersAccount_ReturnsNotFound()
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
            AccountNumber = "1234567890",
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

    [Fact]
    public async Task GetBalance_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetBalance(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetBalance_WithInvalidGuidClaim_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _) = BuildController();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetBalance(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetBalance_WithZeroBalance_ReturnsZero()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 0m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "balance").Should().Be("0");
    }

    [Fact]
    public async Task GetBalance_WithDifferentCurrency_ReturnsCorrectCurrency()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 500m,
            Currency = "EUR",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("EUR");
    }

    [Fact]
    public async Task GetBalance_WithLargeBalance_ReturnsCorrectValue()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var largeBalance = 9999999999.99m;

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = largeBalance,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBalance(accountId);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "balance").Should().Be(largeBalance.ToString());
    }

    #endregion

    #region CreateAccount Extended Tests

    [Fact]
    public async Task CreateAccount_WithEmptyCurrency_UsesDefault()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Savings", "");

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("USD");
    }

    [Fact]
    public async Task CreateAccount_WithWhitespaceCurrency_UsesDefault()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Savings", "   ");

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("USD");
    }

    [Theory]
    [InlineData("Checking")]
    [InlineData("Savings")]
    [InlineData("Investment")]
    [InlineData("CryptoWallet")]
    public async Task CreateAccount_WithVariousAccountTypes_Succeeds(string accountType)
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest(accountType, "USD");

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "accountType").Should().Be(accountType);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("ETH")]
    [InlineData("BTC")]
    public async Task CreateAccount_WithVariousCurrencies_Succeeds(string currency)
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreateAccountRequest("Checking", currency);

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be(currency);
    }

    [Fact]
    public async Task CreateAccount_MultipleTimes_CreatesMultipleAccounts()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));
        await controller.CreateAccount(new CreateAccountRequest("Savings", "USD"));
        await controller.CreateAccount(new CreateAccountRequest("Investment", "EUR"));

        // Assert
        var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync();
        accounts.Should().HaveCount(3);
    }

    #endregion

    #region GetAccounts Extended Tests

    [Fact]
    public async Task GetAccounts_WithIdClaim_ReturnsAccounts()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        db.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 100m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId, "id");

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAccounts_ReturnsAllAccountProperties()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 500m,
            Currency = "EUR",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<object>().ToList();
        accounts.Should().HaveCount(1);

        var firstAccount = accounts![0];
        GetProperty(firstAccount, "id").Should().Be(accountId.ToString());
        GetProperty(firstAccount, "accountNumber").Should().Be("1234567890");
        GetProperty(firstAccount, "accountType").Should().Be("Checking");
        GetProperty(firstAccount, "balance").Should().Be("500");
        GetProperty(firstAccount, "currency").Should().Be("EUR");
    }

    [Fact]
    public async Task GetAccounts_WithMultipleAccounts_ReturnsAll()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountNumber = $"123456789{i}",
                AccountType = i % 2 == 0 ? "Checking" : "Savings",
                Balance = i * 100m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        accounts.Should().HaveCount(5);
    }

    #endregion

    #region Account Model Tests

    [Fact]
    public async Task Account_CryptoProperties_AreStored()
    {
        // Arrange
        var (_, db) = BuildController();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "1234567890",
            AccountType = "CryptoWallet",
            Balance = 1.5m,
            Currency = "ETH",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsCrypto = true,
            Blockchain = "Ethereum",
            Address = "0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C",
            TokenSymbol = "ETH"
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        // Act
        var retrieved = await db.Accounts.FindAsync(account.Id);

        // Assert
        retrieved!.IsCrypto.Should().BeTrue();
        retrieved.Blockchain.Should().Be("Ethereum");
        retrieved.Address.Should().Be("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C");
        retrieved.TokenSymbol.Should().Be("ETH");
    }

    [Fact]
    public async Task Account_DefaultCryptoValues_AreCorrect()
    {
        // Arrange
        var (_, db) = BuildController();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 100m,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        // Act
        var retrieved = await db.Accounts.FindAsync(account.Id);

        // Assert
        retrieved!.IsCrypto.Should().BeFalse();
        retrieved.TokenSymbol.Should().Be("FTK"); // Default value
    }

    #endregion
}
