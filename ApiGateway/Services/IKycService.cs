using ApiGateway.Models;

namespace ApiGateway.Services;

public interface IKycService
{
    Task<KycStatus> ValidateAsync(Guid userId, CancellationToken ct = default);
}
