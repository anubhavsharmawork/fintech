namespace CorporateBankingService.Models;

public class ApprovalRecord
{
    public Guid Id { get; set; }
    public Guid PaymentBatchId { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public string Decision { get; set; } = null!; // Approved, Rejected
    public string? Comments { get; set; }
    public DateTime DecidedAt { get; set; }

    public PaymentBatch PaymentBatch { get; set; } = null!;
}
