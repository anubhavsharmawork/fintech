namespace ApiGateway.Services;

public class MockBankProvider : IBankProvider
{
    private static readonly IReadOnlyList<BankInfo> Banks = new List<BankInfo>
    {
        new("nz_anz", "ANZ New Zealand", "🏦", "NZ"),
        new("nz_asb", "ASB Bank", "🏛️", "NZ"),
        new("nz_bnz", "BNZ", "🏦", "NZ"),
        new("nz_westpac", "Westpac NZ", "🔴", "NZ"),
        new("nz_kiwibank", "Kiwibank", "🥝", "NZ"),
        new("au_commbank", "CommBank", "🟡", "AU"),
        new("au_nab", "NAB", "🔴", "AU"),
        new("au_westpac", "Westpac AU", "🔴", "AU"),
        new("uk_hsbc", "HSBC UK", "🔴", "UK"),
        new("uk_barclays", "Barclays", "🔵", "UK")
    };

    public Task<IReadOnlyList<BankInfo>> GetAvailableBanksAsync(string? country = null)
    {
        IReadOnlyList<BankInfo> result = string.IsNullOrWhiteSpace(country)
            ? Banks
            : Banks.Where(b => b.Country.Equals(country, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult(result);
    }

    public Task<BankInfo?> GetBankByIdAsync(string bankId)
    {
        var bank = Banks.FirstOrDefault(b => b.Id == bankId);
        return Task.FromResult(bank);
    }
}
