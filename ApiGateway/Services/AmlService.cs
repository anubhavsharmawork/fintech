using ApiGateway.Models;

namespace ApiGateway.Services;

public class AmlService : IAmlService
{
    private readonly ILogger<AmlService> _logger;

    public AmlService(ILogger<AmlService> logger)
    {
        _logger = logger;
    }

    public Task<AmlStatus> ScreenAsync(Guid userId, decimal amount, string purpose, CancellationToken ct = default)
    {
        _logger.LogInformation("Running AML screening for user {UserId}, amount {Amount}", userId, amount);

        // Stub: in a real system this would call an external AML screening provider.
        // All requests pass AML by default for the demo implementation.
        return Task.FromResult(AmlStatus.Passed);
    }
}
