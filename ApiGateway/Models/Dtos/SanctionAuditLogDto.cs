namespace ApiGateway.Models.Dtos;

public record SanctionAuditLogDto(
    Guid Id,
    Guid SanctionRequestId,
    string FromStatus,
    string ToStatus,
    string ChangedBy,
    string Reason,
    DateTimeOffset Timestamp,
    string CorrelationId
);
