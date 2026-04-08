namespace ApiGateway.Models.Dtos;

public record CreateSanctionRequestDto(
    string ExternalProjectId,
    string ExternalTenantId,
    Guid UserId,
    Guid AccountId,
    decimal RequestedAmount,
    string? Currency,
    string Purpose,
    string IdempotencyKey
);
