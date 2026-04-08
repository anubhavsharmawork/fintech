using System.Security.Claims;
using Contracts;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace Tests;

public class TransactionsControllerFiltersTests
{
    private (TransactionsController Controller, TransactionDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildController(
            Guid? userId = null,
            Guid? orgId = null,
            string? clientType = "Individual")
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new TransactionDbContext(options);
        var publisher = new Mock<IPublishEndpoint>();
        var amlChannel = new Mock<IAmlScreeningChannel>();
        var cacheService = new Mock<ICacheService>();
        var serviceLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
        amlChannel.Setup(a => a.TryEnqueue(It.IsAny<Transaction>())).Returns(true);

        var service = new TransactionService.Services.TransactionService(
            db, publisher.Object, amlChannel.Object, cacheService.Object, serviceLogger.Object);

        var controller = new TransactionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim("sub", userId.Value.ToString()));
        if (orgId.HasValue)
            claims.Add(new Claim("organisation_id", orgId.Value.ToString()));
        if (!string.IsNullOrEmpty(clientType))
            claims.Add(new Claim("client_type", clientType));

        if (claims.Count > 0)
        {
            var identity = new ClaimsIdentity(claims, "Test");
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
        }

        return (controller, db, publisher);
    }

    // ── GetTransactions date range validation ──────────────────────────────

    [Fact]
    public async Task GetTransactions_ReturnsBadRequest_WhenFromDateIsAfterToDate()
    {
        var (controller, _, _) = BuildController(userId: Guid.NewGuid());

        var result = await controller.GetTransactions(
            fromDate: DateTime.UtcNow,
            toDate: DateTime.UtcNow.AddDays(-1));

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = bad.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Detail.Should().Contain("fromDate");
    }

    [Fact]
    public async Task GetTransactions_ReturnsOk_WhenFromDateEqualsToDate()
    {
        var now = DateTime.UtcNow;
        var (controller, _, _) = BuildController(userId: Guid.NewGuid());

        var result = await controller.GetTransactions(
            fromDate: now,
            toDate: now);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetTransactions amount range validation ────────────────────────────

    [Fact]
    public async Task GetTransactions_ReturnsBadRequest_WhenMinAmountIsGreaterThanMaxAmount()
    {
        var (controller, _, _) = BuildController(userId: Guid.NewGuid());

        var result = await controller.GetTransactions(
            minAmount: 500m,
            maxAmount: 100m);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = bad.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Detail.Should().Contain("minAmount");
    }

    [Fact]
    public async Task GetTransactions_ReturnsOk_WhenMinAmountEqualsMaxAmount()
    {
        var (controller, _, _) = BuildController(userId: Guid.NewGuid());

        var result = await controller.GetTransactions(
            minAmount: 100m,
            maxAmount: 100m);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetTransactions auth ───────────────────────────────────────────────

    [Fact]
    public async Task GetTransactions_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildController();

        var result = await controller.GetTransactions();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetOrganisationTransactions ────────────────────────────────────────

    [Fact]
    public async Task GetOrganisationTransactions_ReturnsUnauthorized_WhenNoOrgClaimPresent()
    {
        var (controller, _, _) = BuildController(userId: Guid.NewGuid()); // no orgId claim

        var result = await controller.GetOrganisationTransactions(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetOrganisationTransactions_ReturnsForbid_WhenOrgIdMismatch()
    {
        var claimedOrgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();
        var (controller, _, _) = BuildController(userId: Guid.NewGuid(), orgId: claimedOrgId);

        var result = await controller.GetOrganisationTransactions(differentOrgId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetOrganisationTransactions_ReturnsOk_WhenOrgIdMatches()
    {
        var orgId = Guid.NewGuid();
        var (controller, _, _) = BuildController(userId: Guid.NewGuid(), orgId: orgId);

        var result = await controller.GetOrganisationTransactions(orgId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrganisationTransactions_ReturnsOnlyMatchingOrgTransactions()
    {
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var (controller, db, _) = BuildController(userId: Guid.NewGuid(), orgId: orgId);

        db.Transactions.AddRange(
            new Transaction
            {
                Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), UserId = Guid.NewGuid(),
                Amount = 100m, Currency = "NZD", Type = "credit", Description = "org tx",
                ClientType = "Corporate", OrganisationId = orgId, CreatedAt = DateTime.UtcNow
            },
            new Transaction
            {
                Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), UserId = Guid.NewGuid(),
                Amount = 200m, Currency = "NZD", Type = "credit", Description = "other org tx",
                ClientType = "Corporate", OrganisationId = otherOrgId, CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var result = await controller.GetOrganisationTransactions(orgId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    // ── CreateTransaction publishes event ─────────────────────────────────

    [Fact]
    public async Task CreateTransaction_PublishesTransactionCreatedEvent()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var (controller, _, publisher) = BuildController(userId: userId);

        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 50m,
            Type = "credit",
            Currency = "NZD",
            Description = "test payment"
        };

        await controller.CreateTransaction(request);

        publisher.Verify(p => p.Publish(
            It.IsAny<TransactionCreated>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CreateTransaction creates double-entry ledger entries ──────────────

    [Fact]
    public async Task CreateTransaction_CreatesTwoLedgerEntries_DebitAndCredit()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var (controller, db, _) = BuildController(userId: userId);

        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 75m,
            Type = "debit",
            Currency = "NZD",
            Description = "double entry test"
        };

        await controller.CreateTransaction(request);

        db.LedgerEntries.Should().HaveCount(2);
        db.LedgerEntries.Should().Contain(e => e.EntryType == "debit");
        db.LedgerEntries.Should().Contain(e => e.EntryType == "credit");
    }

    [Fact]
    public async Task CreateTransaction_LedgerEntries_HaveMatchingAmounts()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var (controller, db, _) = BuildController(userId: userId);

        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 123.45m,
            Type = "credit",
            Currency = "NZD",
            Description = "amount check"
        };

        await controller.CreateTransaction(request);

        var entries = db.LedgerEntries.ToList();
        entries.Should().AllSatisfy(e => e.Amount.Should().Be(123.45m));
    }
}
