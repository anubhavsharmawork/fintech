using Xunit;
using FluentAssertions;
using TransactionService.Models;
using TransactionService.Models.Dtos;
using AccountService.Models;
using NotificationService.Models;
using CorporateBankingService.Models.Dtos;

namespace Tests;

/// <summary>
/// Tests for models, DTOs, and records to ensure they are instantiated and covered.
/// These are simple data classes but need coverage for SonarQube.
/// </summary>
public class ModelAndDtoCoverageTests
{
    // ── TransactionService IdempotencyRecord ──────────────────────────────

    [Fact]
    public void TransactionService_IdempotencyRecord_AllPropertiesCanBeSet()
    {
        var record = new TransactionService.Models.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "test-key-123",
            RequestPath = "/api/transactions",
            RequestMethod = "POST",
            ResponseBody = "{\"id\":\"123\"}",
            ResponseStatusCode = 201,
            IsProcessing = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        record.Id.Should().NotBeEmpty();
        record.IdempotencyKey.Should().Be("test-key-123");
        record.RequestPath.Should().Be("/api/transactions");
        record.RequestMethod.Should().Be("POST");
        record.ResponseBody.Should().Be("{\"id\":\"123\"}");
        record.ResponseStatusCode.Should().Be(201);
        record.IsProcessing.Should().BeTrue();
        record.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void TransactionService_IdempotencyRecord_CanBeUsedInDb()
    {
        var record = new TransactionService.Models.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "duplicate-key",
            RequestPath = "/api/transactions",
            RequestMethod = "POST",
            ResponseBody = "{}",
            ResponseStatusCode = 200,
            IsProcessing = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        // Verify getter/setter roundtrip
        var key = record.IdempotencyKey;
        record.IdempotencyKey = "new-key";
        record.IdempotencyKey.Should().Be("new-key");
        record.IdempotencyKey = key;
        record.IdempotencyKey.Should().Be("duplicate-key");
    }

    // ── AccountService IdempotencyRecord ──────────────────────────────────

    [Fact]
    public void AccountService_IdempotencyRecord_AllPropertiesCanBeSet()
    {
        var record = new AccountService.Models.IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "account-key-456",
            RequestPath = "/api/accounts",
            RequestMethod = "POST",
            ResponseBody = "{\"accountId\":\"789\"}",
            ResponseStatusCode = 201,
            IsProcessing = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddHours(23).AddMinutes(50)
        };

        record.Id.Should().NotBeEmpty();
        record.IdempotencyKey.Should().Be("account-key-456");
        record.RequestPath.Should().Be("/api/accounts");
        record.RequestMethod.Should().Be("POST");
        record.ResponseBody.Should().Contain("accountId");
        record.ResponseStatusCode.Should().Be(201);
        record.IsProcessing.Should().BeFalse();
        record.CreatedAt.Should().BeBefore(DateTime.UtcNow);
        record.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    // ── NotificationEvent ─────────────────────────────────────────────────

    [Fact]
    public void NotificationEvent_CanBeCreated()
    {
        var evt = new NotificationEvent(
            Guid.NewGuid(),
            "PaymentCreated",
            "Your payment of $100 was successful",
            DateTime.UtcNow,
            false
        );

        evt.Id.Should().NotBeEmpty();
        evt.EventType.Should().Be("PaymentCreated");
        evt.Message.Should().Contain("$100");
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        evt.Read.Should().BeFalse();
    }

    [Fact]
    public void NotificationEvent_RecordEqualityWorks()
    {
        var id = Guid.NewGuid();
        var time = DateTime.UtcNow;

        var evt1 = new NotificationEvent(id, "Test", "Message", time, false);
        var evt2 = new NotificationEvent(id, "Test", "Message", time, false);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void NotificationEvent_CanMarkAsRead()
    {
        var evt = new NotificationEvent(
            Guid.NewGuid(),
            "Alert",
            "Test alert",
            DateTime.UtcNow,
            false
        );

        var readEvt = evt with { Read = true };

        readEvt.Read.Should().BeTrue();
        readEvt.Id.Should().Be(evt.Id);
        readEvt.EventType.Should().Be(evt.EventType);
    }

    // ── SarDto ────────────────────────────────────────────────────────────

    [Fact]
    public void SarSummaryDto_AllPropertiesCanBeSet()
    {
        var dto = new SarSummaryDto
        {
            Id = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 15000m,
            Currency = "NZD",
            Reason = "Unusual transaction pattern",
            RiskLevel = "High",
            FlaggedAt = DateTime.UtcNow.AddDays(-1),
            Status = "UnderReview"
        };

        dto.Id.Should().NotBeEmpty();
        dto.TransactionId.Should().NotBeEmpty();
        dto.UserId.Should().NotBeEmpty();
        dto.Amount.Should().Be(15000m);
        dto.Currency.Should().Be("NZD");
        dto.Reason.Should().Contain("Unusual");
        dto.RiskLevel.Should().Be("High");
        dto.FlaggedAt.Should().BeBefore(DateTime.UtcNow);
        dto.Status.Should().Be("UnderReview");
    }

    [Fact]
    public void CreateSarDto_AllPropertiesCanBeSet()
    {
        var dto = new CreateSarDto
        {
            TransactionId = Guid.NewGuid(),
            Reason = "Suspicious wire transfer",
            RiskLevel = "Medium"
        };

        dto.TransactionId.Should().NotBeEmpty();
        dto.Reason.Should().Be("Suspicious wire transfer");
        dto.RiskLevel.Should().Be("Medium");
    }

    [Fact]
    public void CreateSarDto_DefaultValuesAreEmpty()
    {
        var dto = new CreateSarDto();

        dto.TransactionId.Should().BeEmpty();
        dto.Reason.Should().BeEmpty();
        dto.RiskLevel.Should().BeEmpty();
    }

    // ── CorporateDtos ─────────────────────────────────────────────────────

    [Fact]
    public void CreateOrganisationRequest_CanBeCreated()
    {
        var req = new CreateOrganisationRequest("Acme Corp", "12345");

        req.Name.Should().Be("Acme Corp");
        req.RegistrationNumber.Should().Be("12345");
    }

    [Fact]
    public void InviteMemberRequest_CanBeCreated()
    {
        var req = new InviteMemberRequest("user@example.com", "Viewer");

        req.Email.Should().Be("user@example.com");
        req.Role.Should().Be("Viewer");
    }

    [Fact]
    public void AssignRoleRequest_CanBeCreated()
    {
        var req = new AssignRoleRequest("Admin");

        req.Role.Should().Be("Admin");
    }

    [Fact]
    public void CreateApprovalPolicyRequest_CanBeCreated()
    {
        var req = new CreateApprovalPolicyRequest(3, 100000m);

        req.RequiredApprovals.Should().Be(3);
        req.MonetaryThreshold.Should().Be(100000m);
    }

    [Fact]
    public void CreateApprovalPolicyRequest_ThresholdCanBeNull()
    {
        var req = new CreateApprovalPolicyRequest(2, null);

        req.RequiredApprovals.Should().Be(2);
        req.MonetaryThreshold.Should().BeNull();
    }

    [Fact]
    public void CreatePaymentBatchRequest_CanBeCreated()
    {
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "John Doe", "123456", 500m, "Invoice payment"),
            new(Guid.NewGuid(), "Jane Smith", null, 300m, null)
        };

        var req = new CreatePaymentBatchRequest("NZD", items);

        req.Currency.Should().Be("NZD");
        req.Items.Should().HaveCount(2);
        req.Items[0].PayeeName.Should().Be("John Doe");
        req.Items[1].PayeeAccountNumber.Should().BeNull();
    }

    [Fact]
    public void PaymentBatchItemDto_CanBeCreated()
    {
        var accountId = Guid.NewGuid();
        var dto = new PaymentBatchItemDto(accountId, "Supplier ABC", "98765", 1200m, "Monthly payment");

        dto.SourceAccountId.Should().Be(accountId);
        dto.PayeeName.Should().Be("Supplier ABC");
        dto.PayeeAccountNumber.Should().Be("98765");
        dto.Amount.Should().Be(1200m);
        dto.Description.Should().Be("Monthly payment");
    }

    [Fact]
    public void ApprovalDecisionRequest_CanBeCreated()
    {
        var req = new ApprovalDecisionRequest("Approved", "Looks good");

        req.Decision.Should().Be("Approved");
        req.Comments.Should().Be("Looks good");
    }

    [Fact]
    public void ApprovalDecisionRequest_CommentsCanBeNull()
    {
        var req = new ApprovalDecisionRequest("Rejected", null);

        req.Decision.Should().Be("Rejected");
        req.Comments.Should().BeNull();
    }

    [Fact]
    public void OrganisationResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var res = new OrganisationResponse(id, "Test Org", "REG123", createdAt);

        res.Id.Should().Be(id);
        res.Name.Should().Be("Test Org");
        res.RegistrationNumber.Should().Be("REG123");
        res.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void MemberResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedAt = DateTime.UtcNow.AddDays(-2);
        var acceptedAt = DateTime.UtcNow.AddDays(-1);

        var res = new MemberResponse(id, userId, "member@test.com", "Approver", "Active", invitedAt, acceptedAt);

        res.Id.Should().Be(id);
        res.UserId.Should().Be(userId);
        res.Email.Should().Be("member@test.com");
        res.Role.Should().Be("Approver");
        res.Status.Should().Be("Active");
        res.InvitedAt.Should().Be(invitedAt);
        res.AcceptedAt.Should().Be(acceptedAt);
    }

    [Fact]
    public void MemberResponse_AcceptedAtCanBeNull()
    {
        var res = new MemberResponse(Guid.NewGuid(), Guid.NewGuid(), "pending@test.com", "Viewer", "Pending", DateTime.UtcNow, null);

        res.AcceptedAt.Should().BeNull();
        res.Status.Should().Be("Pending");
    }

    [Fact]
    public void ApprovalPolicyResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var res = new ApprovalPolicyResponse(id, 2, 50000m);

        res.Id.Should().Be(id);
        res.RequiredApprovals.Should().Be(2);
        res.MonetaryThreshold.Should().Be(50000m);
    }

    [Fact]
    public void ApprovalPolicyResponse_ThresholdCanBeNull()
    {
        var res = new ApprovalPolicyResponse(Guid.NewGuid(), 1, null);

        res.RequiredApprovals.Should().Be(1);
        res.MonetaryThreshold.Should().BeNull();
    }

    [Fact]
    public void PaymentBatchResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-2);
        var submittedAt = DateTime.UtcNow.AddHours(-1);

        var res = new PaymentBatchResponse(id, orgId, userId, "PendingApproval", "NZD", 5000m, 3, createdAt, submittedAt, null);

        res.Id.Should().Be(id);
        res.OrganisationId.Should().Be(orgId);
        res.SubmittedByUserId.Should().Be(userId);
        res.Status.Should().Be("PendingApproval");
        res.Currency.Should().Be("NZD");
        res.TotalAmount.Should().Be(5000m);
        res.ItemCount.Should().Be(3);
        res.CreatedAt.Should().Be(createdAt);
        res.SubmittedAt.Should().Be(submittedAt);
        res.ExecutedAt.Should().BeNull();
    }

    [Fact]
    public void PaymentBatchResponse_ExecutedAtCanBeSet()
    {
        var executedAt = DateTime.UtcNow;
        var res = new PaymentBatchResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Executed", "EUR", 2000m, 1, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2), executedAt);

        res.Status.Should().Be("Executed");
        res.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void PaymentBatchDetailResponse_CanBeCreated()
    {
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "Vendor A", "111", 100m, "Payment 1")
        };
        var approvals = new List<ApprovalRecordResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Approved", "LGTM", DateTime.UtcNow)
        };

        var res = new PaymentBatchDetailResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Approved", "GBP",
            100m, 1, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, null, items, approvals);

        res.Status.Should().Be("Approved");
        res.Currency.Should().Be("GBP");
        res.Items.Should().HaveCount(1);
        res.Approvals.Should().HaveCount(1);
        res.Approvals[0].Decision.Should().Be("Approved");
    }

    [Fact]
    public void PaymentBatchDetailResponse_CanHaveEmptyCollections()
    {
        var res = new PaymentBatchDetailResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Draft", "CAD",
            0m, 0, DateTime.UtcNow, null, null, new List<PaymentBatchItemDto>(), new List<ApprovalRecordResponse>());

        res.Status.Should().Be("Draft");
        res.Items.Should().BeEmpty();
        res.Approvals.Should().BeEmpty();
    }

    [Fact]
    public void ApprovalRecordResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var decidedAt = DateTime.UtcNow;

        var res = new ApprovalRecordResponse(id, userId, "Rejected", "Insufficient funds", decidedAt);

        res.Id.Should().Be(id);
        res.ApprovedByUserId.Should().Be(userId);
        res.Decision.Should().Be("Rejected");
        res.Comments.Should().Be("Insufficient funds");
        res.DecidedAt.Should().Be(decidedAt);
    }

    [Fact]
    public void ApprovalRecordResponse_CommentsCanBeNull()
    {
        var res = new ApprovalRecordResponse(Guid.NewGuid(), Guid.NewGuid(), "Approved", null, DateTime.UtcNow);

        res.Decision.Should().Be("Approved");
        res.Comments.Should().BeNull();
    }
}
