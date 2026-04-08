namespace CorporateBankingService.Models;

public class PaymentBatchItem
{
    public Guid Id { get; set; }
    public Guid PaymentBatchId { get; set; }
    public Guid SourceAccountId { get; set; }
    public string PayeeName { get; set; } = null!;
    public string? PayeeAccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }

    public PaymentBatch PaymentBatch { get; set; } = null!;
}
