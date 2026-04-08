namespace ApiGateway.Models;

public class CreditFacility
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string WalletAddress { get; set; } = null!;
    public decimal CreditLimit { get; set; }
    public decimal DrawnAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string Currency { get; set; } = "FTK";
    public CreditFacilityStatus Status { get; set; } = CreditFacilityStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CreditRepayment> Repayments { get; set; } = new List<CreditRepayment>();
}
