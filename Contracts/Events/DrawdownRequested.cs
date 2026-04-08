namespace Contracts.Events;

public record DrawdownRequested(
    Guid FacilityId,
    Guid UserId,
    string WalletAddress,
    decimal Amount,
    string Currency,
    decimal OutstandingBalance,
    DateTime RequestedAt
);
