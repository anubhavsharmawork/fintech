using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace Tests;

public class TransactionServiceCoverageTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static TransactionDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (TransactionService.Services.TransactionService Svc, TransactionDbContext Db,
        Mock<IPublishEndpoint> Publisher, Mock<ICacheService> Cache)
        BuildService()
    {
        var db = BuildDb();
        var pub = new Mock<IPublishEndpoint>();
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<TransactionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TransactionDto>?)null);
        var aml = new Mock<IAmlScreeningChannel>();
        aml.Setup(a => a.TryEnqueue(It.IsAny<Transaction>())).Returns(true);

        var svc = new TransactionService.Services.TransactionService(
            db,
            pub.Object,
            aml.Object,
            cache.Object,
            new Mock<ILogger<TransactionService.Services.TransactionService>>().Object);

        return (svc, db, pub, cache);
    }

    private static TransactionsController BuildController(
        TransactionDbContext db,
        Mock<IPublishEndpoint>? pub = null,
        Mock<ICacheService>? cache = null,
        Guid? userId = null,
        Guid? orgId = null)
    {
        pub ??= new Mock<IPublishEndpoint>();
        cache ??= new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<TransactionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TransactionDto>?)null);
        var aml = new Mock<IAmlScreeningChannel>();
        aml.Setup(a => a.TryEnqueue(It.IsAny<Transaction>())).Returns(true);

        var svc = new TransactionService.Services.TransactionService(
            db, pub.Object, aml.Object, cache.Object,
            new Mock<ILogger<TransactionService.Services.TransactionService>>().Object);

        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim("sub", userId.Value.ToString()));
        }
        if (orgId.HasValue)
            claims.Add(new Claim("organisation_id", orgId.Value.ToString()));

        var ctrl = new TransactionsController(svc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return ctrl;
    }

    private static Transaction SeedTransaction(TransactionDbContext db, Guid userId,
        Guid? orgId = null, string type = "debit", decimal amount = 100m,
        string description = "test", DateTime? createdAt = null) 
    {
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Currency = "NZD",
            Type = type,
            Description = description,
            SpendingType = "Fun",
            ClientType = orgId.HasValue ? "Corporate" : "Individual",
            OrganisationId = orgId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            Status = "Completed"
        };
        db.Transactions.Add(tx);
        db.SaveChanges();
        return tx;
    }

    // ── GetTransactionsAsync – filter branches ─────────────────────────────

    [Fact]
    public async Task GetTransactions_FiltersByAccountId()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var tx1 = SeedTransaction(db, userId);
        var tx2 = new Transaction
        {
            Id = Guid.NewGuid(), AccountId = accountId, UserId = userId,
            Amount = 50m, Currency = "NZD", Type = "credit", Description = "specific",
            SpendingType = "Fun", ClientType = "Individual", CreatedAt = DateTime.UtcNow,
            Status = "Completed"
        };
        db.Transactions.Add(tx2);
        await db.SaveChangesAsync();

        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, accountId: accountId, null, 1, 50);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByBatchId()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        SeedTransaction(db, userId);
        var batchTx = new Transaction
        {
            Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), UserId = userId,
            Amount = 200m, Currency = "NZD", Type = "debit", Description = "batch",
            SpendingType = "Fun", ClientType = "Individual", PaymentBatchId = batchId,
            CreatedAt = DateTime.UtcNow, Status = "Completed"
        };
        db.Transactions.Add(batchTx);
        await db.SaveChangesAsync();

        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, batchId: batchId, 1, 50);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByFromDate()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();
        var cutoff = DateTime.UtcNow.Date;

        SeedTransaction(db, userId, createdAt: cutoff.AddDays(-2));
        SeedTransaction(db, userId, createdAt: cutoff.AddDays(1));

        var filter = new TransactionFilterDto { FromDate = cutoff };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByToDate()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();
        var cutoff = DateTime.UtcNow.Date;

        SeedTransaction(db, userId, createdAt: cutoff.AddDays(-1));
        SeedTransaction(db, userId, createdAt: cutoff.AddDays(2));

        var filter = new TransactionFilterDto { ToDate = cutoff };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByTransactionType()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();

        SeedTransaction(db, userId, type: "debit");
        SeedTransaction(db, userId, type: "credit");

        var filter = new TransactionFilterDto { TransactionType = "credit" };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByMinAmount()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();

        SeedTransaction(db, userId, amount: 10m);
        SeedTransaction(db, userId, amount: 500m);

        var filter = new TransactionFilterDto { MinAmount = 100m };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersByMaxAmount()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();

        SeedTransaction(db, userId, amount: 10m);
        SeedTransaction(db, userId, amount: 500m);

        var filter = new TransactionFilterDto { MaxAmount = 100m };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_FiltersBySearchTerm()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();

        SeedTransaction(db, userId, description: "Salary payment");
        SeedTransaction(db, userId, description: "Grocery shopping");

        var filter = new TransactionFilterDto { SearchTerm = "salary" };
        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 50, filter);

        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTransactions_PaginatesResults()
    {
        var (svc, db, _, _) = BuildService();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
            SeedTransaction(db, userId);

        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, null, null, 1, 3);

        result.Data.Should().HaveCount(3);
        result.TotalFilteredCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public async Task GetTransactions_ReturnsCachedResult_WhenCacheHit()
    {
        var db = BuildDb();
        var cache = new Mock<ICacheService>();
        var cachedList = new List<TransactionDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 42m, "NZD", "debit", "cached", "Fun",
                null, DateTime.UtcNow, "Individual", null, null, "Completed")
        };
        cache.Setup(c => c.GetAsync<List<TransactionDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedList);

        var aml = new Mock<IAmlScreeningChannel>();
        var svc = new TransactionService.Services.TransactionService(
            db,
            new Mock<IPublishEndpoint>().Object,
            aml.Object,
            cache.Object,
            new Mock<ILogger<TransactionService.Services.TransactionService>>().Object);

        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(Guid.NewGuid(), null, null, 1, 50);

        result.Data.Should().HaveCount(1);
        result.TotalFilteredCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTransactions_DoesNotCache_WhenAccountIdFilterApplied()
    {
        var db = BuildDb();
        var cache = new Mock<ICacheService>();
        var cachedList = new List<TransactionDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 42m, "NZD", "debit", "should-not-see", "Fun",
                null, DateTime.UtcNow, "Individual", null, null, "Completed")
        };
        // Cache returns data but it should be bypassed when accountId is supplied
        cache.Setup(c => c.GetAsync<List<TransactionDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedList);

        var aml = new Mock<IAmlScreeningChannel>();
        var svc = new TransactionService.Services.TransactionService(
            db,
            new Mock<IPublishEndpoint>().Object,
            aml.Object,
            cache.Object,
            new Mock<ILogger<TransactionService.Services.TransactionService>>().Object);

        var userId = Guid.NewGuid();
        SeedTransaction(db, userId);

        var result = (TransactionPagedResponse<object>)
            await svc.GetTransactionsAsync(userId, accountId: Guid.NewGuid(), null, 1, 50);

        // Cache not used because accountId filter was provided - must query DB
        cache.Verify(c => c.GetAsync<List<TransactionDto>>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateTransaction_Throws_WhenSpendingTypeInvalid()
    {
        var (svc, _, _, _) = BuildService();

        var act = async () => await svc.CreateTransactionAsync(
            Guid.NewGuid(), "Individual", null,
            new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(), Amount = 100m, Currency = "NZD",
                Type = "debit", Description = "test", SpendingType = "InvalidType"
            });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid spendingType*");
    }

    [Fact]
    public async Task CreateTransaction_CorporateClientType_SetsCorrectEventEnum()
    {
        var (svc, db, pub, _) = BuildService();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await svc.CreateTransactionAsync(
            userId, "Corporate", orgId,
            new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(), Amount = 100m, Currency = "NZD",
                Type = "debit", Description = "corp tx", SpendingType = "Fixed"
            });

        pub.Verify(p => p.Publish(
            It.Is<TransactionCreated>(e => e.ClientType == ClientType.Corporate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Controller-level: GetTransactions with filters ─────────────────────

    [Fact]
    public async Task Controller_GetTransactions_ReturnsOk_WithAllFilters()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        SeedTransaction(db, userId, amount: 150m, description: "test merchant");
        var ctrl = BuildController(db, userId: userId);

        var result = await ctrl.GetTransactions(
            accountId: null, batchId: null,
            page: 1, pageSize: 50,
            fromDate: DateTime.UtcNow.AddDays(-1), toDate: DateTime.UtcNow.AddDays(1),
            transactionType: null, minAmount: 100m, maxAmount: 200m,
            searchTerm: "merchant");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Controller_GetTransactions_ReturnsBadRequest_WhenFromDateAfterToDate()
    {
        var db = BuildDb();
        var ctrl = BuildController(db, userId: Guid.NewGuid());

        var result = await ctrl.GetTransactions(
            accountId: null, batchId: null,
            page: 1, pageSize: 50,
            fromDate: DateTime.UtcNow.AddDays(1),
            toDate: DateTime.UtcNow.AddDays(-1),
            transactionType: null, minAmount: null, maxAmount: null, searchTerm: null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
