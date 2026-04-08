namespace Contracts.Events;

public record SuspiciousActivityFlagged(
    Guid Id,
    Guid TransactionId,
    Guid UserId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Reason,
    string RiskLevel,
    DateTime FlaggedAt,
    ClientType ClientType = ClientType.Individual,
    Guid? OrganisationId = null
);
