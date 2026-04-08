using System;

namespace TransactionService.Models.Dtos;

public class CreatePaymentRequestDto
{
    public Guid AccountId { get; set; }

    /// <summary>
    /// The destination account for double-entry bookkeeping.
    /// If null, defaults to AccountId for single-leg transactions.
    /// </summary>
    public Guid? DestinationAccountId { get; set; }

    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "NZD";
    public string PayeeName { get; set; } = string.Empty;
    public string? PayeeAccountNumber { get; set; }
    public string? Description { get; set; }
    public string? SpendingType { get; set; } = "Fun";
    public string? TxHash { get; set; }
}
