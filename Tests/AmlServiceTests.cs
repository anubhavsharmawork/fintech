using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TransactionService.Configuration;
using TransactionService.Data;
using TransactionService.Models;
using TransactionService.Services;

namespace Tests;

public class AmlServiceTests
{
    private (AmlService Service, TransactionDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildService(FraudDetectionSettings? settings = null)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new TransactionDbContext(options);
        var publisher = new Mock<IPublishEndpoint>();
        var logger = new Mock<ILogger<AmlService>>();
        var opts = Options.Create(settings ?? new FraudDetectionSettings());
        var service = new AmlService(opts, db, publisher.Object, logger.Object);
        return (service, db, publisher);
    }

    private Transaction MakeTx(
        Guid? userId = null,
        decimal amount = 100m,
        string type = "credit",
        string description = "normal payment",
        string clientType = "Individual",
        Guid? orgId = null) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        Amount = amount,
        Currency = "NZD",
        Type = type,
        Description = description,
        ClientType = clientType,
        OrganisationId = orgId,
        CreatedAt = DateTime.UtcNow
    };

    // ── Rule 1: Large transaction ──────────────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_Rule1_FlagsHigh_WhenAmountExceedsThreshold()
    {
        var (service, db, publisher) = BuildService();
        var tx = MakeTx(amount: 10_001m);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeTrue();
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("High");
        sar.Reason.Should().Contain("Large transaction");
    }

    [Fact]
    public async Task ScreenTransaction_Rule1_DoesNotFlag_WhenAmountEqualsThreshold()
    {
        var (service, _, _) = BuildService();
        var tx = MakeTx(amount: 10_000m); // exactly at threshold — NOT exceeded

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    [Fact]
    public async Task ScreenTransaction_Rule1_DoesNotFlag_WhenAmountIsBelowThreshold()
    {
        var (service, _, _) = BuildService();
        var tx = MakeTx(amount: 500m);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    // ── Rule 2: Rapid successive debits ───────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_Rule2_FlagsMedium_WhenRapidDebitThresholdReached()
    {
        var userId = Guid.NewGuid();
        var (service, db, _) = BuildService();

        // Seed 3 recent transactions for the same user within the window
        for (var i = 0; i < 3; i++)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                UserId = userId,
                Amount = 500m,
                Currency = "NZD",
                Type = "debit",
                Description = "payment",
                ClientType = "Individual",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        }
        await db.SaveChangesAsync();

        var tx = MakeTx(userId: userId, amount: 3_001m, type: "debit");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeTrue();
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("Medium");
        sar.Reason.Should().Contain("Rapid successive");
    }

    [Fact]
    public async Task ScreenTransaction_Rule2_DoesNotFlag_WhenBelowRapidCount()
    {
        var userId = Guid.NewGuid();
        var (service, db, _) = BuildService();

        // Only 2 recent transactions — below threshold of 3
        for (var i = 0; i < 2; i++)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                UserId = userId,
                Amount = 100m,
                Currency = "NZD",
                Type = "debit",
                Description = "payment",
                ClientType = "Individual",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        }
        await db.SaveChangesAsync();

        var tx = MakeTx(userId: userId, amount: 3_001m, type: "debit");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    [Fact]
    public async Task ScreenTransaction_Rule2_DoesNotFlag_ForCreditTransactions()
    {
        var userId = Guid.NewGuid();
        var (service, db, _) = BuildService();

        for (var i = 0; i < 5; i++)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                UserId = userId,
                Amount = 500m,
                Currency = "NZD",
                Type = "credit",
                Description = "income",
                ClientType = "Individual",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
        }
        await db.SaveChangesAsync();

        var tx = MakeTx(userId: userId, amount: 3_001m, type: "credit"); // credit, not debit

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    // ── Rule 3: Flagged keywords ───────────────────────────────────────────

    [Theory]
    [InlineData("casino", "casino withdrawal")]
    [InlineData("crypto", "crypto exchange transfer")]
    [InlineData("anonymous", "anonymous payment")]
    [InlineData("offshore", "offshore account transfer")]
    public async Task ScreenTransaction_Rule3_FlagsLow_WhenDescriptionContainsKeyword(string keyword, string description)
    {
        var (service, db, _) = BuildService();
        var tx = MakeTx(description: description);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeTrue();
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("Low");
        sar.Reason.Should().Contain("keyword");
        _ = keyword; // used via description
    }

    [Fact]
    public async Task ScreenTransaction_Rule3_IsCaseInsensitive()
    {
        var (service, db, _) = BuildService();
        var tx = MakeTx(description: "CASINO bonus payment");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeTrue();
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("Low");
    }

    [Fact]
    public async Task ScreenTransaction_Rule3_DoesNotFlag_WhenDescriptionIsClean()
    {
        var (service, _, _) = BuildService();
        var tx = MakeTx(description: "monthly rent payment");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    [Fact]
    public async Task ScreenTransaction_Rule3_DoesNotFlag_WhenDescriptionIsEmpty()
    {
        var (service, _, _) = BuildService();
        var tx = MakeTx(description: "");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    // ── Rule 4: Corporate bulk volume ──────────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_Rule4_FlagsHigh_WhenCorporateBulkVolumeExceeded()
    {
        var orgId = Guid.NewGuid();
        var (service, db, _) = BuildService();

        // Seed 24h corporate transactions totalling > 100,000 (threshold * 10)
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100_001m,
            Currency = "NZD",
            Type = "debit",
            Description = "batch payment",
            ClientType = "Corporate",
            OrganisationId = orgId,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var tx = MakeTx(amount: 500m, clientType: "Corporate", orgId: orgId);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeTrue();
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("High");
        sar.Reason.Should().Contain("corporate");
    }

    [Fact]
    public async Task ScreenTransaction_Rule4_DoesNotFlag_ForIndividualClientType()
    {
        var orgId = Guid.NewGuid();
        var (service, db, _) = BuildService();

        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 200_000m,
            Currency = "NZD",
            Type = "debit",
            Description = "payment",
            ClientType = "Individual",
            OrganisationId = orgId,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var tx = MakeTx(amount: 500m, clientType: "Individual", orgId: orgId);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    [Fact]
    public async Task ScreenTransaction_Rule4_DoesNotFlag_WhenOrgIdIsNull()
    {
        var (service, _, _) = BuildService();
        var tx = MakeTx(amount: 500m, clientType: "Corporate", orgId: null);

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
    }

    // ── Clean transaction ──────────────────────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_ReturnsFalse_ForCleanTransaction()
    {
        var (service, db, publisher) = BuildService();
        var tx = MakeTx(amount: 50m, description: "coffee");

        var flagged = await service.ScreenTransactionAsync(tx);

        flagged.Should().BeFalse();
        db.SuspiciousActivityReports.Should().BeEmpty();
        publisher.Verify(p => p.Publish(It.IsAny<SuspiciousActivityFlagged>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Publishes event ────────────────────────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_PublishesSuspiciousActivityFlagged_WhenFlagged()
    {
        var (service, _, publisher) = BuildService();
        var tx = MakeTx(amount: 50_000m);

        await service.ScreenTransactionAsync(tx);

        publisher.Verify(p => p.Publish(
            It.IsAny<SuspiciousActivityFlagged>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScreenTransaction_CreatesSarRecord_InDatabase()
    {
        var (service, db, _) = BuildService();
        var tx = MakeTx(amount: 50_000m);

        await service.ScreenTransactionAsync(tx);

        db.SuspiciousActivityReports.Should().HaveCount(1);
        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.TransactionId.Should().Be(tx.Id);
        sar.UserId.Should().Be(tx.UserId);
        sar.Status.Should().Be("Open");
    }

    // ── Rule 1 takes priority over Rule 3 ─────────────────────────────────

    [Fact]
    public async Task ScreenTransaction_Rule1_TakesPriority_OverKeywordRule()
    {
        var (service, db, _) = BuildService();
        var tx = MakeTx(amount: 50_000m, description: "casino payment");

        await service.ScreenTransactionAsync(tx);

        var sar = await db.SuspiciousActivityReports.FirstAsync();
        sar.RiskLevel.Should().Be("High"); // Rule 1, not Low from Rule 3
    }

    // ── GetReportByTransactionAsync ────────────────────────────────────────

    [Fact]
    public async Task GetReportByTransactionAsync_ReturnsNull_WhenNoReportExists()
    {
        var (service, _, _) = BuildService();

        var result = await service.GetReportByTransactionAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReportByTransactionAsync_ReturnsReport_WhenExists()
    {
        var (service, db, _) = BuildService();
        var transactionId = Guid.NewGuid();
        var sar = new SuspiciousActivityReport
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            UserId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "NZD",
            Reason = "Test",
            RiskLevel = "Low",
            Status = "Open",
            FlaggedAt = DateTime.UtcNow
        };
        db.SuspiciousActivityReports.Add(sar);
        await db.SaveChangesAsync();

        var result = await service.GetReportByTransactionAsync(transactionId);

        result.Should().NotBeNull();
        result!.TransactionId.Should().Be(transactionId);
    }
}
