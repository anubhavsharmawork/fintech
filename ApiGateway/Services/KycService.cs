using ApiGateway.Models;

namespace ApiGateway.Services;

public class KycService : IKycService
{
    private readonly ILogger<KycService> _logger;

    public KycService(ILogger<KycService> logger)
    {
        _logger = logger;
    }

    public Task<KycStatus> ValidateAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Running KYC validation for user {UserId}", userId);

        // Stub: in a real system this would call an external KYC provider.
        // All users pass KYC by default for the demo implementation.
        return Task.FromResult(KycStatus.Passed);
    }
}
