namespace ApiGateway.Services;

public record BankInfo(string Id, string Name, string Logo, string Country);

public interface IBankProvider
{
    Task<IReadOnlyList<BankInfo>> GetAvailableBanksAsync(string? country = null);
    Task<BankInfo?> GetBankByIdAsync(string bankId);
}
