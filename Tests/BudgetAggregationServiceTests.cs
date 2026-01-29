using Microsoft.EntityFrameworkCore;
using TransactionService.Data;
using TransactionService.Services;
using Xunit;

namespace Tests;

public class BudgetAggregationServiceTests
{
    private static BudgetAggregationService BuildService(out TransactionDbContext context)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        context = new TransactionDbContext(options);
        return new BudgetAggregationService(context);
    }

    [Fact]
    public async Task Aggregates_BySpendingType_Correctly()
    {
        var service = BuildService(out var context);
        var accountId = Guid.NewGuid();
        var baseDate = DateTime.UtcNow;
        var ct = CancellationToken.None;

        context.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Type = "debit", Currency = "USD", Description = "Fun", SpendingType = "Fun", CreatedAt = baseDate },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 200, Type = "debit", Currency = "USD", Description = "Fixed", SpendingType = "Fixed", CreatedAt = baseDate },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 300, Type = "debit", Currency = "USD", Description = "Future", SpendingType = "Future", CreatedAt = baseDate },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 50, Type = "debit", Currency = "USD", Description = "Invalid", SpendingType = "Other", CreatedAt = baseDate }
        );
        await context.SaveChangesAsync(ct);

        var result = await service.GetBudgetAsync(accountId, baseDate.AddDays(-1), baseDate.AddDays(1), ct);

        Assert.Equal(100, result.Fun);
        Assert.Equal(200, result.Fixed);
        Assert.Equal(300, result.Future);
        Assert.Equal(600, result.Total);
    }

    [Fact]
    public async Task Filters_ByDateRange()
    {
        var service = BuildService(out var context);
        var accountId = Guid.NewGuid();
        var baseDate = DateTime.UtcNow;
        var ct = CancellationToken.None;

        context.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Type = "debit", Currency = "USD", Description = "Fun", SpendingType = "Fun", CreatedAt = baseDate.AddDays(-2) },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 150, Type = "debit", Currency = "USD", Description = "Fun", SpendingType = "Fun", CreatedAt = baseDate }
        );
        await context.SaveChangesAsync(ct);

        var result = await service.GetBudgetAsync(accountId, baseDate.AddDays(-1), baseDate.AddDays(1), ct);

        Assert.Equal(150, result.Fun);
        Assert.Equal(150, result.Total);
    }

    [Fact]
    public async Task Ignores_NullOrInvalid_SpendingTypes()
    {
        var service = BuildService(out var context);
        var accountId = Guid.NewGuid();
        var baseDate = DateTime.UtcNow;
        var ct = CancellationToken.None;

        context.Transactions.AddRange(
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 100, Type = "debit", Currency = "USD", Description = "None", SpendingType = null, CreatedAt = baseDate },
            new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = Guid.NewGuid(), Amount = 50, Type = "debit", Currency = "USD", Description = "Bad", SpendingType = "Unknown", CreatedAt = baseDate }
        );
        await context.SaveChangesAsync(ct);

        var result = await service.GetBudgetAsync(accountId, baseDate.AddDays(-1), baseDate.AddDays(1), ct);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Fun);
        Assert.Equal(0, result.Fixed);
        Assert.Equal(0, result.Future);
    }

    [Fact]
    public async Task ReturnsZero_ForEmptySet()
    {
        var service = BuildService(out var context);
        var ct = CancellationToken.None;
        var result = await service.GetBudgetAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), ct);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Fun);
        Assert.Equal(0, result.Fixed);
        Assert.Equal(0, result.Future);
    }
}
