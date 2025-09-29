namespace Contracts.Events;

public record TransactionCreated(
    Guid TransactionId,
    Guid AccountId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Type,
    DateTime CreatedAt
);
