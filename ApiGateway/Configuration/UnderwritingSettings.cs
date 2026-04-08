namespace ApiGateway.Configuration;

public class UnderwritingSettings
{
    public const string SectionName = "Underwriting";

    public decimal AmountThreshold { get; set; } = 50_000m;
    public int AmountThresholdRiskPoints { get; set; } = 30;
    public int KycPassedRiskReduction { get; set; } = 10;
    public int AmlPassedRiskReduction { get; set; } = 10;
    public int NoPriorApprovalsRiskPoints { get; set; } = 10;
    public int MaxAcceptableRiskScore { get; set; } = 60;
    public decimal PartialApprovalDiscountPercent { get; set; } = 0m;
}
