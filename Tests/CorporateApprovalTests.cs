using CorporateBankingService.Data;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class CorporateApprovalTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private CorporateDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<CorporateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CorporateDbContext(options);
    }

    private (ApprovalWorkflowService Workflow, ApprovalService Service, CorporateDbContext Db, Mock<IPublishEndpoint> Publisher)
        BuildServices()
    {
        var db = BuildDb();
        var publisher = new Mock<IPublishEndpoint>();
        var workflowLogger = new Mock<ILogger<ApprovalWorkflowService>>();
        var serviceLogger = new Mock<ILogger<ApprovalService>>();

        var workflow = new ApprovalWorkflowService(db, workflowLogger.Object);
        var service = new ApprovalService(db, workflow, publisher.Object, serviceLogger.Object);

        return (workflow, service, db, publisher);
    }

    private (ApprovalService Service, CorporateDbContext Db, Mock<IPublishEndpoint> Publisher, Mock<IApprovalWorkflowService> Workflow)
        BuildServicesWithMockedWorkflow()
    {
        var db = BuildDb();
        var publisher = new Mock<IPublishEndpoint>();
        var serviceLogger = new Mock<ILogger<ApprovalService>>();
        var workflow = new Mock<IApprovalWorkflowService>();

        var service = new ApprovalService(db, workflow.Object, publisher.Object, serviceLogger.Object);
        return (service, db, publisher, workflow);
    }

    private Organisation MakeOrg() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Org",
        RegistrationNumber = $"REG-{Guid.NewGuid():N}",
        CreatedByUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private PaymentBatch MakeBatch(Guid orgId, string status = "PendingApproval", decimal total = 1000m,
        Organisation? org = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = orgId,
        SubmittedByUserId = Guid.NewGuid(),
        Status = status,
        Currency = "NZD",
        TotalAmount = total,
        ItemCount = 1,
        CreatedAt = DateTime.UtcNow,
        Organisation = org!
    };

    private ApprovalPolicy MakePolicy(Guid orgId, int required, decimal? threshold = null,
        Organisation? org = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganisationId = orgId,
        RequiredApprovals = required,
        MonetaryThreshold = threshold,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Organisation = org!
    };

    // ── ApprovalWorkflowService.GetApplicablePolicyAsync ──────────────────

    [Fact]
    public async Task GetApplicablePolicy_ReturnsNull_WhenNoPoliciesExist()
    {
        var (workflow, _, _, _) = BuildServices();

        var result = await workflow.GetApplicablePolicyAsync(Guid.NewGuid(), 500m);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApplicablePolicy_ReturnsMatchingPolicy_WhenAmountExceedsThreshold()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        var policy = MakePolicy(orgId, required: 2, threshold: 1000m);
        org.Id = orgId;
        db.Organisations.Add(org);
        db.ApprovalPolicies.Add(policy);
        await db.SaveChangesAsync();

        var result = await workflow.GetApplicablePolicyAsync(orgId, 5000m);

        result.Should().NotBeNull();
        result!.RequiredApprovals.Should().Be(2);
    }

    [Fact]
    public async Task GetApplicablePolicy_SelectsHighestMatchingThreshold()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.ApprovalPolicies.AddRange(
            MakePolicy(orgId, required: 1, threshold: 500m),
            MakePolicy(orgId, required: 3, threshold: 10_000m),
            MakePolicy(orgId, required: 2, threshold: 5_000m));
        await db.SaveChangesAsync();

        var result = await workflow.GetApplicablePolicyAsync(orgId, 7_000m);

        // 7000 >= 5000 and 7000 >= 500, but highest threshold is 5000 (10000 is not <= 7000)
        result.Should().NotBeNull();
        result!.RequiredApprovals.Should().Be(2);
    }

    [Fact]
    public async Task GetApplicablePolicy_FallsBackToLastPolicy_WhenNoneMatchAmount()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var policy = MakePolicy(orgId, required: 2, threshold: 50_000m);
        db.ApprovalPolicies.Add(policy);
        await db.SaveChangesAsync();

        // Amount 100 is below the threshold of 50_000 — should fall back
        var result = await workflow.GetApplicablePolicyAsync(orgId, 100m);

        result.Should().NotBeNull();
        result!.RequiredApprovals.Should().Be(2);
    }

    // ── ApprovalWorkflowService.HasSufficientApprovalsAsync ───────────────

    [Fact]
    public async Task HasSufficientApprovals_ReturnsTrue_WhenNoPolicyAndOneApproval()
    {
        var (workflow, _, _, _) = BuildServices();
        var batch = MakeBatch(Guid.NewGuid());
        batch.Approvals.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow
        });

        var result = await workflow.HasSufficientApprovalsAsync(Guid.NewGuid(), batch);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientApprovals_ReturnsFalse_WhenNoPolicyAndZeroApprovals()
    {
        var (workflow, _, _, _) = BuildServices();
        var batch = MakeBatch(Guid.NewGuid());

        var result = await workflow.HasSufficientApprovalsAsync(Guid.NewGuid(), batch);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasSufficientApprovals_ReturnsFalse_WhenApprovalCountBelowRequired()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.ApprovalPolicies.Add(MakePolicy(orgId, required: 3, threshold: 100m, org: org));
        await db.SaveChangesAsync();

        var batch = MakeBatch(orgId, total: 500m);
        batch.Approvals.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow
        });

        var result = await workflow.HasSufficientApprovalsAsync(orgId, batch);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasSufficientApprovals_ReturnsTrue_WhenApprovalCountMeetsRequired()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.ApprovalPolicies.Add(MakePolicy(orgId, required: 2, threshold: 100m, org: org));
        await db.SaveChangesAsync();

        var batch = MakeBatch(orgId, total: 500m);
        for (var i = 0; i < 2; i++)
        {
            batch.Approvals.Add(new ApprovalRecord
            {
                Id = Guid.NewGuid(),
                PaymentBatchId = batch.Id,
                ApprovedByUserId = Guid.NewGuid(),
                Decision = "Approved",
                DecidedAt = DateTime.UtcNow
            });
        }

        var result = await workflow.HasSufficientApprovalsAsync(orgId, batch);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientApprovals_DoesNotCountRejectedDecisions()
    {
        var (workflow, _, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.ApprovalPolicies.Add(MakePolicy(orgId, required: 2, threshold: 100m, org: org));
        await db.SaveChangesAsync();

        var batch = MakeBatch(orgId, total: 500m);
        batch.Approvals.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Rejected",
            DecidedAt = DateTime.UtcNow
        });
        batch.Approvals.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow
        });

        var result = await workflow.HasSufficientApprovalsAsync(orgId, batch);

        result.Should().BeFalse(); // only 1 Approved, need 2
    }

    // ── ApprovalService.GetPendingBatchesAsync ─────────────────────────────

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Treasurer")]
    [InlineData("")]
    public async Task GetPendingBatches_Throws_WhenCallerRoleIsInsufficient(string role)
    {
        var (_, service, _, _) = BuildServices();

        var act = async () => await service.GetPendingBatchesAsync(Guid.NewGuid(), role);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("Approver")]
    [InlineData("Admin")]
    public async Task GetPendingBatches_ReturnsResults_ForAuthorisedRole(string role)
    {
        var (_, service, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.PaymentBatches.Add(MakeBatch(orgId, status: "PendingApproval", org: org));
        await db.SaveChangesAsync();

        var act = async () => await service.GetPendingBatchesAsync(orgId, role);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPendingBatches_ReturnsOnlyPendingApprovalBatches()
    {
        var (_, service, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        db.PaymentBatches.Add(MakeBatch(orgId, status: "PendingApproval", org: org));
        db.PaymentBatches.Add(MakeBatch(orgId, status: "Draft", org: org));
        db.PaymentBatches.Add(MakeBatch(orgId, status: "Approved", org: org));
        await db.SaveChangesAsync();

        var result = (IEnumerable<PaymentBatchResponse>)await service.GetPendingBatchesAsync(orgId, "Admin");

        result.Should().HaveCount(1);
        result.First().Status.Should().Be("PendingApproval");
    }

    // ── ApprovalService.GetBatchDetailAsync ───────────────────────────────

    [Fact]
    public async Task GetBatchDetail_ReturnsNull_WhenBatchNotFound()
    {
        var (_, service, _, _) = BuildServices();

        var result = await service.GetBatchDetailAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBatchDetail_ReturnsDetail_WithItemsAndApprovals()
    {
        var (_, service, db, _) = BuildServices();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var batch = MakeBatch(orgId, org: org);
        db.PaymentBatches.Add(batch);
        db.PaymentBatchItems.Add(new PaymentBatchItem
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            SourceAccountId = Guid.NewGuid(),
            PayeeName = "Alice",
            Amount = 100m
        });
        db.ApprovalRecords.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await service.GetBatchDetailAsync(batch.Id, orgId);

        result.Should().NotBeNull();
        var detail = (PaymentBatchDetailResponse)result!;
        detail.Items.Should().HaveCount(1);
        detail.Approvals.Should().HaveCount(1);
    }

    // ── ApprovalService.DecideAsync ────────────────────────────────────────

    [Fact]
    public async Task Decide_Throws_WhenCallerRoleIsInsufficient()
    {
        var (service, _, _, _) = BuildServicesWithMockedWorkflow();

        var act = async () => await service.DecideAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Viewer",
            new ApprovalDecisionRequest("Approved", null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Decide_ReturnsNull_WhenBatchNotFound()
    {
        var (service, _, _, _) = BuildServicesWithMockedWorkflow();

        var result = await service.DecideAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Admin",
            new ApprovalDecisionRequest("Approved", null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Decide_Throws_WhenBatchIsNotPendingApproval()
    {
        var (service, db, _, _) = BuildServicesWithMockedWorkflow();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var batch = MakeBatch(orgId, status: "Draft", org: org);
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();

        var act = async () => await service.DecideAsync(
            batch.Id, orgId, Guid.NewGuid(), "Admin",
            new ApprovalDecisionRequest("Approved", null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not pending approval*");
    }

    [Fact]
    public async Task Decide_Throws_WhenUserHasAlreadyDecided()
    {
        var (service, db, _, _) = BuildServicesWithMockedWorkflow();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var userId = Guid.NewGuid();
        var batch = MakeBatch(orgId, status: "PendingApproval", org: org);
        db.PaymentBatches.Add(batch);
        db.ApprovalRecords.Add(new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = userId,
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var act = async () => await service.DecideAsync(
            batch.Id, orgId, userId, "Admin",
            new ApprovalDecisionRequest("Approved", null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already submitted*");
    }

    [Fact]
    public async Task Decide_SetsStatusToRejected_WhenDecisionIsRejected()
    {
        var (service, db, _, _) = BuildServicesWithMockedWorkflow();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var batch = MakeBatch(orgId, status: "PendingApproval", org: org);
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();

        await service.DecideAsync(batch.Id, orgId, Guid.NewGuid(), "Admin",
            new ApprovalDecisionRequest("Rejected", "Not authorised"));

        var updated = await db.PaymentBatches.FindAsync(batch.Id);
        updated!.Status.Should().Be("Rejected");
    }

    [Fact]
    public async Task Decide_SetsStatusToApproved_WhenSufficientApprovals()
    {
        var (service, db, _, workflow) = BuildServicesWithMockedWorkflow();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var batch = MakeBatch(orgId, status: "PendingApproval", total: 500m, org: org);
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();
        workflow.Setup(w => w.HasSufficientApprovalsAsync(orgId, It.IsAny<PaymentBatch>()))
            .ReturnsAsync(true);

        await service.DecideAsync(batch.Id, orgId, Guid.NewGuid(), "Admin",
            new ApprovalDecisionRequest("Approved", null));

        var updated = await db.PaymentBatches.FindAsync(batch.Id);
        updated!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Decide_KeepsStatusAsPendingApproval_WhenInsufficientApprovals()
    {
        var (service, db, _, workflow) = BuildServicesWithMockedWorkflow();
        var orgId = Guid.NewGuid();
        var org = MakeOrg();
        org.Id = orgId;
        db.Organisations.Add(org);
        var batch = MakeBatch(orgId, status: "PendingApproval", total: 500m, org: org);
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();
        workflow.Setup(w => w.HasSufficientApprovalsAsync(orgId, It.IsAny<PaymentBatch>()))
            .ReturnsAsync(false);

        await service.DecideAsync(batch.Id, orgId, Guid.NewGuid(), "Admin",
            new ApprovalDecisionRequest("Approved", null));

        var updated = await db.PaymentBatches.FindAsync(batch.Id);
        updated!.Status.Should().Be("PendingApproval");
    }
}
