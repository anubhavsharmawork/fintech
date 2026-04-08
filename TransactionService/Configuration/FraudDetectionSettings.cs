namespace TransactionService.Configuration;

public class FraudDetectionSettings
{
    public const string SectionName = "FraudDetection";

    public decimal LargeTransactionThreshold { get; set; } = 10_000m;
    public decimal RapidDebitThreshold { get; set; } = 3_000m;
    public int RapidTransactionCount { get; set; } = 3;
    public int RapidTransactionWindowMinutes { get; set; } = 60;
    public string[] FlaggedKeywords { get; set; } = ["casino", "crypto", "anonymous", "offshore"];
}
