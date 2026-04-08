using ApiGateway.Models;
using ApiGateway.Models.Dtos;

namespace ApiGateway.Services;

public interface ISanctioningService
{
    Task<SanctionRequest> CreateSanctionRequestAsync(CreateSanctionRequestDto dto, string createdBy, CancellationToken ct = default);
    Task<SanctionRequest> RunScreeningAsync(Guid sanctionRequestId, string changedBy, CancellationToken ct = default);
    Task<SanctionRequest> RunUnderwritingAsync(Guid sanctionRequestId, string changedBy, CancellationToken ct = default);
    Task<SanctionRequest> DisburseToFtkAsync(Guid sanctionRequestId, string changedBy, CancellationToken ct = default);
    Task<SanctionRequest> RejectRequestAsync(Guid sanctionRequestId, string reason, string changedBy, CancellationToken ct = default);
    Task<SanctionRequest> CancelRequestAsync(Guid sanctionRequestId, string reason, string changedBy, CancellationToken ct = default);
    Task<SanctionRequest?> GetSanctionStatusAsync(string externalProjectId, Guid userId, CancellationToken ct = default);
    Task<SanctionRequest?> GetByIdAsync(Guid sanctionRequestId, CancellationToken ct = default);
    Task<List<SanctionAuditLog>> GetAuditLogsAsync(Guid sanctionRequestId, CancellationToken ct = default);
    Task<List<SanctionRequest>> GetAllAsync(CancellationToken ct = default);
}
