namespace Contracts.Events;

public record RepaymentCompleted(
    Guid RepaymentId,
    Guid FacilityId,
    Guid UserId,
    string WalletAddress,
    decimal Amount,
    string Currency,
    decimal OutstandingBalance,
    string Status,
    DateTime CompletedAt
);
