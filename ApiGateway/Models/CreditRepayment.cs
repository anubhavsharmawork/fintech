namespace ApiGateway.Models;

public class CreditRepayment
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "FTK";
    public string Status { get; set; } = "Completed";
    public DateTimeOffset CreatedAt { get; set; }

    public CreditFacility Facility { get; set; } = null!;
}
