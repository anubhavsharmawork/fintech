namespace ApiGateway.Services;

public interface IFtkLedgerService
{
    Task<string> AllocateAsync(Guid userId, Guid accountId, decimal amount, CancellationToken ct = default);
}
