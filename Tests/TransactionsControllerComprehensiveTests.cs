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
/// Comprehensive tests for TransactionService.Controllers.TransactionsController
/// Covers transaction CRUD, spending type validation, publishing, and authorization
/// </summary>
public class TransactionsControllerComprehensiveTests
{
    private (TransactionsController Controller, TransactionDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildController(ILogger<TransactionsController>? logger = null)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TransactionDbContext(options);
        var loggerMock = logger ?? new Mock<ILogger<TransactionsController>>().Object;
        var publisherMock = new Mock<IPublishEndpoint>();

        var controller = new TransactionsController(db, publisherMock.Object, loggerMock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db, publisherMock);
    }

    private ClaimsPrincipal CreateUserPrincipal(Guid userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
        return new ClaimsPrincipal(identity);
    }

    private string? GetProperty(object? anon, string prop)
        => anon?.GetType().GetProperty(prop)?.GetValue(anon)?.ToString();

    #region GetTransactions Tests

    [Fact]
    public async Task GetTransactions_WithValidAuth_ReturnsUserTransactions()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var tx1 = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Coffee",
            SpendingType = "Fun",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var tx2 = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Amount = 50,
            Currency = "USD",
            Type = "credit",
            Description = "Refund",
            SpendingType = "Fun",
            CreatedAt = DateTime.UtcNow
        };

        db.Transactions.AddRange(tx1, tx2);
        await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTransactions_FiltersByAccountId()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId2, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions(accountId1);

        // Assert
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_OrdersByCreatedAtDescending()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddHours(-2) },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 150, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddHours(-1) }
        );
        await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<object>().ToList();
        transactions.Should().HaveCount(3);
        // Verify first transaction has the highest amount (most recent)
        var firstAmount = GetProperty(transactions![0], "amount");
        firstAmount.Should().Be("200");
    }

    [Fact]
    public async Task GetTransactions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransactions_OnlyReturnsUserOwnTransactions()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = otherUserId, Amount = 9999, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        var okResult = (OkObjectResult)result;
        var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        transactions.Should().HaveCount(1);
    }

    #endregion

    #region CreateTransaction Tests

    [Fact]
    public async Task CreateTransaction_WithValidData_CreatesAndPublishesEvent()
    {
        // Arrange
        var (controller, db, publisher) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 100m,
            Currency = "USD",
            Type = "debit",
            Description = "Test transaction",
            SpendingType = "Fun",
            TxHash = "0x123abc"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == userId);
        transaction.Should().NotBeNull();
        transaction!.Description.Should().Be("Test transaction");

        publisher.Verify(p => p.Publish(It.IsAny<TransactionCreated>(), default), Times.Once);
    }

    [Theory]
    [InlineData("Fun")]
    [InlineData("Fixed")]
    [InlineData("Future")]
    [InlineData("fun")] // Case insensitive
    public async Task CreateTransaction_WithValidSpendingType_Succeeds(string spendingType)
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
            SpendingType = spendingType
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Other")]
    [InlineData("")]
    public async Task CreateTransaction_WithInvalidSpendingType_ReturnsBadRequest(string spendingType)
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
            SpendingType = spendingType
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTransaction_WithNullSpendingType_UsesDefault()
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
            SpendingType = null
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "spendingType").Should().Be("Fun");
    }

    [Fact]
    public async Task CreateTransaction_WithoutCurrency_UsesDefault()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = null,
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
    public async Task CreateTransaction_NormalizeCurrency()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "eur",
            Type = "debit",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == userId);
        transaction!.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task CreateTransaction_WithoutType_UsesDefault()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Type = null,
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        var okResult = (OkObjectResult)result;
        GetProperty(okResult.Value, "type").Should().Be("debit");
    }

    [Fact]
    public async Task CreateTransaction_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD"
        };

        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateTransaction_AssociatesToCorrectUser()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == userId);
        transaction!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateTransaction_TrimsWhitespace()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "  usd  ",
            Type = "  debit  ",
            Description = "  Test  ",
            TxHash = "  0x123  ",
            SpendingType = "Fun"
        };

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        await controller.CreateTransaction(request);

        // Assert
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == userId);
        transaction!.Currency.Should().Be("USD");
        transaction.Type.Should().Be("debit");
        transaction.Description.Should().Be("Test");
        transaction.TxHash.Should().Be("0x123");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CreateTransaction_WithInvalidUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        var request = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD"
        };

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransactions_WithIdClaimType_Works()
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
            Description = "Test transaction",
            SpendingType = "Fun",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(new[] { new Claim("id", userId.ToString()) }, "Test");
        controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion
}
