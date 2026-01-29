using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Services;

namespace Tests;

/// <summary>
/// Comprehensive tests for BudgetAggregationService
/// Covers budget calculations, date filtering, spending type aggregation
/// </summary>
public class BudgetAggregationServiceComprehensiveTests
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

    #region Basic Aggregation Tests

    [Fact]
    public async Task GetBudgetAsync_WithEmptyAccount_ReturnsZeros()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddMonths(-1);
        var to = DateTime.UtcNow;

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(0);
        result.Fixed.Should().Be(0);
        result.Future.Should().Be(0);
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetBudgetAsync_AggregatesBySpendingType()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1); // Ensure transactions are within range

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 200, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fixed", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 50, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Future", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(100);
        result.Fixed.Should().Be(200);
        result.Future.Should().Be(50);
        result.Total.Should().Be(350);
    }

    [Fact]
    public async Task GetBudgetAsync_AccumulatesSameSpendingType()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1);

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 50, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 75, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 25, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(150);
        result.Total.Should().Be(150);
    }

    #endregion

    #region Date Filtering Tests

    [Fact]
    public async Task GetBudgetAsync_FiltersTransactionsByDateRange()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddDays(-5);
        var to = now.AddDays(-2);

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now.AddDays(-10) }, // Before range
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 200, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now.AddDays(-3) }, // Within range
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 50, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now } // After range
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(200);
    }

    [Fact]
    public async Task GetBudgetAsync_IncludesFromAndToDate()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-5);
        var to = DateTime.UtcNow;

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = from },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 200, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = to }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(300);
    }

    [Fact]
    public async Task GetBudgetAsync_ExcludesAfterToDate()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-5);
        var to = DateTime.UtcNow;
        var after = to.AddSeconds(1);

        db.Transactions.Add(new Transaction 
        { 
            Id = Guid.NewGuid(), 
            AccountId = accountId, 
            UserId = Guid.NewGuid(), 
            Amount = 100, 
            Currency = "USD", 
            Type = "debit", 
            Description = "Test",
            SpendingType = "Fun", 
            CreatedAt = after 
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(0);
    }

    #endregion

    #region Account Isolation Tests

    [Fact]
    public async Task GetBudgetAsync_IsolatesTransactionsByAccount()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1);

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = Guid.NewGuid(), Amount = 100, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId2, UserId = Guid.NewGuid(), Amount = 9999, Currency = "USD", Type = "debit", Description = "Test", SpendingType = "Fun", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId1, from, to);

        // Assert
        result.Fun.Should().Be(100);
    }

    #endregion

    #region Spending Type Case Handling Tests

    [Theory]
    [InlineData("fun")]
    [InlineData("Fun")]
    [InlineData("FUN")]
    public async Task GetBudgetAsync_HandlesCaseInsensitiveSpendingType_Fun(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1);

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
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(100);
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("Fixed")]
    [InlineData("FIXED")]
    public async Task GetBudgetAsync_HandlesCaseInsensitiveSpendingType_Fixed(string spendingType)
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1);

        db.Transactions.Add(new Transaction 
        { 
            Id = Guid.NewGuid(), 
            AccountId = accountId, 
            UserId = Guid.NewGuid(), 
            Amount = 200, 
            Currency = "USD", 
            Type = "debit", 
            Description = "Test",
            SpendingType = spendingType, 
            CreatedAt = now 
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fixed.Should().Be(200);
    }

    #endregion

    #region Invalid/Null Spending Type Tests

    [Fact]
    public async Task GetBudgetAsync_IgnoresNullSpendingType()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddMonths(-1);
        var to = DateTime.UtcNow;

        db.Transactions.Add(new Transaction 
        { 
            Id = Guid.NewGuid(), 
            AccountId = accountId, 
            UserId = Guid.NewGuid(), 
            Amount = 100, 
            Currency = "USD", 
            Type = "debit", 
            Description = "Test",
            SpendingType = null, 
            CreatedAt = DateTime.UtcNow 
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetBudgetAsync_IgnoresWhitespaceSpendingType()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddMonths(-1);
        var to = DateTime.UtcNow;

        db.Transactions.Add(new Transaction 
        { 
            Id = Guid.NewGuid(), 
            AccountId = accountId, 
            UserId = Guid.NewGuid(), 
            Amount = 100, 
            Currency = "USD", 
            Type = "debit", 
            Description = "Test",
            SpendingType = "   ", 
            CreatedAt = DateTime.UtcNow 
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetBudgetAsync_IgnoresInvalidSpendingType()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddMonths(-1);
        var to = DateTime.UtcNow;

        db.Transactions.Add(new Transaction 
        { 
            Id = Guid.NewGuid(), 
            AccountId = accountId, 
            UserId = Guid.NewGuid(), 
            Amount = 100, 
            Currency = "USD", 
            Type = "debit", 
            Description = "Test",
            SpendingType = "Invalid", 
            CreatedAt = DateTime.UtcNow 
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Total.Should().Be(0);
    }

    #endregion

    #region Period Metadata Tests

    [Fact]
    public async Task GetBudgetAsync_ReturnsPeriodMetadata()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Period.Should().NotBeNull();
        result.Period!.From.Should().Contain("2024-01-01");
        result.Period.To.Should().Contain("2024-01-31");
    }

    [Fact]
    public async Task GetBudgetAsync_PeriodUsesIso8601Format()
    {
        // Arrange
        var (service, _) = BuildService();
        var accountId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow;

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        // ISO 8601 format includes 'T' and 'Z' or offset
        result.Period!.From.Should().Contain("T");
        result.Period!.To.Should().Contain("T");
    }

    #endregion

    #region Complex Scenarios Tests

    [Fact]
    public async Task GetBudgetAsync_MixedTransactionsWithComplexData()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddDays(-30);
        var to = now;

        // Create a realistic budget scenario
        db.Transactions.AddRange(
            // Week 1 - Fun activities
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 15.50m, Currency = "USD", Type = "debit", Description = "Movie tickets", SpendingType = "Fun", CreatedAt = now.AddDays(-25) },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 45.00m, Currency = "USD", Type = "debit", Description = "Concert", SpendingType = "Fun", CreatedAt = now.AddDays(-23) },
            // Fixed expenses
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 1200m, Currency = "USD", Type = "debit", Description = "Rent", SpendingType = "Fixed", CreatedAt = now.AddDays(-20) },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 150m, Currency = "USD", Type = "debit", Description = "Utilities", SpendingType = "Fixed", CreatedAt = now.AddDays(-20) },
            // Future savings
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 500m, Currency = "USD", Type = "credit", Description = "Savings", SpendingType = "Future", CreatedAt = now.AddDays(-15) },
            // Invalid entries that should be ignored
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100m, Currency = "USD", Type = "debit", Description = "Invalid", SpendingType = null, CreatedAt = now.AddDays(-10) }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(60.50m);
        result.Fixed.Should().Be(1350m);
        result.Future.Should().Be(500m);
        result.Total.Should().Be(1910.50m);
    }

    [Fact]
    public async Task GetBudgetAsync_HandlesLargeAmounts()
    {
        // Arrange
        var (service, db) = BuildService();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var from = now.AddMonths(-1);
        var to = now.AddDays(1);

        db.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 999999.99m, Currency = "USD", Type = "debit", Description = "Large purchase", SpendingType = "Fun", CreatedAt = now },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 1000000m, Currency = "USD", Type = "debit", Description = "Investment", SpendingType = "Fixed", CreatedAt = now }
        );
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetBudgetAsync(accountId, from, to);

        // Assert
        result.Fun.Should().Be(999999.99m);
        result.Fixed.Should().Be(1000000m);
        result.Total.Should().Be(1999999.99m);
    }

    #endregion
}
