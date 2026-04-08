namespace Contracts.Events;

public record TransactionCreated(
    Guid TransactionId,
    Guid AccountId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Type,
    DateTime CreatedAt,
    ClientType ClientType = ClientType.Individual,
    Guid? OrganisationId = null,
    Guid? PaymentBatchId = null
);
