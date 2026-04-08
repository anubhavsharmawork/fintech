using CorporateBankingService.Models.Dtos;

namespace CorporateBankingService.Services;

public interface IApprovalService
{
    Task<object> GetPendingBatchesAsync(Guid orgId, string callerRole);
    Task<object?> GetBatchDetailAsync(Guid batchId, Guid orgId);
    Task<object?> DecideAsync(Guid batchId, Guid orgId, Guid userId, string callerRole, ApprovalDecisionRequest request);
}
