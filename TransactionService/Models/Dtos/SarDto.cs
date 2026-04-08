namespace TransactionService.Models.Dtos;

public class SarSummaryDto
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime FlaggedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateSarDto
{
    public Guid TransactionId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
}
