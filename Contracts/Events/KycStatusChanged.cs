namespace Contracts.Events;

public record KycStatusChanged(
    Guid UserId,
    string Status,
    string Notes,
    DateTime ChangedAt
);
