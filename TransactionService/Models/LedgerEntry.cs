namespace TransactionService.Models;

/// <summary>
/// Represents a single ledger entry in double-entry bookkeeping.
/// Every money movement produces exactly two entries: a debit on the source and a credit on the destination.
/// Account balances are derived by summing all ledger entries for that account.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; set; }

    /// <summary>
    /// The transaction this entry belongs to.
    /// Each transaction must have exactly two entries (debit + credit).
    /// </summary>
    public Guid TransactionId { get; set; }

    /// <summary>
    /// The account being debited or credited.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// The type of entry: "debit" (decrease) or "credit" (increase).
    /// </summary>
    public string EntryType { get; set; } = null!;

    /// <summary>
    /// The amount of the entry. Always positive.
    /// Sign is determined by EntryType.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The currency code (ISO 4217).
    /// </summary>
    public string Currency { get; set; } = null!;

    /// <summary>
    /// When this entry was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
