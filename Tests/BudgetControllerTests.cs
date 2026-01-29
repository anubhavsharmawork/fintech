using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace Tests;

/// <summary>
/// Comprehensive tests for BudgetController
/// Covers budget retrieval, validation, authorization, and edge cases
/// </summary>
public class BudgetControllerTests
{
    private (BudgetController Controller, TransactionDbContext Db, BudgetAggregationService Service) BuildController()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TransactionDbContext(options);
        var service = new BudgetAggregationService(db);

        var controller = new BudgetController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db, service);
    }

    private ClaimsPrincipal CreateUserPrincipal(Guid userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
        return new ClaimsPrincipal(identity);
    }

    #region GetBudget Validation Tests

    [Fact]
    public async Task GetBudget_WithEmptyAccountId_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;

        // Act
        var result = await controller.GetBudget(Guid.Empty, from, to, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result.Result!;
        badRequest.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBudget_WithDefaultFromDate_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(Guid.NewGuid(), default, DateTime.UtcNow, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBudget_WithDefaultToDate_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), default, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBudget_WithFromDateAfterToDate_ReturnsBadRequest()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(-30); // To is before from

        // Act
        var result = await controller.GetBudget(Guid.NewGuid(), from, to, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetBudget Success Tests

    [Fact]
    public async Task GetBudget_WithValidParameters_ReturnsOkWithBudgetData()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Coffee", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Rent", SpendingType = "Fixed", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 50, Currency = "USD", Type = "debit", Description = "Savings", SpendingType = "Future", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget.Should().NotBeNull();
        budget!.Fun.Should().Be(100);
        budget.Fixed.Should().Be(200);
        budget.Future.Should().Be(50);
        budget.Total.Should().Be(350);
    }

    [Fact]
    public async Task GetBudget_WithNoTransactions_ReturnsZeroBudget()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget.Should().NotBeNull();
        budget!.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetBudget_OnlyReturnsTransactionsInDateRange()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "In range", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 9999, Currency = "USD", Type = "debit", Description = "Out of range", SpendingType = "Fun", CreatedAt = now.AddDays(-100) }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(100);
        budget.Total.Should().Be(100);
    }

    [Fact]
    public async Task GetBudget_OnlyReturnsTransactionsForSpecificAccount()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Account 1", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId2, UserId = userId, Amount = 500, Currency = "USD", Type = "debit", Description = "Account 2", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId1, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(100);
    }

    [Fact]
    public async Task GetBudget_ReturnsPeriodDates()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, from, to, CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Period.Should().NotBeNull();
        budget.Period.From.Should().NotBeNullOrEmpty();
        budget.Period.To.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBudget_WithCancellationToken_RespondsToCancel()
    {
        // Arrange
        var (controller, _, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw or handle cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await controller.GetBudget(accountId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, cts.Token));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetBudget_WithSameDayFromAndTo_ReturnsTransactionsForThatDay()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Same day",
            SpendingType = "Fun",
            CreatedAt = today.AddHours(12)
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, today, today.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(100);
    }

    [Fact]
    public async Task GetBudget_WithMixedSpendingTypes_AggregatesCorrectly()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 10, Currency = "USD", Type = "debit", Description = "1", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 20, Currency = "USD", Type = "debit", Description = "2", SpendingType = "fun", CreatedAt = now }, // lowercase
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 30, Currency = "USD", Type = "debit", Description = "3", SpendingType = "FUN", CreatedAt = now }, // uppercase
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 40, Currency = "USD", Type = "debit", Description = "4", SpendingType = "Fixed", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 50, Currency = "USD", Type = "debit", Description = "5", SpendingType = "FIXED", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 60, Currency = "USD", Type = "debit", Description = "6", SpendingType = "Future", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(60); // 10 + 20 + 30
        budget.Fixed.Should().Be(90); // 40 + 50
        budget.Future.Should().Be(60);
        budget.Total.Should().Be(210);
    }

    [Fact]
    public async Task GetBudget_IgnoresInvalidSpendingTypes()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Valid", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 999, Currency = "USD", Type = "debit", Description = "Invalid", SpendingType = "InvalidType", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 888, Currency = "USD", Type = "debit", Description = "Null", SpendingType = null, CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 777, Currency = "USD", Type = "debit", Description = "Empty", SpendingType = "", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(100);
        budget.Total.Should().Be(100); // Only the valid one counted
    }

    [Fact]
    public async Task GetBudget_WithLargeAmounts_CalculatesCorrectly()
    {
        // Arrange
        var (controller, db, _) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 999999999.99m, Currency = "USD", Type = "debit", Description = "Large", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 0.01m, Currency = "USD", Type = "debit", Description = "Small", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetBudget(accountId, now.AddDays(-1), now.AddDays(1), CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var budget = okResult.Value as BudgetAggregationDto;
        budget!.Fun.Should().Be(1000000000m);
    }

    #endregion
}
