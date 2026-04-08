namespace ApiGateway.Models;

public class SanctionRequest
{
    public Guid Id { get; set; }
    public string ExternalProjectId { get; set; } = null!;
    public string ExternalTenantId { get; set; } = null!;
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = "FTK";
    public string Purpose { get; set; } = null!;
    public int RiskScore { get; set; }
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;
    public AmlStatus AmlStatus { get; set; } = AmlStatus.Pending;
    public SanctionStatus Status { get; set; } = SanctionStatus.Draft;
    public decimal? ApprovedAmount { get; set; }
    public string? DecisionReason { get; set; }
    public string? FtkTransactionRef { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;

    public ICollection<SanctionAuditLog> AuditLogs { get; set; } = new List<SanctionAuditLog>();
}
