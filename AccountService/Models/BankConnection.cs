namespace AccountService.Models;

/// <summary>
/// Represents a connection to an external third-party bank via Open Banking.
/// </summary>
public class BankConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BankId { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string BankLogo { get; set; } = null!;
    public string Status { get; set; } = "Active";
    public string? AccessToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents an external bank account retrieved from a connected bank.
/// </summary>
public class ExternalBankAccount
{
    public Guid Id { get; set; }
    public Guid BankConnectionId { get; set; }
    public Guid UserId { get; set; }
    public string ExternalAccountId { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NZD";
    public DateTime LastSyncedAt { get; set; }
    
    public BankConnection? BankConnection { get; set; }
}
