namespace CorporateBankingService.Models.Dtos;

public record CreateOrganisationRequest(string Name, string RegistrationNumber);

public record InviteMemberRequest(string Email, string Role);

public record AssignRoleRequest(string Role);

public record CreateApprovalPolicyRequest(int RequiredApprovals, decimal? MonetaryThreshold);

public record CreatePaymentBatchRequest(string Currency, List<PaymentBatchItemDto> Items);

public record PaymentBatchItemDto(Guid SourceAccountId, string PayeeName, string? PayeeAccountNumber, decimal Amount, string? Description);

public record ApprovalDecisionRequest(string Decision, string? Comments);

public record OrganisationResponse(Guid Id, string Name, string RegistrationNumber, DateTime CreatedAt);

public record MemberResponse(Guid Id, Guid UserId, string Email, string Role, string Status, DateTime InvitedAt, DateTime? AcceptedAt);

public record ApprovalPolicyResponse(Guid Id, int RequiredApprovals, decimal? MonetaryThreshold);

public record PaymentBatchResponse(
    Guid Id,
    Guid OrganisationId,
    Guid SubmittedByUserId,
    string Status,
    string Currency,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ExecutedAt);

public record PaymentBatchDetailResponse(
    Guid Id,
    Guid OrganisationId,
    Guid SubmittedByUserId,
    string Status,
    string Currency,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ExecutedAt,
    List<PaymentBatchItemDto> Items,
    List<ApprovalRecordResponse> Approvals);

public record ApprovalRecordResponse(Guid Id, Guid ApprovedByUserId, string Decision, string? Comments, DateTime DecidedAt);
