using Xunit;
using FluentAssertions;
using Contracts.Events;
using ApiGateway.Models;
using ApiGateway.Models.Dtos;
using ApiGateway.Services;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Controllers;
using CorporateBankingService.Data;
using CorporateBankingService.Services;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Tests;

/// <summary>
/// Tests to boost code coverage for models, DTOs, events, and controllers
/// with low coverage percentages.
/// </summary>
public class CoverageBoostTests
{
    #region Contract Events Tests

    [Fact]
    public void DrawdownRequested_AllPropertiesCanBeSet()
    {
        var facilityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var walletAddress = "0x1234567890abcdef";
        var amount = 1000m;
        var currency = "USD";
        var outstandingBalance = 5000m;
        var requestedAt = DateTime.UtcNow;

        var evt = new DrawdownRequested(
            facilityId, userId, walletAddress, amount, 
            currency, outstandingBalance, requestedAt);

        evt.FacilityId.Should().Be(facilityId);
        evt.UserId.Should().Be(userId);
        evt.WalletAddress.Should().Be(walletAddress);
        evt.Amount.Should().Be(amount);
        evt.Currency.Should().Be(currency);
        evt.OutstandingBalance.Should().Be(outstandingBalance);
        evt.RequestedAt.Should().Be(requestedAt);
    }

    [Fact]
    public void DrawdownRequested_SupportsEquality()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new DrawdownRequested(id1, id2, "0x123", 100m, "USD", 500m, now);
        var evt2 = new DrawdownRequested(id1, id2, "0x123", 100m, "USD", 500m, now);

        evt1.Should().Be(evt2);
        (evt1 == evt2).Should().BeTrue();
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void DrawdownRequested_SupportsDeconstruction()
    {
        var facilityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var evt = new DrawdownRequested(facilityId, userId, "0xabc", 200m, "EUR", 1000m, DateTime.UtcNow);

        var (fid, uid, wallet, amt, curr, balance, reqAt) = evt;

        fid.Should().Be(facilityId);
        uid.Should().Be(userId);
        wallet.Should().Be("0xabc");
        amt.Should().Be(200m);
        curr.Should().Be("EUR");
        balance.Should().Be(1000m);
    }

    [Fact]
    public void PaymentBatchSubmittedForApproval_AllPropertiesCanBeSet()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var itemCount = 5;
        var totalAmount = 2500m;
        var currency = "NZD";
        var submittedAt = DateTime.UtcNow;

        var evt = new PaymentBatchSubmittedForApproval(
            batchId, orgId, userId, itemCount, totalAmount, currency, submittedAt);

        evt.BatchId.Should().Be(batchId);
        evt.OrganisationId.Should().Be(orgId);
        evt.SubmittedByUserId.Should().Be(userId);
        evt.ItemCount.Should().Be(itemCount);
        evt.TotalAmount.Should().Be(totalAmount);
        evt.Currency.Should().Be(currency);
        evt.SubmittedAt.Should().Be(submittedAt);
    }

    [Fact]
    public void PaymentBatchSubmittedForApproval_SupportsEquality()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new PaymentBatchSubmittedForApproval(id1, id2, id3, 3, 1500m, "USD", now);
        var evt2 = new PaymentBatchSubmittedForApproval(id1, id2, id3, 3, 1500m, "USD", now);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void PaymentBatchSubmittedForApproval_SupportsDeconstruction()
    {
        var batchId = Guid.NewGuid();
        var evt = new PaymentBatchSubmittedForApproval(
            batchId, Guid.NewGuid(), Guid.NewGuid(), 10, 5000m, "GBP", DateTime.UtcNow);

        var (bid, oid, uid, count, total, curr, submitted) = evt;

        bid.Should().Be(batchId);
        count.Should().Be(10);
        total.Should().Be(5000m);
        curr.Should().Be("GBP");
    }

    [Fact]
    public void BatchExecuted_AllPropertiesCanBeSet()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var txCount = 10;
        var totalAmount = 5000m;
        var currency = "AUD";
        var executedAt = DateTime.UtcNow;

        var evt = new BatchExecuted(batchId, orgId, txCount, totalAmount, currency, executedAt);

        evt.BatchId.Should().Be(batchId);
        evt.OrganisationId.Should().Be(orgId);
        evt.TransactionCount.Should().Be(txCount);
        evt.TotalAmount.Should().Be(totalAmount);
        evt.Currency.Should().Be(currency);
        evt.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void BatchExecuted_SupportsEquality()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new BatchExecuted(id1, id2, 5, 2500m, "NZD", now);
        var evt2 = new BatchExecuted(id1, id2, 5, 2500m, "NZD", now);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void BatchExecuted_SupportsDeconstruction()
    {
        var batchId = Guid.NewGuid();
        var evt = new BatchExecuted(batchId, Guid.NewGuid(), 20, 10000m, "CAD", DateTime.UtcNow);

        var (bid, oid, count, total, curr, executed) = evt;

        bid.Should().Be(batchId);
        count.Should().Be(20);
        total.Should().Be(10000m);
        curr.Should().Be("CAD");
    }

    [Fact]
    public void OrganisationCreated_AllPropertiesCanBeSet()
    {
        var orgId = Guid.NewGuid();
        var name = "Test Company Ltd";
        var regNumber = "NZ123456";
        var createdBy = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var evt = new OrganisationCreated(orgId, name, regNumber, createdBy, createdAt);

        evt.OrganisationId.Should().Be(orgId);
        evt.Name.Should().Be(name);
        evt.RegistrationNumber.Should().Be(regNumber);
        evt.CreatedByUserId.Should().Be(createdBy);
        evt.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void OrganisationCreated_SupportsEquality()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new OrganisationCreated(id, "Company", "REG001", userId, now);
        var evt2 = new OrganisationCreated(id, "Company", "REG001", userId, now);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void OrganisationCreated_SupportsDeconstruction()
    {
        var orgId = Guid.NewGuid();
        var evt = new OrganisationCreated(orgId, "My Org", "REG999", Guid.NewGuid(), DateTime.UtcNow);

        var (oid, name, regNum, createdBy, createdAt) = evt;

        oid.Should().Be(orgId);
        name.Should().Be("My Org");
        regNum.Should().Be("REG999");
    }

    [Fact]
    public void KycStatusChanged_AllPropertiesCanBeSet()
    {
        var userId = Guid.NewGuid();
        var status = "Approved";
        var notes = "All documents verified";
        var changedAt = DateTime.UtcNow;

        var evt = new KycStatusChanged(userId, status, notes, changedAt);

        evt.UserId.Should().Be(userId);
        evt.Status.Should().Be(status);
        evt.Notes.Should().Be(notes);
        evt.ChangedAt.Should().Be(changedAt);
    }

    [Fact]
    public void KycStatusChanged_SupportsEquality()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new KycStatusChanged(id, "Pending", "Awaiting docs", now);
        var evt2 = new KycStatusChanged(id, "Pending", "Awaiting docs", now);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void KycStatusChanged_SupportsDeconstruction()
    {
        var userId = Guid.NewGuid();
        var evt = new KycStatusChanged(userId, "Rejected", "Invalid ID", DateTime.UtcNow);

        var (uid, status, notes, changed) = evt;

        uid.Should().Be(userId);
        status.Should().Be("Rejected");
        notes.Should().Be("Invalid ID");
    }

    [Fact]
    public void PaymentApproved_AllPropertiesCanBeSet()
    {
        var batchId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var approvedAt = DateTime.UtcNow;

        var evt = new PaymentApproved(batchId, orgId, approvedBy, approvedAt);

        evt.BatchId.Should().Be(batchId);
        evt.OrganisationId.Should().Be(orgId);
        evt.ApprovedByUserId.Should().Be(approvedBy);
        evt.ApprovedAt.Should().Be(approvedAt);
    }

    [Fact]
    public void PaymentApproved_SupportsEquality()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new PaymentApproved(id1, id2, id3, now);
        var evt2 = new PaymentApproved(id1, id2, id3, now);

        evt1.Should().Be(evt2);
        evt1.GetHashCode().Should().Be(evt2.GetHashCode());
    }

    [Fact]
    public void PaymentApproved_SupportsDeconstruction()
    {
        var batchId = Guid.NewGuid();
        var evt = new PaymentApproved(batchId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var (bid, oid, approver, approvedAt) = evt;

        bid.Should().Be(batchId);
    }

    #endregion

    #region SanctionAuditLog and SanctionAuditLogDto Tests

    [Fact]
    public void SanctionAuditLog_AllPropertiesCanBeSet()
    {
        var log = new SanctionAuditLog
        {
            Id = Guid.NewGuid(),
            SanctionRequestId = Guid.NewGuid(),
            FromStatus = SanctionStatus.Draft,
            ToStatus = SanctionStatus.Approved,
            ChangedBy = "admin@test.com",
            Reason = "Manual review completed",
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        log.Id.Should().NotBeEmpty();
        log.SanctionRequestId.Should().NotBeEmpty();
        log.FromStatus.Should().Be(SanctionStatus.Draft);
        log.ToStatus.Should().Be(SanctionStatus.Approved);
        log.ChangedBy.Should().Be("admin@test.com");
        log.Reason.Should().Be("Manual review completed");
        log.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        log.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SanctionAuditLog_NavigationPropertyCanBeSet()
    {
        var sanctionRequest = new SanctionRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            ExternalProjectId = "proj-001",
            ExternalTenantId = "tenant-001",
            Purpose = "Test purpose",
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedBy = "test@test.com"
        };

        var log = new SanctionAuditLog
        {
            Id = Guid.NewGuid(),
            SanctionRequestId = sanctionRequest.Id,
            FromStatus = SanctionStatus.Draft,
            ToStatus = SanctionStatus.Rejected,
            ChangedBy = "system",
            Reason = "Auto-rejected",
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-123",
            SanctionRequest = sanctionRequest
        };

        log.SanctionRequest.Should().NotBeNull();
        log.SanctionRequest.Id.Should().Be(sanctionRequest.Id);
    }

    [Fact]
    public void SanctionAuditLogDto_AllPropertiesCanBeSet()
    {
        var id = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var dto = new SanctionAuditLogDto(
            id, reqId, "Pending", "Approved", "admin", "Review done", timestamp, "corr-456");

        dto.Id.Should().Be(id);
        dto.SanctionRequestId.Should().Be(reqId);
        dto.FromStatus.Should().Be("Pending");
        dto.ToStatus.Should().Be("Approved");
        dto.ChangedBy.Should().Be("admin");
        dto.Reason.Should().Be("Review done");
        dto.Timestamp.Should().Be(timestamp);
        dto.CorrelationId.Should().Be("corr-456");
    }

    [Fact]
    public void SanctionAuditLogDto_SupportsEquality()
    {
        var id = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var dto1 = new SanctionAuditLogDto(id, reqId, "A", "B", "user", "reason", timestamp, "corr");
        var dto2 = new SanctionAuditLogDto(id, reqId, "A", "B", "user", "reason", timestamp, "corr");

        dto1.Should().Be(dto2);
        dto1.GetHashCode().Should().Be(dto2.GetHashCode());
    }

    [Fact]
    public void SanctionAuditLogDto_SupportsDeconstruction()
    {
        var id = Guid.NewGuid();
        var dto = new SanctionAuditLogDto(
            id, Guid.NewGuid(), "X", "Y", "changer", "notes", DateTimeOffset.UtcNow, "corr-789");

        var (logId, reqId, from, to, changed, reason, ts, corr) = dto;

        logId.Should().Be(id);
        from.Should().Be("X");
        to.Should().Be("Y");
        changed.Should().Be("changer");
        reason.Should().Be("notes");
        corr.Should().Be("corr-789");
    }

    #endregion

    #region CreditRepayment Tests

    [Fact]
    public void CreditRepayment_AllPropertiesCanBeSet()
    {
        var repayment = new CreditRepayment
        {
            Id = Guid.NewGuid(),
            FacilityId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 500m,
            Currency = "USD",
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        repayment.Id.Should().NotBeEmpty();
        repayment.FacilityId.Should().NotBeEmpty();
        repayment.UserId.Should().NotBeEmpty();
        repayment.Amount.Should().Be(500m);
        repayment.Currency.Should().Be("USD");
        repayment.Status.Should().Be("Completed");
        repayment.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreditRepayment_HasDefaultValues()
    {
        var repayment = new CreditRepayment();

        repayment.Currency.Should().Be("FTK");
        repayment.Status.Should().Be("Completed");
    }

    [Fact]
    public void CreditRepayment_NavigationPropertyCanBeSet()
    {
        var facility = new CreditFacility
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CreditLimit = 10000m
        };

        var repayment = new CreditRepayment
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            UserId = facility.UserId,
            Amount = 1000m,
            Facility = facility
        };

        repayment.Facility.Should().NotBeNull();
        repayment.Facility.Id.Should().Be(facility.Id);
    }

    #endregion

    #region Corporate Models Tests

    [Fact]
    public void ApprovalRecord_AllPropertiesCanBeSet()
    {
        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = Guid.NewGuid(),
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            Comments = "Looks good",
            DecidedAt = DateTime.UtcNow
        };

        record.Id.Should().NotBeEmpty();
        record.PaymentBatchId.Should().NotBeEmpty();
        record.ApprovedByUserId.Should().NotBeEmpty();
        record.Decision.Should().Be("Approved");
        record.Comments.Should().Be("Looks good");
        record.DecidedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApprovalRecord_CommentsCanBeNull()
    {
        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = Guid.NewGuid(),
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Rejected",
            Comments = null,
            DecidedAt = DateTime.UtcNow
        };

        record.Comments.Should().BeNull();
    }

    [Fact]
    public void ApprovalRecord_NavigationPropertyCanBeSet()
    {
        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            SubmittedByUserId = Guid.NewGuid(),
            Status = "PendingApproval",
            Currency = "NZD",
            TotalAmount = 5000m,
            ItemCount = 3,
            CreatedAt = DateTime.UtcNow
        };

        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            ApprovedByUserId = Guid.NewGuid(),
            Decision = "Approved",
            DecidedAt = DateTime.UtcNow,
            PaymentBatch = batch
        };

        record.PaymentBatch.Should().NotBeNull();
        record.PaymentBatch.Id.Should().Be(batch.Id);
    }

    [Fact]
    public void PaymentBatchItem_AllPropertiesCanBeSet()
    {
        var item = new PaymentBatchItem
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = Guid.NewGuid(),
            SourceAccountId = Guid.NewGuid(),
            PayeeName = "John Doe",
            PayeeAccountNumber = "1234567890",
            Amount = 250m,
            Description = "Monthly payment"
        };

        item.Id.Should().NotBeEmpty();
        item.PaymentBatchId.Should().NotBeEmpty();
        item.SourceAccountId.Should().NotBeEmpty();
        item.PayeeName.Should().Be("John Doe");
        item.PayeeAccountNumber.Should().Be("1234567890");
        item.Amount.Should().Be(250m);
        item.Description.Should().Be("Monthly payment");
    }

    [Fact]
    public void PaymentBatchItem_OptionalFieldsCanBeNull()
    {
        var item = new PaymentBatchItem
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = Guid.NewGuid(),
            SourceAccountId = Guid.NewGuid(),
            PayeeName = "Jane Smith",
            PayeeAccountNumber = null,
            Amount = 100m,
            Description = null
        };

        item.PayeeAccountNumber.Should().BeNull();
        item.Description.Should().BeNull();
    }

    [Fact]
    public void PaymentBatchItem_NavigationPropertyCanBeSet()
    {
        var batch = new PaymentBatch
        {
            Id = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            SubmittedByUserId = Guid.NewGuid(),
            Status = "Draft",
            Currency = "USD",
            TotalAmount = 1000m,
            ItemCount = 2,
            CreatedAt = DateTime.UtcNow
        };

        var item = new PaymentBatchItem
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batch.Id,
            SourceAccountId = Guid.NewGuid(),
            PayeeName = "Vendor",
            Amount = 500m,
            PaymentBatch = batch
        };

        item.PaymentBatch.Should().NotBeNull();
        item.PaymentBatch.Id.Should().Be(batch.Id);
    }

    [Fact]
    public void OrganisationMember_AllPropertiesCanBeSet()
    {
        var member = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "member@company.com",
            Role = "Admin",
            Status = "Active",
            InvitedAt = DateTime.UtcNow.AddDays(-7),
            AcceptedAt = DateTime.UtcNow
        };

        member.Id.Should().NotBeEmpty();
        member.OrganisationId.Should().NotBeEmpty();
        member.UserId.Should().NotBeEmpty();
        member.Email.Should().Be("member@company.com");
        member.Role.Should().Be("Admin");
        member.Status.Should().Be("Active");
        member.InvitedAt.Should().BeBefore(DateTime.UtcNow);
        member.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OrganisationMember_HasDefaultValues()
    {
        var member = new OrganisationMember();

        member.Role.Should().Be("Viewer");
        member.Status.Should().Be("Invited");
    }

    [Fact]
    public void OrganisationMember_AcceptedAtCanBeNull()
    {
        var member = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "pending@company.com",
            Role = "Approver",
            Status = "Invited",
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = null
        };

        member.AcceptedAt.Should().BeNull();
    }

    [Fact]
    public void OrganisationMember_NavigationPropertyCanBeSet()
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            RegistrationNumber = "REG001",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var member = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            UserId = Guid.NewGuid(),
            Email = "user@test.com",
            InvitedAt = DateTime.UtcNow,
            Organisation = org
        };

        member.Organisation.Should().NotBeNull();
        member.Organisation.Id.Should().Be(org.Id);
    }

    #endregion

    #region Corporate DTOs Tests

    [Fact]
    public void CreateOrganisationRequest_CanBeCreated()
    {
        var request = new CreateOrganisationRequest("Acme Corp", "REG123");

        request.Name.Should().Be("Acme Corp");
        request.RegistrationNumber.Should().Be("REG123");
    }

    [Fact]
    public void InviteMemberRequest_CanBeCreated()
    {
        var request = new InviteMemberRequest("newuser@company.com", "Treasurer");

        request.Email.Should().Be("newuser@company.com");
        request.Role.Should().Be("Treasurer");
    }

    [Fact]
    public void AssignRoleRequest_CanBeCreated()
    {
        var request = new AssignRoleRequest("Admin");

        request.Role.Should().Be("Admin");
    }

    [Fact]
    public void CreateApprovalPolicyRequest_CanBeCreated()
    {
        var request = new CreateApprovalPolicyRequest(2, 5000m);

        request.RequiredApprovals.Should().Be(2);
        request.MonetaryThreshold.Should().Be(5000m);
    }

    [Fact]
    public void CreateApprovalPolicyRequest_ThresholdCanBeNull()
    {
        var request = new CreateApprovalPolicyRequest(1, null);

        request.RequiredApprovals.Should().Be(1);
        request.MonetaryThreshold.Should().BeNull();
    }

    [Fact]
    public void CreatePaymentBatchRequest_CanBeCreated()
    {
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "Vendor A", "12345", 100m, "Payment 1"),
            new(Guid.NewGuid(), "Vendor B", "67890", 200m, "Payment 2")
        };

        var request = new CreatePaymentBatchRequest("USD", items);

        request.Currency.Should().Be("USD");
        request.Items.Should().HaveCount(2);
    }

    [Fact]
    public void PaymentBatchItemDto_CanBeCreated()
    {
        var dto = new PaymentBatchItemDto(
            Guid.NewGuid(), "Payee Name", "ACC123", 500m, "Description");

        dto.PayeeName.Should().Be("Payee Name");
        dto.PayeeAccountNumber.Should().Be("ACC123");
        dto.Amount.Should().Be(500m);
        dto.Description.Should().Be("Description");
    }

    [Fact]
    public void PaymentBatchItemDto_OptionalFieldsCanBeNull()
    {
        var dto = new PaymentBatchItemDto(
            Guid.NewGuid(), "Payee", null, 100m, null);

        dto.PayeeAccountNumber.Should().BeNull();
        dto.Description.Should().BeNull();
    }

    [Fact]
    public void ApprovalDecisionRequest_CanBeCreated()
    {
        var request = new ApprovalDecisionRequest("Approved", "All checks passed");

        request.Decision.Should().Be("Approved");
        request.Comments.Should().Be("All checks passed");
    }

    [Fact]
    public void OrganisationResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var response = new OrganisationResponse(id, "My Org", "REG001", createdAt);

        response.Id.Should().Be(id);
        response.Name.Should().Be("My Org");
        response.RegistrationNumber.Should().Be("REG001");
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void MemberResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedAt = DateTime.UtcNow.AddDays(-1);
        var acceptedAt = DateTime.UtcNow;

        var response = new MemberResponse(
            id, userId, "user@test.com", "Admin", "Active", invitedAt, acceptedAt);

        response.Id.Should().Be(id);
        response.UserId.Should().Be(userId);
        response.Email.Should().Be("user@test.com");
        response.Role.Should().Be("Admin");
        response.Status.Should().Be("Active");
        response.InvitedAt.Should().Be(invitedAt);
        response.AcceptedAt.Should().Be(acceptedAt);
    }

    [Fact]
    public void ApprovalPolicyResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();

        var response = new ApprovalPolicyResponse(id, 3, 10000m);

        response.Id.Should().Be(id);
        response.RequiredApprovals.Should().Be(3);
        response.MonetaryThreshold.Should().Be(10000m);
    }

    [Fact]
    public void PaymentBatchResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var submittedAt = DateTime.UtcNow.AddMinutes(5);
        var executedAt = DateTime.UtcNow.AddMinutes(10);

        var response = new PaymentBatchResponse(
            id, orgId, userId, "Executed", "NZD", 5000m, 10, createdAt, submittedAt, executedAt);

        response.Id.Should().Be(id);
        response.OrganisationId.Should().Be(orgId);
        response.SubmittedByUserId.Should().Be(userId);
        response.Status.Should().Be("Executed");
        response.Currency.Should().Be("NZD");
        response.TotalAmount.Should().Be(5000m);
        response.ItemCount.Should().Be(10);
        response.CreatedAt.Should().Be(createdAt);
        response.SubmittedAt.Should().Be(submittedAt);
        response.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void PaymentBatchDetailResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "Vendor", "ACC1", 1000m, "Desc")
        };
        var approvals = new List<ApprovalRecordResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Approved", "OK", DateTime.UtcNow)
        };

        var response = new PaymentBatchDetailResponse(
            id, orgId, userId, "Approved", "USD", 1000m, 1,
            DateTime.UtcNow, DateTime.UtcNow, null, items, approvals);

        response.Id.Should().Be(id);
        response.Items.Should().HaveCount(1);
        response.Approvals.Should().HaveCount(1);
    }

    [Fact]
    public void ApprovalRecordResponse_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var decidedAt = DateTime.UtcNow;

        var response = new ApprovalRecordResponse(id, approvedBy, "Rejected", "Too risky", decidedAt);

        response.Id.Should().Be(id);
        response.ApprovedByUserId.Should().Be(approvedBy);
        response.Decision.Should().Be("Rejected");
        response.Comments.Should().Be("Too risky");
        response.DecidedAt.Should().Be(decidedAt);
    }

    #endregion

    #region VirtualCardDto Tests

    [Fact]
    public void VirtualCardDto_CanBeCreated()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var dto = new VirtualCardDto(id, userId, "My Card", "1234", 12, 2025, "Active", createdAt);

        dto.Id.Should().Be(id);
        dto.UserId.Should().Be(userId);
        dto.Nickname.Should().Be("My Card");
        dto.Last4.Should().Be("1234");
        dto.ExpiryMonth.Should().Be(12);
        dto.ExpiryYear.Should().Be(2025);
        dto.Status.Should().Be("Active");
        dto.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void VirtualCardDto_SupportsEquality()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var dto1 = new VirtualCardDto(id, userId, "Card", "9999", 6, 2026, "Active", createdAt);
        var dto2 = new VirtualCardDto(id, userId, "Card", "9999", 6, 2026, "Active", createdAt);

        dto1.Should().Be(dto2);
        dto1.GetHashCode().Should().Be(dto2.GetHashCode());
    }

    [Fact]
    public void CardCreateResultDto_CanBeCreated()
    {
        var card = new VirtualCardDto(
            Guid.NewGuid(), Guid.NewGuid(), "New Card", "5678", 3, 2027, "Active", DateTimeOffset.UtcNow);

        var result = new CardCreateResultDto(card, "4111111111115678", "123");

        result.Card.Should().NotBeNull();
        result.CardNumber.Should().Be("4111111111115678");
        result.Cvv.Should().Be("123");
    }

    [Fact]
    public void CardCreateResultDto_SupportsDeconstruction()
    {
        var card = new VirtualCardDto(
            Guid.NewGuid(), Guid.NewGuid(), "Test", "0000", 1, 2028, "Active", DateTimeOffset.UtcNow);
        var result = new CardCreateResultDto(card, "4000000000000000", "999");

        var (c, num, cvv) = result;

        c.Should().NotBeNull();
        num.Should().Be("4000000000000000");
        cvv.Should().Be("999");
    }

    #endregion

    #region PaymentBatchesController Additional Tests

    private CorporateDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<CorporateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CorporateDbContext(options);
    }

    private PaymentBatchesController BuildController(
        CorporateDbContext db,
        Mock<IPublishEndpoint>? publisher = null,
        Mock<IHttpClientFactory>? httpFactory = null,
        Guid? organisationId = null,
        string? role = "Admin")
    {
        publisher ??= new Mock<IPublishEndpoint>();
        httpFactory ??= new Mock<IHttpClientFactory>();
        var workflow = new Mock<IApprovalWorkflowService>();
        var logger = new Mock<ILogger<PaymentBatchesController>>();

        var controller = new PaymentBatchesController(
            db, workflow.Object, publisher.Object, httpFactory.Object, logger.Object);

        var claims = new List<Claim>
        {
            new Claim("sub", Guid.NewGuid().ToString())
        };

        if (organisationId.HasValue)
        {
            claims.Add(new Claim("organisation_id", organisationId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim("organisation_role", role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        controller.HttpContext.Request.Headers["Authorization"] = "Bearer test-token";

        return controller;
    }

    [Fact]
    public async Task GetBatches_ReturnsForbid_WhenNoOrganisationId()
    {
        var db = BuildDb();
        var controller = BuildController(db, organisationId: null);

        var result = await controller.GetBatches();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBatches_ReturnsEmptyList_WhenNoBatches()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.GetBatches();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var batches = ok.Value as IEnumerable<PaymentBatchResponse>;
        batches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatches_ReturnsBatches_ForOrganisation()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();

        db.PaymentBatches.Add(new PaymentBatch
        {
            Id = Guid.NewGuid(),
            OrganisationId = orgId,
            SubmittedByUserId = Guid.NewGuid(),
            Status = "Draft",
            Currency = "USD",
            TotalAmount = 1000m,
            ItemCount = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.GetBatches();

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var batches = (ok.Value as IEnumerable<PaymentBatchResponse>)?.ToList();
        batches.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBatch_ReturnsForbid_WhenNoOrganisationId()
    {
        var db = BuildDb();
        var controller = BuildController(db, organisationId: null);

        var result = await controller.GetBatch(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBatch_ReturnsNotFound_WhenBatchDoesNotExist()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.GetBatch(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBatch_ReturnsBatchDetails()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var batch = new PaymentBatch
        {
            Id = batchId,
            OrganisationId = orgId,
            SubmittedByUserId = Guid.NewGuid(),
            Status = "Draft",
            Currency = "NZD",
            TotalAmount = 500m,
            ItemCount = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.PaymentBatches.Add(batch);

        db.PaymentBatchItems.Add(new PaymentBatchItem
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batchId,
            SourceAccountId = Guid.NewGuid(),
            PayeeName = "Test Payee",
            Amount = 500m
        });

        await db.SaveChangesAsync();

        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.GetBatch(batchId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var detail = ok.Value as PaymentBatchDetailResponse;
        detail.Should().NotBeNull();
        detail!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateBatch_ReturnsForbid_WhenNoOrganisationId()
    {
        var db = BuildDb();
        var controller = BuildController(db, organisationId: null);

        var request = new CreatePaymentBatchRequest("USD", new List<PaymentBatchItemDto>());

        var result = await controller.CreateBatch(request);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateBatch_ReturnsForbid_WhenNotInAllowedRole()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var controller = BuildController(db, organisationId: orgId, role: "Viewer");

        var request = new CreatePaymentBatchRequest("USD", new List<PaymentBatchItemDto>());

        var result = await controller.CreateBatch(request);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsForbid_WhenNoOrganisationId()
    {
        var db = BuildDb();
        var controller = BuildController(db, organisationId: null);

        var result = await controller.SubmitForApproval(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsNotFound_WhenBatchDoesNotExist()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.SubmitForApproval(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsBadRequest_WhenBatchNotDraft()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        db.PaymentBatches.Add(new PaymentBatch
        {
            Id = batchId,
            OrganisationId = orgId,
            SubmittedByUserId = Guid.NewGuid(),
            Status = "PendingApproval", // Not Draft
            Currency = "USD",
            TotalAmount = 1000m,
            ItemCount = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.SubmitForApproval(batchId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsForbid_WhenNoOrganisationId()
    {
        var db = BuildDb();
        var controller = BuildController(db, organisationId: null);

        var result = await controller.ExecuteBatch(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsNotFound_WhenBatchDoesNotExist()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.ExecuteBatch(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsBadRequest_WhenBatchNotApproved()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        db.PaymentBatches.Add(new PaymentBatch
        {
            Id = batchId,
            OrganisationId = orgId,
            SubmittedByUserId = Guid.NewGuid(),
            Status = "PendingApproval", // Not Approved
            Currency = "USD",
            TotalAmount = 1000m,
            ItemCount = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, organisationId: orgId);

        var result = await controller.ExecuteBatch(batchId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
