namespace Contracts.Events;

public record OrganisationCreated(
    Guid OrganisationId,
    string Name,
    string RegistrationNumber,
    Guid CreatedByUserId,
    DateTime CreatedAt
);
