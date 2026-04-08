namespace Contracts.Events;

public record BatchExecuted(
    Guid BatchId,
    Guid OrganisationId,
    int TransactionCount,
    decimal TotalAmount,
    string Currency,
    DateTime ExecutedAt
);
