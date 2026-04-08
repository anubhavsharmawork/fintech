namespace CorporateBankingService.Models;

public class ApprovalPolicy
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public int RequiredApprovals { get; set; } = 1;
    public decimal? MonetaryThreshold { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Organisation Organisation { get; set; } = null!;
}
