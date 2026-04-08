using CorporateBankingService.Models;

namespace CorporateBankingService.Services;

public interface IApprovalWorkflowService
{
    Task<bool> HasSufficientApprovalsAsync(Guid organisationId, PaymentBatch batch);
    Task<ApprovalPolicy?> GetApplicablePolicyAsync(Guid organisationId, decimal amount);
}
