namespace ApiGateway.Models;

public class SanctionAuditLog
{
    public Guid Id { get; set; }
    public Guid SanctionRequestId { get; set; }
    public SanctionStatus FromStatus { get; set; }
    public SanctionStatus ToStatus { get; set; }
    public string ChangedBy { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public string CorrelationId { get; set; } = null!;

    public SanctionRequest SanctionRequest { get; set; } = null!;
}
