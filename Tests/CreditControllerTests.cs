using System.Security.Claims;
using ApiGateway.Controllers;
using ApiGateway.Data;
using ApiGateway.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class CreditControllerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private (CreditController Controller, CreditDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildController(Guid? claimUserId = null)
    {
        var options = new DbContextOptionsBuilder<CreditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CreditDbContext(options);
        var publisher = new Mock<IPublishEndpoint>();
        var logger = new Mock<ILogger<CreditController>>();

        var controller = new CreditController(db, publisher.Object, logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (claimUserId.HasValue)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim("sub", claimUserId.Value.ToString()) }, "Test");
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
        }

        return (controller, db, publisher);
    }

    private CreditFacility MakeFacility(Guid userId, string walletAddress = "0xwallet",
        decimal creditLimit = 10_000m, decimal drawn = 0m, decimal outstanding = 0m,
        CreditFacilityStatus status = CreditFacilityStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        WalletAddress = walletAddress,
        CreditLimit = creditLimit,
        DrawnAmount = drawn,
        OutstandingBalance = outstanding,
        Currency = "FTK",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    // ── GetFacility ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFacility_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetFacility("0xwallet", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetFacility_ReturnsBadRequest_WhenWalletAddressIsEmpty()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.GetFacility("", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFacility_ReturnsBadRequest_WhenWalletAddressIsWhitespace()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.GetFacility("   ", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFacility_CreatesNewFacility_WhenNoneExists()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);

        var result = await controller.GetFacility("0xabc", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        db.CreditFacilities.Should().HaveCount(1);
        var facility = await db.CreditFacilities.FirstAsync();
        facility.UserId.Should().Be(userId);
        facility.CreditLimit.Should().Be(10_000m);
        facility.Currency.Should().Be("FTK");
        facility.Status.Should().Be(CreditFacilityStatus.Active);
    }

    [Fact]
    public async Task GetFacility_ReturnsExistingFacility_WhenAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        var existing = MakeFacility(userId, walletAddress: "0xexisting", drawn: 500m);
        db.CreditFacilities.Add(existing);
        await db.SaveChangesAsync();

        var result = await controller.GetFacility("0xexisting", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        db.CreditFacilities.Should().HaveCount(1); // no new facility created
    }

    [Fact]
    public async Task GetFacility_IsWalletAddressCaseInsensitive()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        var existing = MakeFacility(userId, walletAddress: "0xabc");
        db.CreditFacilities.Add(existing);
        await db.SaveChangesAsync();

        var result = await controller.GetFacility("0XABC", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        db.CreditFacilities.Should().HaveCount(1);
    }

    // ── Drawdown ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Drawdown_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", 100m), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsBadRequest_WhenAmountIsZero()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", 0m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsBadRequest_WhenAmountIsNegative()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", -50m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsBadRequest_WhenWalletAddressIsEmpty()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Drawdown(new DrawdownRequest("", 100m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsNotFound_WhenFacilityDoesNotExist()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Drawdown(new DrawdownRequest("0xnotfound", 100m), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsBadRequest_WhenFacilityIsNotActive()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, status: CreditFacilityStatus.Closed));
        await db.SaveChangesAsync();

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", 100m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Drawdown_ReturnsBadRequest_WhenAmountExceedsAvailableCredit()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, creditLimit: 1_000m, drawn: 900m));
        await db.SaveChangesAsync();

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", 200m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)result;
        bad.Value!.ToString().Should().Contain("credit");
    }

    [Fact]
    public async Task Drawdown_ReturnsOk_AndUpdatesFacility_WhenValid()
    {
        var userId = Guid.NewGuid();
        var (controller, db, publisher) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, creditLimit: 1_000m, drawn: 0m, outstanding: 0m));
        await db.SaveChangesAsync();

        var result = await controller.Drawdown(new DrawdownRequest("0xwallet", 400m), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var facility = await db.CreditFacilities.FirstAsync();
        facility.DrawnAmount.Should().Be(400m);
        facility.OutstandingBalance.Should().Be(400m);
    }

    [Fact]
    public async Task Drawdown_PublishesDrawdownRequested_WhenSuccessful()
    {
        var userId = Guid.NewGuid();
        var (controller, db, publisher) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId));
        await db.SaveChangesAsync();

        await controller.Drawdown(new DrawdownRequest("0xwallet", 100m), CancellationToken.None);

        publisher.Verify(p => p.Publish(
            It.IsAny<DrawdownRequested>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Repayment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Repayment_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.Repayment(new RepaymentRequest("0xwallet", 100m), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Repayment_ReturnsBadRequest_WhenAmountIsZero()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Repayment(new RepaymentRequest("0xwallet", 0m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Repayment_ReturnsBadRequest_WhenWalletIsEmpty()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Repayment(new RepaymentRequest("", 50m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Repayment_ReturnsNotFound_WhenFacilityDoesNotExist()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.Repayment(new RepaymentRequest("0xnotfound", 50m), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Repayment_ReturnsBadRequest_WhenAmountExceedsOutstandingBalance()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, drawn: 200m, outstanding: 200m));
        await db.SaveChangesAsync();

        var result = await controller.Repayment(new RepaymentRequest("0xwallet", 500m), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)result;
        bad.Value!.ToString().Should().Contain("outstanding balance");
    }

    [Fact]
    public async Task Repayment_ReturnsOk_AndReducesOutstandingBalance()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, drawn: 500m, outstanding: 500m));
        await db.SaveChangesAsync();

        var result = await controller.Repayment(new RepaymentRequest("0xwallet", 200m), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var facility = await db.CreditFacilities.FirstAsync();
        facility.OutstandingBalance.Should().Be(300m);
    }

    [Fact]
    public async Task Repayment_CreatesRepaymentRecord_InDatabase()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, drawn: 500m, outstanding: 500m));
        await db.SaveChangesAsync();

        await controller.Repayment(new RepaymentRequest("0xwallet", 100m), CancellationToken.None);

        db.CreditRepayments.Should().HaveCount(1);
        var repayment = await db.CreditRepayments.FirstAsync();
        repayment.Amount.Should().Be(100m);
        repayment.UserId.Should().Be(userId);
        repayment.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Repayment_PublishesRepaymentCompleted_WhenSuccessful()
    {
        var userId = Guid.NewGuid();
        var (controller, db, publisher) = BuildController(claimUserId: userId);
        db.CreditFacilities.Add(MakeFacility(userId, drawn: 500m, outstanding: 500m));
        await db.SaveChangesAsync();

        await controller.Repayment(new RepaymentRequest("0xwallet", 100m), CancellationToken.None);

        publisher.Verify(p => p.Publish(
            It.IsAny<RepaymentCompleted>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetRepayments ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRepayments_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetRepayments("0xwallet", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetRepayments_ReturnsBadRequest_WhenWalletIsEmpty()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.GetRepayments("", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRepayments_ReturnsEmptyArray_WhenNoFacilityExists()
    {
        var (controller, _, _) = BuildController(claimUserId: Guid.NewGuid());

        var result = await controller.GetRepayments("0xnotfound", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<object>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepayments_ReturnsRepaymentList_WhenFacilityExists()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildController(claimUserId: userId);
        var facility = MakeFacility(userId);
        db.CreditFacilities.Add(facility);
        db.CreditRepayments.Add(new CreditRepayment
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            UserId = userId,
            Amount = 100m,
            Currency = "FTK",
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.GetRepayments("0xwallet", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
        list.Should().HaveCount(1);
    }
}
