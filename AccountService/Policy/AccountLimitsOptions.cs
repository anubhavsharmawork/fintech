namespace AccountService.Policy;

public sealed class AccountLimitsOptions
{
    public const string SectionName = "AccountLimits";

    public ClientTypeLimits Individual { get; set; } = new();
    public ClientTypeLimits Corporate { get; set; } = new();
}

public sealed class ClientTypeLimits
{
    /// <summary>Maximum number of active internal ledger accounts.</summary>
    public int MaxAccounts { get; set; }

    /// <summary>Maximum number of active external bank connections.</summary>
    public int MaxBankConnections { get; set; }
}
