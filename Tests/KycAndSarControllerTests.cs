using System.Security.Claims;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models;
using TransactionService.Models.Dtos;
using UserService.Controllers;
using UserService.Data;

namespace Tests;

public class KycAndSarControllerTests
{
    // ── KycController helpers ──────────────────────────────────────────────

    private (KycController Controller, UserDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildKycController(Guid? claimUserId = null, string claimType = "sub")
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new UserDbContext(options);
        var publisher = new Mock<IPublishEndpoint>();
        var logger = new Mock<ILogger<KycController>>();

        var controller = new KycController(db, publisher.Object, logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (claimUserId.HasValue)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(claimType, claimUserId.Value.ToString()) }, "Test");
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
        }

        return (controller, db, publisher);
    }

    private User MakeUser(string kycStatus = "Verified") => new()
    {
        Id = Guid.NewGuid(),
        Email = $"user-{Guid.NewGuid()}@example.com",
        PasswordHash = "hash",
        FirstName = "Test",
        LastName = "User",
        KycStatus = kycStatus,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── KycController.GetKycStatus ─────────────────────────────────────────

    [Fact]
    public async Task GetKycStatus_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _, _) = BuildKycController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetKycStatus();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetKycStatus_ReturnsUnauthorized_WhenClaimIsNotAGuid()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new UserDbContext(options);
        var controller = new KycController(db, new Mock<IPublishEndpoint>().Object,
            new Mock<ILogger<KycController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "Test");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var result = await controller.GetKycStatus();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetKycStatus_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (controller, _, _) = BuildKycController(claimUserId: Guid.NewGuid());

        var result = await controller.GetKycStatus();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetKycStatus_ReturnsOk_WithStatus_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildKycController(claimUserId: userId);
        var user = MakeUser("Verified");
        user.Id = userId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.GetKycStatus();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!.ToString()!;
        body.Should().Contain("Verified");
    }

    [Fact]
    public async Task GetKycStatus_FindsUser_BySubClaim()
    {
        var userId = Guid.NewGuid();
        var (controller, db, _) = BuildKycController(claimUserId: userId, claimType: "sub");
        var user = MakeUser("Pending");
        user.Id = userId;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await controller.GetKycStatus();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── KycController.UpdateKycStatus ─────────────────────────────────────

    [Fact]
    public async Task UpdateKycStatus_ReturnsBadRequest_ForInvalidStatus()
    {
        var (controller, _, _) = BuildKycController(claimUserId: Guid.NewGuid());
        var dto = new UpdateKycStatusDto { UserId = Guid.NewGuid(), Status = "NotAStatus", Notes = "" };

        var result = await controller.UpdateKycStatus(dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateKycStatus_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var (controller, _, _) = BuildKycController(claimUserId: Guid.NewGuid());
        var dto = new UpdateKycStatusDto { UserId = Guid.NewGuid(), Status = "Verified", Notes = "" };

        var result = await controller.UpdateKycStatus(dto);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateKycStatus_UpdatesUser_AndPublishesEvent()
    {
        var (controller, db, publisher) = BuildKycController(claimUserId: Guid.NewGuid());
        var user = MakeUser("Pending");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = new UpdateKycStatusDto { UserId = user.Id, Status = "Verified", Notes = "All docs OK" };

        var result = await controller.UpdateKycStatus(dto);

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.Users.FindAsync(user.Id);
        updated!.KycStatus.Should().Be("Verified");
        publisher.Verify(p => p.Publish(It.IsAny<KycStatusChanged>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Verified")]
    [InlineData("Rejected")]
    [InlineData("UnderReview")]
    public async Task UpdateKycStatus_AcceptsAllValidStatuses(string status)
    {
        var (controller, db, _) = BuildKycController(claimUserId: Guid.NewGuid());
        var user = MakeUser("Pending");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = new UpdateKycStatusDto { UserId = user.Id, Status = status, Notes = "" };

        var result = await controller.UpdateKycStatus(dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateKycStatus_IsCaseInsensitive_ForStatus()
    {
        var (controller, db, _) = BuildKycController(claimUserId: Guid.NewGuid());
        var user = MakeUser("Pending");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = new UpdateKycStatusDto { UserId = user.Id, Status = "verified", Notes = "" };

        var result = await controller.UpdateKycStatus(dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── SarController helpers ──────────────────────────────────────────────

    private (SarController Controller, TransactionDbContext Db)
        BuildSarController(Guid? claimUserId = null)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new TransactionDbContext(options);
        var logger = new Mock<ILogger<SarController>>();

        var controller = new SarController(db, logger.Object)
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

        return (controller, db);
    }

    private SuspiciousActivityReport MakeSar(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = Guid.NewGuid(),
        UserId = userId,
        AccountId = Guid.NewGuid(),
        Amount = 15_000m,
        Currency = "NZD",
        Reason = "Large transaction",
        RiskLevel = "High",
        Status = "Open",
        FlaggedAt = DateTime.UtcNow
    };

    // ── SarController.GetSarReports ────────────────────────────────────────

    [Fact]
    public async Task GetSarReports_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _) = BuildSarController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetSarReports();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSarReports_ReturnsEmptyList_WhenNoReportsForUser()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        db.SuspiciousActivityReports.Add(MakeSar(Guid.NewGuid())); // different user
        await db.SaveChangesAsync();

        var result = await controller.GetSarReports();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<SarSummaryDto>>().Subject;
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSarReports_ReturnsOnlyCurrentUserReports()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        db.SuspiciousActivityReports.Add(MakeSar(userId));
        db.SuspiciousActivityReports.Add(MakeSar(userId));
        db.SuspiciousActivityReports.Add(MakeSar(Guid.NewGuid())); // other user
        await db.SaveChangesAsync();

        var result = await controller.GetSarReports();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<SarSummaryDto>>().Subject;
        list.Should().HaveCount(2);
    }

    // ── SarController.GetSarReport ─────────────────────────────────────────

    [Fact]
    public async Task GetSarReport_ReturnsUnauthorized_WhenNoClaimPresent()
    {
        var (controller, _) = BuildSarController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.GetSarReport(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSarReport_ReturnsNotFound_WhenReportBelongsToDifferentUser()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        var sar = MakeSar(Guid.NewGuid()); // different user
        db.SuspiciousActivityReports.Add(sar);
        await db.SaveChangesAsync();

        var result = await controller.GetSarReport(sar.Id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSarReport_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var (controller, _) = BuildSarController(claimUserId: userId);

        var result = await controller.GetSarReport(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSarReport_ReturnsOk_WithReport_WhenFound()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        var sar = MakeSar(userId);
        db.SuspiciousActivityReports.Add(sar);
        await db.SaveChangesAsync();

        var result = await controller.GetSarReport(sar.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<SarSummaryDto>().Subject;
        dto.Id.Should().Be(sar.Id);
        dto.UserId.Should().Be(userId);
    }

    // ── SarController.ResolveSar ───────────────────────────────────────────

    [Fact]
    public async Task ResolveSar_ReturnsNotFound_WhenSarDoesNotExist()
    {
        var (controller, _) = BuildSarController(claimUserId: Guid.NewGuid());
        var dto = new ResolveSarDto { Notes = "Investigated" };

        var result = await controller.ResolveSar(Guid.NewGuid(), dto);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ResolveSar_SetsStatusToResolved_AndPersistsNotes()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        var sar = MakeSar(userId);
        db.SuspiciousActivityReports.Add(sar);
        await db.SaveChangesAsync();

        var dto = new ResolveSarDto { Notes = "False positive confirmed" };
        var result = await controller.ResolveSar(sar.Id, dto);

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.SuspiciousActivityReports.FindAsync(sar.Id);
        updated!.Status.Should().Be("Resolved");
        updated.Notes.Should().Be("False positive confirmed");
        updated.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveSar_SetsResolvedAt_ToApproximatelyNow()
    {
        var userId = Guid.NewGuid();
        var (controller, db) = BuildSarController(claimUserId: userId);
        var sar = MakeSar(userId);
        db.SuspiciousActivityReports.Add(sar);
        await db.SaveChangesAsync();

        var before = DateTime.UtcNow;
        await controller.ResolveSar(sar.Id, new ResolveSarDto { Notes = "" });
        var after = DateTime.UtcNow;

        var updated = await db.SuspiciousActivityReports.FindAsync(sar.Id);
        updated!.ResolvedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
