namespace Contracts.Events;

public record PaymentBatchSubmittedForApproval(
    Guid BatchId,
    Guid OrganisationId,
    Guid SubmittedByUserId,
    int ItemCount,
    decimal TotalAmount,
    string Currency,
    DateTime SubmittedAt
);
