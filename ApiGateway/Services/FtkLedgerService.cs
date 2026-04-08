namespace ApiGateway.Services;

public class FtkLedgerService : IFtkLedgerService
{
    private readonly ILogger<FtkLedgerService> _logger;

    public FtkLedgerService(ILogger<FtkLedgerService> logger)
    {
        _logger = logger;
    }

    public Task<string> AllocateAsync(Guid userId, Guid accountId, decimal amount, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Allocating {Amount} FTK to user {UserId} account {AccountId}",
            amount, userId, accountId);

        // Stub: in a real system this would call the blockchain/FTK token contract
        // to mint or transfer FTK tokens into the user's wallet/account.
        var transactionRef = $"FTK-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "FTK allocation complete. TransactionRef={TransactionRef}", transactionRef);

        return Task.FromResult(transactionRef);
    }
}
