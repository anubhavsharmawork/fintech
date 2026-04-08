namespace TransactionService.Models;

public class SuspiciousActivityReport
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Reason { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime FlaggedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
}
