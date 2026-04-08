namespace Contracts.Events;

public record ExternalBankDepositInitiated(
    Guid DepositId,
    Guid AccountId,
    Guid UserId,
    Guid ExternalBankAccountId,
    decimal Amount,
    string Currency,
    DateTime InitiatedAt,
    ClientType ClientType = ClientType.Individual,
    Guid? OrganisationId = null
);
