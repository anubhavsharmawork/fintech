using Microsoft.EntityFrameworkCore;
using CorporateBankingService.Data;
using CorporateBankingService.Models;

namespace CorporateBankingService.Services;

public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly CorporateDbContext _context;
    private readonly ILogger<ApprovalWorkflowService> _logger;

    public ApprovalWorkflowService(CorporateDbContext context, ILogger<ApprovalWorkflowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApprovalPolicy?> GetApplicablePolicyAsync(Guid organisationId, decimal amount)
    {
        var policies = await _context.ApprovalPolicies
            .Where(p => p.OrganisationId == organisationId)
            .OrderByDescending(p => p.MonetaryThreshold ?? 0)
            .ToListAsync();

        foreach (var policy in policies)
        {
            if (policy.MonetaryThreshold.HasValue && amount >= policy.MonetaryThreshold.Value)
                return policy;
        }

        return policies.LastOrDefault();
    }

    public async Task<bool> HasSufficientApprovalsAsync(Guid organisationId, PaymentBatch batch)
    {
        var policy = await GetApplicablePolicyAsync(organisationId, batch.TotalAmount);
        if (policy is null)
        {
            _logger.LogWarning("No approval policy found for organisation {OrganisationId}; defaulting to 1 required", organisationId);
            return batch.Approvals.Count(a => a.Decision == "Approved") >= 1;
        }

        var approvedCount = batch.Approvals.Count(a => a.Decision == "Approved");
        return approvedCount >= policy.RequiredApprovals;
    }
}
