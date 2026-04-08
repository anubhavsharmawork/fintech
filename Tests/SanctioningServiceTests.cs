using ApiGateway.Configuration;
using ApiGateway.Data;
using ApiGateway.Models;
using ApiGateway.Models.Dtos;
using ApiGateway.Services;
using ApiGateway.Services.Underwriting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests;

public class SanctioningServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static SanctionDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<SanctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UnderwritingSettings DefaultSettings() => new()
    {
        AmountThreshold = 50_000m,
        AmountThresholdRiskPoints = 30,
        KycPassedRiskReduction = 10,
        AmlPassedRiskReduction = 10,
        NoPriorApprovalsRiskPoints = 10,
        MaxAcceptableRiskScore = 60,
        PartialApprovalDiscountPercent = 0m
    };

    private static SanctioningService BuildService(
        SanctionDbContext db,
        IEnumerable<IUnderwritingRule>? rules = null,
        UnderwritingSettings? settings = null,
        Mock<IKycService>? kyc = null,
        Mock<IAmlService>? aml = null,
        Mock<IFtkLedgerService>? ftk = null)
    {
        var opts = Options.Create(settings ?? DefaultSettings());
        return new SanctioningService(
            db,
            (kyc ?? new Mock<IKycService>()).Object,
            (aml ?? new Mock<IAmlService>()).Object,
            (ftk ?? new Mock<IFtkLedgerService>()).Object,
            rules ?? Enumerable.Empty<IUnderwritingRule>(),
            opts,
            new Mock<ILogger<SanctioningService>>().Object);
    }

    private static CreateSanctionRequestDto MakeDto(
        string idempotencyKey = "key-1",
        decimal amount = 10_000m,
        string? projectId = "proj-1",
        Guid? userId = null) => new(
        ExternalProjectId: projectId ?? "proj-1",
        ExternalTenantId: "tenant-1",
        UserId: userId ?? Guid.NewGuid(),
        AccountId: Guid.NewGuid(),
        RequestedAmount: amount,
        Currency: "FTK",
        Purpose: "Test purpose",
        IdempotencyKey: idempotencyKey
    );

    // ── CreateSanctionRequestAsync ─────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsRequest_WithSubmittedStatus()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var dto = MakeDto();

        var result = await svc.CreateSanctionRequestAsync(dto, "admin");

        result.Status.Should().Be(SanctionStatus.Submitted);
        result.ExternalProjectId.Should().Be("proj-1");
        db.SanctionRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_CreatesAuditLog_DraftToSubmitted()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var dto = MakeDto();

        var result = await svc.CreateSanctionRequestAsync(dto, "admin");

        var logs = db.SanctionAuditLogs.ToList();
        logs.Should().HaveCount(1);
        logs[0].FromStatus.Should().Be(SanctionStatus.Draft);
        logs[0].ToStatus.Should().Be(SanctionStatus.Submitted);
    }

    [Fact]
    public async Task Create_UsesFtk_WhenCurrencyIsNull()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var dto = new CreateSanctionRequestDto(
            ExternalProjectId: "proj-1",
            ExternalTenantId: "tenant-1",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: 10_000m,
            Currency: null,
            Purpose: "Test purpose",
            IdempotencyKey: Guid.NewGuid().ToString());

        var result = await svc.CreateSanctionRequestAsync(dto, "admin");

        result.Currency.Should().Be("FTK");
    }

    [Fact]
    public async Task Create_IsIdempotent_ReturnsSameRecord_OnDuplicateKey()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var dto = MakeDto("dupe-key");

        var first = await svc.CreateSanctionRequestAsync(dto, "admin");
        var second = await svc.CreateSanctionRequestAsync(dto, "admin");

        second.Id.Should().Be(first.Id);
        db.SanctionRequests.Should().HaveCount(1);
    }

    // ── RunScreeningAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RunScreening_TransitionsToUnderwriting_WhenKycAndAmlPass()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        var aml = new Mock<IAmlService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KycStatus.Passed);
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Passed);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.RunScreeningAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Underwriting);
        result.KycStatus.Should().Be(KycStatus.Passed);
        result.AmlStatus.Should().Be(AmlStatus.Passed);
    }

    [Fact]
    public async Task RunScreening_TransitionsToRejected_WhenKycFails()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KycStatus.Failed);
        var aml = new Mock<IAmlService>();
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Passed);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.RunScreeningAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Rejected);
        result.DecisionReason.Should().Contain("KYC");
    }

    [Fact]
    public async Task RunScreening_TransitionsToRejected_WhenAmlFlagged()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KycStatus.Passed);
        var aml = new Mock<IAmlService>();
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Flagged);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.RunScreeningAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Rejected);
        result.DecisionReason.Should().Contain("AML");
    }

    [Fact]
    public async Task RunScreening_KeepsScreeningStatus_WhenBothPending()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KycStatus.Pending);
        var aml = new Mock<IAmlService>();
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Pending);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.RunScreeningAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Screening);
    }

    [Fact]
    public async Task RunScreening_Throws_WhenRequestNotFound()
    {
        var db = BuildDb();
        var svc = BuildService(db);

        var act = async () => await svc.RunScreeningAsync(Guid.NewGuid(), "admin");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RunScreening_ContinuesPartially_WhenKycThrows()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("KYC service unreachable"));
        var aml = new Mock<IAmlService>();
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Passed);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.RunScreeningAsync(req.Id, "admin");

        result.KycStatus.Should().Be(KycStatus.Pending);
        result.AmlStatus.Should().Be(AmlStatus.Passed);
    }

    // ── RunUnderwritingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RunUnderwriting_ApprovesRequest_WhenRiskScoreBelowThreshold()
    {
        var db = BuildDb();
        var settings = DefaultSettings();
        settings.MaxAcceptableRiskScore = 60;
        var mockRule = new Mock<IUnderwritingRule>();
        mockRule.Setup(r => r.Evaluate(It.IsAny<SanctionRequest>(), It.IsAny<int>())).Returns(20);

        var svc = BuildService(db, rules: new[] { mockRule.Object }, settings: settings);
        var req = await CreateUnderwritingReadyRequest(db, svc);

        var result = await svc.RunUnderwritingAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Approved);
        result.RiskScore.Should().Be(20);
        result.ApprovedAmount.Should().Be(req.RequestedAmount);
    }

    [Fact]
    public async Task RunUnderwriting_RejectsRequest_WhenRiskScoreExceedsThreshold()
    {
        var db = BuildDb();
        var settings = DefaultSettings();
        settings.MaxAcceptableRiskScore = 20;
        var mockRule = new Mock<IUnderwritingRule>();
        mockRule.Setup(r => r.Evaluate(It.IsAny<SanctionRequest>(), It.IsAny<int>())).Returns(50);

        var svc = BuildService(db, rules: new[] { mockRule.Object }, settings: settings);
        var req = await CreateUnderwritingReadyRequest(db, svc);

        var result = await svc.RunUnderwritingAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Rejected);
        result.DecisionReason.Should().Contain("Risk score");
    }

    [Fact]
    public async Task RunUnderwriting_AppliesDiscount_WhenPartialApprovalConfigured()
    {
        var db = BuildDb();
        var settings = DefaultSettings();
        settings.PartialApprovalDiscountPercent = 10m;
        settings.MaxAcceptableRiskScore = 100;
        var mockRule = new Mock<IUnderwritingRule>();
        mockRule.Setup(r => r.Evaluate(It.IsAny<SanctionRequest>(), It.IsAny<int>())).Returns(0);

        var svc = BuildService(db, rules: new[] { mockRule.Object }, settings: settings);
        var req = await CreateUnderwritingReadyRequest(db, svc, amount: 10_000m);

        var result = await svc.RunUnderwritingAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Approved);
        result.ApprovedAmount.Should().Be(9_000m); // 10_000 * 0.9
    }

    [Fact]
    public async Task RunUnderwriting_Throws_WhenStatusIsNotUnderwriting()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var act = async () => await svc.RunUnderwritingAsync(req.Id, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot run underwriting*");
    }

    [Fact]
    public async Task RunUnderwriting_ClampsRiskScore_WhenNegativePointsExceedBase()
    {
        var db = BuildDb();
        var settings = DefaultSettings();
        var mockRule = new Mock<IUnderwritingRule>();
        mockRule.Setup(r => r.Evaluate(It.IsAny<SanctionRequest>(), It.IsAny<int>())).Returns(-999);

        var svc = BuildService(db, rules: new[] { mockRule.Object }, settings: settings);
        var req = await CreateUnderwritingReadyRequest(db, svc);

        var result = await svc.RunUnderwritingAsync(req.Id, "admin");

        result.RiskScore.Should().Be(0);
    }

    // ── DisburseToFtkAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task Disburse_SetsFtkRefAndDisbursedStatus()
    {
        var db = BuildDb();
        var ftk = new Mock<IFtkLedgerService>();
        ftk.Setup(f => f.AllocateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("txref-123");

        var svc = BuildService(db, ftk: ftk);
        var req = await CreateApprovedRequest(db, svc);

        var result = await svc.DisburseToFtkAsync(req.Id, "admin");

        result.Status.Should().Be(SanctionStatus.Disbursed);
        result.FtkTransactionRef.Should().Be("txref-123");
    }

    [Fact]
    public async Task Disburse_Throws_WhenStatusIsNotApproved()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var act = async () => await svc.DisburseToFtkAsync(req.Id, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Must be Approved*");
    }

    [Fact]
    public async Task Disburse_Rethrows_WhenFtkThrows()
    {
        var db = BuildDb();
        var ftk = new Mock<IFtkLedgerService>();
        ftk.Setup(f => f.AllocateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FTK unavailable"));

        var svc = BuildService(db, ftk: ftk);
        var req = await CreateApprovedRequest(db, svc);

        var act = async () => await svc.DisburseToFtkAsync(req.Id, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FTK unavailable*");
    }

    // ── RejectRequestAsync / CancelRequestAsync ────────────────────────────

    [Fact]
    public async Task Reject_SetsRejectedStatusAndReason()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        // Must be in Screening or Underwriting to allow Rejected transition
        req.Status = SanctionStatus.Screening;
        await db.SaveChangesAsync();

        var result = await svc.RejectRequestAsync(req.Id, "compliance issue", "admin");

        result.Status.Should().Be(SanctionStatus.Rejected);
        result.DecisionReason.Should().Be("compliance issue");
    }

    [Fact]
    public async Task Cancel_SetsCancelledStatus()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.CancelRequestAsync(req.Id, "customer withdrew", "admin");

        result.Status.Should().Be(SanctionStatus.Cancelled);
        result.DecisionReason.Should().Be("customer withdrew");
    }

    // ── GetSanctionStatusAsync / GetByIdAsync / GetAuditLogsAsync / GetAllAsync ──

    [Fact]
    public async Task GetSanctionStatus_ReturnsLatest_ForProjectAndUser()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var userId = Guid.NewGuid();
        var dto = MakeDto("key-a", projectId: "proj-x", userId: userId);

        var created = await svc.CreateSanctionRequestAsync(dto, "admin");

        var result = await svc.GetSanctionStatusAsync("proj-x", userId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetSanctionStatus_ReturnsNull_WhenNotFound()
    {
        var db = BuildDb();
        var svc = BuildService(db);

        var result = await svc.GetSanctionStatusAsync("nonexistent", Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_ReturnsRequest()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");

        var result = await svc.GetByIdAsync(req.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(req.Id);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        var db = BuildDb();
        var svc = BuildService(db);

        var result = await svc.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsLogsInOrder()
    {
        var db = BuildDb();
        var kyc = new Mock<IKycService>();
        kyc.Setup(k => k.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(KycStatus.Failed);
        var aml = new Mock<IAmlService>();
        aml.Setup(a => a.ScreenAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AmlStatus.Passed);

        var svc = BuildService(db, kyc: kyc, aml: aml);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");
        await svc.RunScreeningAsync(req.Id, "admin");

        var logs = await svc.GetAuditLogsAsync(req.Id);

        logs.Should().HaveCountGreaterThan(1);
        logs.Should().BeInAscendingOrder(l => l.Timestamp);
    }

    [Fact]
    public async Task GetAll_ReturnsAllRequests()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        await svc.CreateSanctionRequestAsync(MakeDto("k1"), "admin");
        await svc.CreateSanctionRequestAsync(MakeDto("k2"), "admin");

        var all = await svc.GetAllAsync();

        all.Should().HaveCount(2);
    }

    // ── TransitionStatus invalid transition ────────────────────────────────

    [Fact]
    public async Task Reject_Throws_OnInvalidTransition()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var req = await svc.CreateSanctionRequestAsync(MakeDto(), "admin");
        await svc.CancelRequestAsync(req.Id, "cancel", "admin");

        var act = async () => await svc.RejectRequestAsync(req.Id, "reason", "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid status transition*");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<SanctionRequest> CreateUnderwritingReadyRequest(
        SanctionDbContext db, SanctioningService svc, decimal amount = 10_000m)
    {
        var dto = MakeDto(Guid.NewGuid().ToString(), amount);
        var req = await svc.CreateSanctionRequestAsync(dto, "admin");

        // Manually set to Underwriting status for testing
        req.Status = SanctionStatus.Underwriting;
        req.KycStatus = KycStatus.Passed;
        req.AmlStatus = AmlStatus.Passed;
        await db.SaveChangesAsync();

        return req;
    }

    private static async Task<SanctionRequest> CreateApprovedRequest(
        SanctionDbContext db, SanctioningService svc)
    {
        var dto = MakeDto(Guid.NewGuid().ToString());
        var req = await svc.CreateSanctionRequestAsync(dto, "admin");

        req.Status = SanctionStatus.Approved;
        req.ApprovedAmount = req.RequestedAmount;
        await db.SaveChangesAsync();

        return req;
    }
}
