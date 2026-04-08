namespace ApiGateway.Models.Dtos;

public record SanctionRequestDto(
    Guid Id,
    string ExternalProjectId,
    string ExternalTenantId,
    Guid UserId,
    Guid AccountId,
    decimal RequestedAmount,
    string Currency,
    string Purpose,
    int RiskScore,
    string KycStatus,
    string AmlStatus,
    string Status,
    decimal? ApprovedAmount,
    string? DecisionReason,
    string? FtkTransactionRef,
    string IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CreatedBy
);
