namespace CorporateBankingService.Models;

public class PaymentBatch
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, PendingApproval, Approved, Executed, Rejected
    public string Currency { get; set; } = "USD";
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }

    public Organisation Organisation { get; set; } = null!;
    public ICollection<PaymentBatchItem> Items { get; set; } = new List<PaymentBatchItem>();
    public ICollection<ApprovalRecord> Approvals { get; set; } = new List<ApprovalRecord>();
}
