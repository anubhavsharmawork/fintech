namespace Contracts.Events;

public record PaymentApproved(
    Guid BatchId,
    Guid OrganisationId,
    Guid ApprovedByUserId,
    DateTime ApprovedAt
);
