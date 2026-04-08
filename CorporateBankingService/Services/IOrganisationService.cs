using CorporateBankingService.Models.Dtos;

namespace CorporateBankingService.Services;

public interface IOrganisationService
{
    Task<object> CreateOrganisationAsync(Guid userId, string email, CreateOrganisationRequest request);
    Task<object?> GetOrganisationAsync(Guid organisationId, Guid callerOrgId);
    Task<object> GetMembersAsync(Guid organisationId, Guid callerOrgId);
    Task<object> InviteMemberAsync(Guid organisationId, Guid callerOrgId, string callerRole, InviteMemberRequest request);
    Task<object?> AssignRoleAsync(Guid organisationId, Guid callerOrgId, string callerRole, Guid memberId, AssignRoleRequest request);
    Task<object> GetPoliciesAsync(Guid organisationId, Guid callerOrgId);
    Task<object> CreatePolicyAsync(Guid organisationId, Guid callerOrgId, string callerRole, CreateApprovalPolicyRequest request);
}
