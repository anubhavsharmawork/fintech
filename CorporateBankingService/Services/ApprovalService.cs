using Contracts.Events;
using CorporateBankingService.Data;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CorporateBankingService.Services;

public class ApprovalService : IApprovalService
{
    private readonly CorporateDbContext _context;
    private readonly IApprovalWorkflowService _approvalWorkflow;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        CorporateDbContext context,
        IApprovalWorkflowService approvalWorkflow,
        IPublishEndpoint publishEndpoint,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _approvalWorkflow = approvalWorkflow;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    private bool IsCallerInRole(string callerRole, params string[] roles)
    {
        return roles.Any(r => string.Equals(callerRole, r, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<object> GetPendingBatchesAsync(Guid orgId, string callerRole)
    {
        if (!IsCallerInRole(callerRole, "Approver", "Admin"))
            throw new UnauthorizedAccessException("User must be Approver or Admin");

        var batches = await _context.PaymentBatches
            .Where(b => b.OrganisationId == orgId && b.Status == "PendingApproval")
            .OrderByDescending(b => b.SubmittedAt)
            .ToListAsync();

        return batches.Select(b => new PaymentBatchResponse(
            b.Id, b.OrganisationId, b.SubmittedByUserId, b.Status, b.Currency,
            b.TotalAmount, b.ItemCount, b.CreatedAt, b.SubmittedAt, b.ExecutedAt));
    }

    public async Task<object?> GetBatchDetailAsync(Guid batchId, Guid orgId)
    {
        var batch = await _context.PaymentBatches
            .Include(b => b.Items)
            .Include(b => b.Approvals)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganisationId == orgId);

        if (batch is null) return null;

        return new PaymentBatchDetailResponse(
            batch.Id, batch.OrganisationId, batch.SubmittedByUserId, batch.Status,
            batch.Currency, batch.TotalAmount, batch.ItemCount, batch.CreatedAt,
            batch.SubmittedAt, batch.ExecutedAt,
            batch.Items.Select(i => new PaymentBatchItemDto(
                i.SourceAccountId, i.PayeeName, i.PayeeAccountNumber, i.Amount, i.Description)).ToList(),
            batch.Approvals.Select(a => new ApprovalRecordResponse(
                a.Id, a.ApprovedByUserId, a.Decision, a.Comments, a.DecidedAt)).ToList());
    }

    public async Task<object?> DecideAsync(Guid batchId, Guid orgId, Guid userId, string callerRole, ApprovalDecisionRequest request)
    {
        if (!IsCallerInRole(callerRole, "Approver", "Admin"))
            throw new UnauthorizedAccessException("User must be Approver or Admin");

        var batch = await _context.PaymentBatches
            .Include(b => b.Approvals)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganisationId == orgId);

        if (batch is null) return null;

        if (batch.Status != "PendingApproval")
            throw new InvalidOperationException("Batch is not pending approval.");

        if (batch.Approvals.Any(a => a.ApprovedByUserId == userId))
            throw new InvalidOperationException("You have already submitted a decision for this batch.");

        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            PaymentBatchId = batchId,
            ApprovedByUserId = userId,
            Decision = request.Decision,
            Comments = request.Comments?.Trim(),
            DecidedAt = DateTime.UtcNow
        };

        _context.ApprovalRecords.Add(record);

        if (string.Equals(request.Decision, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            batch.Status = "Rejected";
            _logger.LogInformation("Batch {BatchId} rejected by user {UserId}", batchId, userId);
        }
        else
        {
            await _publishEndpoint.Publish(new PaymentApproved(
                batchId, orgId, userId, record.DecidedAt));

            batch.Approvals.Add(record);
            if (await _approvalWorkflow.HasSufficientApprovalsAsync(orgId, batch))
            {
                batch.Status = "Approved";
                _logger.LogInformation("Batch {BatchId} fully approved in org {OrgId}", batchId, orgId);
            }
            else
            {
                _logger.LogInformation("Batch {BatchId} approved by {UserId}; awaiting more approvals", batchId, userId);
            }
        }

        await _context.SaveChangesAsync();

        return new ApprovalRecordResponse(record.Id, record.ApprovedByUserId, record.Decision, record.Comments, record.DecidedAt);
    }
}
