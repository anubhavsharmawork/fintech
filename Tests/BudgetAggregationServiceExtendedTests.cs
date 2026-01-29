using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Services;

namespace Tests;

/// <summary>
/// Extended tests for BudgetAggregationService to ensure full coverage
/// Covers edge cases, period formatting, and case sensitivity
/// </summary>
public class BudgetAggregationServiceExtendedTests
{
    private (BudgetAggregationService Service, TransactionDbContext Db) BuildService()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TransactionDbContext(options);
        var service = new BudgetAggregationService(db);
        return (service, db);
    }

    #region Case Sensitivity Tests

    [Theory]
    [InlineData("Fun")]
    [InlineData("FUN")]
    [InlineData("fun")]
    [InlineData("fUn")]
    public async Task GetBudgetAsync_HandlesFunCaseInsensitively(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = spendingType,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fun.Should().Be(100);
    }

    [Theory]
    [InlineData("Fixed")]
    [InlineData("FIXED")]
    [InlineData("fixed")]
    [InlineData("fIxEd")]
    public async Task GetBudgetAsync_HandlesFixedCaseInsensitively(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = spendingType,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fixed.Should().Be(100);
    }

    [Theory]
    [InlineData("Future")]
    [InlineData("FUTURE")]
    [InlineData("future")]
    [InlineData("fUtUrE")]
    public async Task GetBudgetAsync_HandlesFutureCaseInsensitively(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = spendingType,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Future.Should().Be(100);
    }

    #endregion

    #region Invalid Spending Type Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBudgetAsync_IgnoresNullOrWhitespaceSpendingType(string? spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = spendingType,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Total.Should().Be(0);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Entertainment")]
    [InlineData("Other")]
    [InlineData("Misc")]
    [InlineData("Bills")]
    public async Task GetBudgetAsync_IgnoresInvalidSpendingTypes(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = spendingType,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Total.Should().Be(0);
    }

    #endregion

    #region Period DTO Tests

    [Fact]
    public async Task GetBudgetAsync_ReturnsPeriodWithIso8601Format()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var from = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 2, 15, 14, 45, 0, DateTimeKind.Utc);

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Period.Should().NotBeNull();
        result.Period.From.Should().Contain("2024-01-15");
        result.Period.To.Should().Contain("2024-02-15");
    }

    [Fact]
    public async Task GetBudgetAsync_PeriodMatchesInputDates()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Period.From.Should().Contain(from.Year.ToString());
        result.Period.To.Should().Contain(to.Year.ToString());
    }

    #endregion

    #region Decimal Precision Tests

    [Fact]
    public async Task GetBudgetAsync_HandlesFractionalAmounts()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 10.99m, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 0.01m, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fun.Should().Be(11m);
    }

    [Fact]
    public async Task GetBudgetAsync_HandlesLargeAmounts()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 999999999.99m,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = "Fixed",
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fixed.Should().Be(999999999.99m);
    }

    [Fact]
    public async Task GetBudgetAsync_HandlesZeroAmounts()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = Guid.NewGuid(),
            Amount = 0m,
            Currency = "USD",
            Type = "debit",
            Description = "Test",
            SpendingType = "Fun",
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fun.Should().Be(0);
        result.Total.Should().Be(0);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GetBudgetAsync_RespectsCancellationToken()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.GetBudgetAsync(accountId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, cts.Token));
    }

    [Fact]
    public async Task GetBudgetAsync_WithDefaultCancellationToken_Succeeds()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();

        // Act
        var result = await service.GetBudgetAsync(accountId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Mixed Scenarios

    [Fact]
    public async Task GetBudgetAsync_MixedValidAndInvalidSpendingTypes()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 200, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Invalid", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 300, Currency = "USD", Type = "debit", Description = "Test", SpendingType = null, CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 400, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fixed", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fun.Should().Be(100);
        result.Fixed.Should().Be(400);
        result.Total.Should().Be(500); // Only valid types counted
    }

    [Fact]
    public async Task GetBudgetAsync_MultipleAccountsMultipleTypes()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Transactions.AddRange(
            // Account 1 transactions
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = Guid.NewGuid(), Amount = 200, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fixed", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = Guid.NewGuid(), Amount = 300, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Future", CreatedAt = now },
            // Account 2 transactions (should not be counted)
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId2, UserId = Guid.NewGuid(), Amount = 9999, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId1, now.AddDays(-1), now.AddDays(1));

        // Assert
        result.Fun.Should().Be(100);
        result.Fixed.Should().Be(200);
        result.Future.Should().Be(300);
        result.Total.Should().Be(600);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void BudgetAggregationService_CanBeConstructed()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TransactionDbContext(options);

        // Act
        var service = new BudgetAggregationService(db);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion
}
