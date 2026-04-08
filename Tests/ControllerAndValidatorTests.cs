using System.Security.Claims;
using ApiGateway.Controllers;
using ApiGateway.Data;
using ApiGateway.Models;
using ApiGateway.Models.Dtos;
using ApiGateway.Services;
using ApiGateway.Stores;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Services;
using UserService.Controllers;
using UserService.Validation;
using ApiGatewayNotifSvc = ApiGateway.Services.NotificationPreferenceService;

namespace Tests;

// ── SanctionsController ────────────────────────────────────────────────────

public class SanctionsControllerTests
{
    private static SanctionRequest MakeRequest(SanctionStatus status = SanctionStatus.Draft) => new()
    {
        Id = Guid.NewGuid(),
        ExternalProjectId = "proj-1",
        ExternalTenantId = "tenant-1",
        UserId = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        RequestedAmount = 5000m,
        Currency = "FTK",
        Purpose = "Test",
        IdempotencyKey = Guid.NewGuid().ToString(),
        Status = status,
        KycStatus = KycStatus.Passed,
        AmlStatus = AmlStatus.Passed,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        CreatedBy = "test@test.com"
    };

    private static SanctionsController BuildController(Mock<ISanctioningService> svc, string? email = "caller@test.com")
    {
        var ctrl = new SanctionsController(svc.Object, new Mock<ILogger<SanctionsController>>().Object);
        var claims = new List<Claim>();
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        return ctrl;
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenRequiredFieldsMissing()
    {
        var svc = new Mock<ISanctioningService>();
        var ctrl = BuildController(svc);
        var dto = new CreateSanctionRequestDto("", "tenant", Guid.NewGuid(), Guid.NewGuid(),
            1000m, "FTK", "purpose", "key");

        var result = await ctrl.Create(dto, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        svc.Verify(s => s.CreateSanctionRequestAsync(It.IsAny<CreateSanctionRequestDto>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenAmountIsZero()
    {
        var svc = new Mock<ISanctioningService>();
        var ctrl = BuildController(svc);
        var dto = new CreateSanctionRequestDto("proj", "tenant", Guid.NewGuid(), Guid.NewGuid(),
            0m, "FTK", "purpose", "key");

        var result = await ctrl.Create(dto, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_Returns201_WhenScreeningStopsAtRejected()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest(SanctionStatus.Rejected);
        svc.Setup(s => s.CreateSanctionRequestAsync(It.IsAny<CreateSanctionRequestDto>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        svc.Setup(s => s.RunScreeningAsync(req.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(req);

        var ctrl = BuildController(svc);
        var dto = new CreateSanctionRequestDto("proj", "tenant", Guid.NewGuid(), Guid.NewGuid(),
            5000m, "FTK", "purpose", "key");

        var result = await ctrl.Create(dto, default);

        result.Should().BeOfType<CreatedAtActionResult>();
        svc.Verify(s => s.RunUnderwritingAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_RunsUnderwriting_WhenScreeningAdvancesToUnderwriting()
    {
        var svc = new Mock<ISanctioningService>();
        var screenedReq = MakeRequest(SanctionStatus.Underwriting);
        var finalReq = MakeRequest(SanctionStatus.Approved);
        svc.Setup(s => s.CreateSanctionRequestAsync(It.IsAny<CreateSanctionRequestDto>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(screenedReq);
        svc.Setup(s => s.RunScreeningAsync(screenedReq.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(screenedReq);
        svc.Setup(s => s.RunUnderwritingAsync(screenedReq.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalReq);

        var ctrl = BuildController(svc);
        var dto = new CreateSanctionRequestDto("proj", "tenant", Guid.NewGuid(), Guid.NewGuid(),
            5000m, "FTK", "purpose", "key");

        var result = await ctrl.Create(dto, default);

        result.Should().BeOfType<CreatedAtActionResult>();
        svc.Verify(s => s.RunUnderwritingAsync(screenedReq.Id, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest();
        svc.Setup(s => s.GetByIdAsync(req.Id, It.IsAny<CancellationToken>())).ReturnsAsync(req);
        var ctrl = BuildController(svc);

        var result = await ctrl.GetById(req.Id, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SanctionRequest?)null);
        var ctrl = BuildController(svc);

        var result = await ctrl.GetById(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GetFiltered ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFiltered_ReturnsAll_WhenNoFilter()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SanctionRequest> { MakeRequest(), MakeRequest() });
        var ctrl = BuildController(svc);

        var result = await ctrl.GetFiltered(null, null, default);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeAssignableTo<IEnumerable<SanctionRequestDto>>();
    }

    [Fact]
    public async Task GetFiltered_ReturnsSingle_WhenProjectAndUserIdProvided()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest();
        svc.Setup(s => s.GetSanctionStatusAsync("proj-1", req.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(req);
        var ctrl = BuildController(svc);

        var result = await ctrl.GetFiltered("proj-1", req.UserId, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFiltered_ReturnsEmpty_WhenProjectAndUserIdProvidedButNotFound()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.GetSanctionStatusAsync(It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((SanctionRequest?)null);
        var ctrl = BuildController(svc);

        var result = await ctrl.GetFiltered("proj-x", Guid.NewGuid(), default);

        result.Should().BeOfType<OkObjectResult>();
        var value = (IEnumerable<SanctionRequestDto>)((OkObjectResult)result).Value!;
        value.Should().BeEmpty();
    }

    // ── Disburse ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Disburse_ReturnsOk_WhenSuccessful()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest(SanctionStatus.Disbursed);
        svc.Setup(s => s.DisburseToFtkAsync(req.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(req);
        var ctrl = BuildController(svc);

        var result = await ctrl.Disburse(req.Id, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Disburse_ReturnsNotFound_WhenKeyNotFound()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.DisburseToFtkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var ctrl = BuildController(svc);

        var result = await ctrl.Disburse(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Disburse_ReturnsBadRequest_WhenInvalidOperation()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.DisburseToFtkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Not approved"));
        var ctrl = BuildController(svc);

        var result = await ctrl.Disburse(Guid.NewGuid(), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Reject ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_ReturnsBadRequest_WhenReasonMissing()
    {
        var svc = new Mock<ISanctioningService>();
        var ctrl = BuildController(svc);

        var result = await ctrl.Reject(Guid.NewGuid(), new RejectCancelRequestDto(""), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Reject_ReturnsOk_WhenSuccessful()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest(SanctionStatus.Rejected);
        svc.Setup(s => s.RejectRequestAsync(req.Id, "fraud", It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(req);
        var ctrl = BuildController(svc);

        var result = await ctrl.Reject(req.Id, new RejectCancelRequestDto("fraud"), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Reject_ReturnsNotFound_WhenKeyNotFound()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.RejectRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var ctrl = BuildController(svc);

        var result = await ctrl.Reject(Guid.NewGuid(), new RejectCancelRequestDto("reason"), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Reject_ReturnsBadRequest_WhenInvalidTransition()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.RejectRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bad transition"));
        var ctrl = BuildController(svc);

        var result = await ctrl.Reject(Guid.NewGuid(), new RejectCancelRequestDto("reason"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Cancel ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenReasonMissing()
    {
        var svc = new Mock<ISanctioningService>();
        var ctrl = BuildController(svc);

        var result = await ctrl.Cancel(Guid.NewGuid(), new RejectCancelRequestDto(""), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Cancel_ReturnsOk_WhenSuccessful()
    {
        var svc = new Mock<ISanctioningService>();
        var req = MakeRequest(SanctionStatus.Cancelled);
        svc.Setup(s => s.CancelRequestAsync(req.Id, "user request", It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(req);
        var ctrl = BuildController(svc);

        var result = await ctrl.Cancel(req.Id, new RejectCancelRequestDto("user request"), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cancel_ReturnsNotFound_WhenKeyNotFound()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.CancelRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var ctrl = BuildController(svc);

        var result = await ctrl.Cancel(Guid.NewGuid(), new RejectCancelRequestDto("reason"), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenInvalidTransition()
    {
        var svc = new Mock<ISanctioningService>();
        svc.Setup(s => s.CancelRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bad transition"));
        var ctrl = BuildController(svc);

        var result = await ctrl.Cancel(Guid.NewGuid(), new RejectCancelRequestDto("reason"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetAudit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAudit_ReturnsOkWithLogs()
    {
        var svc = new Mock<ISanctioningService>();
        var reqId = Guid.NewGuid();
        var logs = new List<SanctionAuditLog>
        {
            new() { Id = Guid.NewGuid(), SanctionRequestId = reqId,
                    FromStatus = SanctionStatus.Draft, ToStatus = SanctionStatus.Submitted,
                    ChangedBy = "admin", Reason = "submitted", Timestamp = DateTimeOffset.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString() }
        };
        svc.Setup(s => s.GetAuditLogsAsync(reqId, It.IsAny<CancellationToken>())).ReturnsAsync(logs);
        var ctrl = BuildController(svc);

        var result = await ctrl.GetAudit(reqId, default);

        result.Should().BeOfType<OkObjectResult>();
    }
}

// ── NotificationsController ────────────────────────────────────────────────

public class NotificationsControllerTests
{
    private static NotificationDbContext BuildNotifDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NotificationsController BuildController(
        NotificationDbContext db, Guid? userId = null)
    {
        var svc = new ApiGatewayNotifSvc(db);
        var store = new RecentNotificationStore();
        var ctrl = new NotificationsController(svc, store, new Mock<ILogger<NotificationsController>>().Object);

        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim("sub", userId.Value.ToString()));

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        return ctrl;
    }

    private static NotificationsController BuildControllerWithStore(
        NotificationDbContext db, RecentNotificationStore store, Guid? userId = null)
    {
        var svc = new ApiGatewayNotifSvc(db);
        var ctrl = new NotificationsController(svc, store, new Mock<ILogger<NotificationsController>>().Object);

        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim("sub", userId.Value.ToString()));

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        return ctrl;
    }

    // ── GetPreferences ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreferences_ReturnsUnauthorized_WhenNoSubClaim()
    {
        var ctrl = BuildController(BuildNotifDb(), userId: null);

        var result = await ctrl.GetPreferences();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPreferences_ReturnsOkWithEmptyList_WhenNoneStored()
    {
        var userId = Guid.NewGuid();
        var ctrl = BuildController(BuildNotifDb(), userId);

        var result = await ctrl.GetPreferences();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPreferences_ReturnsPreferences_WhenStored()
    {
        var db = BuildNotifDb();
        var userId = Guid.NewGuid();
        var svc = new ApiGatewayNotifSvc(db);
        await svc.UpdatePreference(userId, "payment.received", true, false);

        var ctrl = BuildController(db, userId);
        var result = await ctrl.GetPreferences();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── UpdatePreference ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePreference_ReturnsUnauthorized_WhenNoSubClaim()
    {
        var ctrl = BuildController(BuildNotifDb(), userId: null);

        var result = await ctrl.UpdatePreference("payment.received",
            new UpdatePreferenceRequest(true, false));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdatePreference_ReturnsBadRequest_WhenEventTypeEmpty()
    {
        var userId = Guid.NewGuid();
        var ctrl = BuildController(BuildNotifDb(), userId);

        var result = await ctrl.UpdatePreference("",
            new UpdatePreferenceRequest(true, false));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePreference_ReturnsNoContent_WhenValid()
    {
        var userId = Guid.NewGuid();
        var ctrl = BuildController(BuildNotifDb(), userId);

        var result = await ctrl.UpdatePreference("payment.received",
            new UpdatePreferenceRequest(true, true));

        result.Should().BeOfType<NoContentResult>();
    }

    // ── GetRecent ──────────────────────────────────────────────────────────

    [Fact]
    public void GetRecent_ReturnsUnauthorized_WhenNoSubClaim()
    {
        var ctrl = BuildController(BuildNotifDb(), userId: null);

        var result = ctrl.GetRecent();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void GetRecent_ReturnsOkWithEmptyList_WhenNoNotifications()
    {
        var userId = Guid.NewGuid();
        var ctrl = BuildController(BuildNotifDb(), userId);

        var result = ctrl.GetRecent();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetRecent_ReturnsUpToFiveNotifications()
    {
        var userId = Guid.NewGuid();
        var store = new RecentNotificationStore();
        for (var i = 0; i < 8; i++)
            store.Add(userId, "payment", $"msg {i}");

        var ctrl = BuildControllerWithStore(BuildNotifDb(), store, userId);
        var result = ctrl.GetRecent();

        result.Should().BeOfType<OkObjectResult>();
        var items = ((OkObjectResult)result).Value as IEnumerable<object>;
        items!.Count().Should().Be(5);
    }
}

// ── VerifyEmailRequestValidator ────────────────────────────────────────────

public class VerifyEmailRequestValidatorTests
{
    private static readonly VerifyEmailRequestValidator Validator = new();

    [Fact]
    public void Validate_Passes_WhenUserIdAndTokenProvided()
    {
        var result = Validator.Validate(new VerifyEmailRequest(Guid.NewGuid(), "valid-token"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Fails_WhenUserIdIsEmpty()
    {
        var result = Validator.Validate(new VerifyEmailRequest(Guid.Empty, "token"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("valid user ID"));
    }

    [Fact]
    public void Validate_Fails_WhenTokenIsEmpty()
    {
        var result = Validator.Validate(new VerifyEmailRequest(Guid.NewGuid(), ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("token"));
    }
}

// ── ConsoleSmsService ──────────────────────────────────────────────────────

public class ConsoleSmsServiceTests
{
    [Fact]
    public async Task SendAsync_CompletesWithoutThrowing()
    {
        var svc = new ConsoleSmsService(new Mock<ILogger<ConsoleSmsService>>().Object);

        var act = async () => await svc.SendAsync("+1234567890", "Hello from test");

        await act.Should().NotThrowAsync();
    }
}
