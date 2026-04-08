using ApiGateway.Models;

namespace ApiGateway.Services;

public interface IAmlService
{
    Task<AmlStatus> ScreenAsync(Guid userId, decimal amount, string purpose, CancellationToken ct = default);
}
