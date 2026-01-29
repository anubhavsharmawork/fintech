using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models.Dtos;
using Contracts.Events;

namespace Tests;

/// <summary>
/// Extended tests for TransactionsController to increase code coverage
/// Covers CreateTransaction edge cases, TxHash handling, and event publishing
/// </summary>
public class TransactionsControllerExtendedTests
{
    private (TransactionsController Controller, TransactionDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildController()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TransactionDbContext(options);
        var logger = new Mock<ILogger<TransactionsController>>();
        var publisher = new Mock<IPublishEndpoint>();

        var controller = new TransactionsController(db, publisher.Object, logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db, publisher);
    }

    private ClaimsPrincipal CreateUserPrincipal(Guid userId, string claimType = "sub")
    {
        var identity = new ClaimsIdentity(new[] { new Claim(claimType, userId.ToString()) }, "Test");
        return new ClaimsPrincipal(identity);
    }

    private string? GetProperty(object? anon, string prop)
        => anon?.GetType().GetProperty(prop)?.GetValue(anon)?.ToString();

    #region CreateTransaction TxHash Tests

    [Fact]
    public async Task CreateTransaction_WithTxHash_StoresTxHash()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var txHash = "0xabc123def456789012345678901234567890123456789012345678901234567890";
        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 100m,
            Currency = "ETH",
            Type = "debit",
            SpendingType = "Fun",
            TxHash = txHash
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "txHash").Should().Be(txHash);

        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.TxHash.Should().Be(txHash);
    }

    [Fact]
    public async Task CreateTransaction_WithEmptyTxHash_StoresNull()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun",
            TxHash = ""
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.TxHash.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransaction_WithWhitespaceTxHash_StoresNull()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun",
            TxHash = "   "
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.TxHash.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransaction_TrimsTxHash()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "ETH",
            Type = "debit",
            SpendingType = "Fun",
            TxHash = "  0xabc123  "
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.TxHash.Should().Be("0xabc123");
    }

    #endregion

    #region CreateTransaction Currency Tests

    [Fact]
    public async Task CreateTransaction_WithWhitespaceCurrency_UsesDefault()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "   ",
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "currency").Should().Be("USD");
    }

    [Fact]
    public async Task CreateTransaction_NormalizesToUppercase()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "  eur  ",
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Currency.Should().Be("EUR");
    }

    [Theory]
    [InlineData("btc", "BTC")]
    [InlineData("ETH", "ETH")]
    [InlineData("uSd", "USD")]
    [InlineData("GbP", "GBP")]
    public async Task CreateTransaction_NormalizesCurrencyCase(string input, string expected)
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = input,
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Currency.Should().Be(expected);
    }

    #endregion

    #region CreateTransaction Type Tests

    [Fact]
    public async Task CreateTransaction_WithNullType_UsesDefault()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = null!,
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Type.Should().Be("debit");
    }

    [Fact]
    public async Task CreateTransaction_WithEmptyType_UsesDefault()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Type.Should().Be("debit");
    }

    [Fact]
    public async Task CreateTransaction_TrimsType()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "  credit  ",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Type.Should().Be("credit");
    }

    [Theory]
    [InlineData("debit")]
    [InlineData("credit")]
    [InlineData("transfer")]
    public async Task CreateTransaction_WithVariousTypes_Stores(string type)
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = type,
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Type.Should().Be(type);
    }

    #endregion

    #region CreateTransaction Description Tests

    [Fact]
    public async Task CreateTransaction_WithNullDescription_StoresEmpty()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun",
            Description = null
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTransaction_TrimsDescription()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun",
            Description = "  Coffee at Starbucks  "
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        transaction.Description.Should().Be("Coffee at Starbucks");
    }

    #endregion

    #region CreateTransaction Event Publishing Tests

    [Fact]
    public async Task CreateTransaction_PublishesEventWithCorrectData()
    {
        // Arrange
        var (controller, _, publisher) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        TransactionCreated? capturedEvent = null;

        publisher.Setup(p => p.Publish(It.IsAny<TransactionCreated>(), default))
            .Callback<TransactionCreated, CancellationToken>((e, _) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 250.50m,
            Currency = "EUR",
            Type = "credit",
            SpendingType = "Fixed"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.AccountId.Should().Be(accountId);
        capturedEvent.UserId.Should().Be(userId);
        capturedEvent.Amount.Should().Be(250.50m);
        capturedEvent.Currency.Should().Be("EUR");
        capturedEvent.Type.Should().Be("credit");
    }

    [Fact]
    public async Task CreateTransaction_PublishesEventWithTransactionId()
    {
        // Arrange
        var (controller, db, publisher) = BuildController();
        var userId = Guid.NewGuid();
        TransactionCreated? capturedEvent = null;

        publisher.Setup(p => p.Publish(It.IsAny<TransactionCreated>(), default))
            .Callback<TransactionCreated, CancellationToken>((e, _) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstAsync(t => t.UserId == userId);
        capturedEvent!.TransactionId.Should().Be(transaction.Id);
    }

    #endregion

    #region CreateTransaction Authorization Tests

    [Fact]
    public async Task CreateTransaction_WithIdClaim_Succeeds()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId, "id");

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidGuidClaim_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            SpendingType = "Fun"
        };

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetTransactions Tests

    [Fact]
    public async Task GetTransactions_WithIdClaim_ReturnsTransactions()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = userId,
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = "Fun",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId, "id");

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_ReturnsTransactionWithTxHash()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var txHash = "0x123abc456def";

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = userId,
            Amount = 100m,
            Currency = "ETH",
            Type = "debit",
            Description = "Crypto tx",
            SpendingType = "Fun",
            TxHash = txHash,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<object>().ToList();
        GetProperty(transactions![0], "txHash").Should().Be(txHash);
    }

    [Fact]
    public async Task GetTransactions_ReturnsEmptyListWhenNoTransactions()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        transactions.Should().HaveCount(0);
    }

    #endregion

    #region Transaction Model Tests

    [Fact]
    public async Task Transaction_AllPropertiesArePersisted()
    {
        // Arrange
        var (_, db, _) = BuildController();
        var transactionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = transactionId,
            AccountId = accountId,
            UserId = userId,
            Amount = 123.45m,
            Currency = "EUR",
            Type = "credit",
            Description = "Test description",
            SpendingType = "Fixed",
            TxHash = "0xabc123",
            CreatedAt = createdAt
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        // Act
        var retrieved = await db.Transactions.FindAsync(transactionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.AccountId.Should().Be(accountId);
        retrieved.UserId.Should().Be(userId);
        retrieved.Amount.Should().Be(123.45m);
        retrieved.Currency.Should().Be("EUR");
        retrieved.Type.Should().Be("credit");
        retrieved.Description.Should().Be("Test description");
        retrieved.SpendingType.Should().Be("Fixed");
        retrieved.TxHash.Should().Be("0xabc123");
        retrieved.CreatedAt.Should().Be(createdAt);
    }

    #endregion
}
