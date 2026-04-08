using Contracts.Events;
using CorporateBankingService.Data;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CorporateBankingService.Services;

public class OrganisationService : IOrganisationService
{
    private readonly CorporateDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrganisationService> _logger;

    public OrganisationService(
        CorporateDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<OrganisationService> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    private bool IsCallerInRole(string callerRole, string role)
    {
        return string.Equals(callerRole, role, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<object> CreateOrganisationAsync(Guid userId, string email, CreateOrganisationRequest request)
    {
        if (await _context.Organisations.AnyAsync(o => o.RegistrationNumber == request.RegistrationNumber))
            throw new InvalidOperationException("An organisation with this registration number already exists.");

        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            RegistrationNumber = request.RegistrationNumber.Trim(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Organisations.Add(org);

        var adminMember = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            UserId = userId,
            Email = email ?? "",
            Role = "Admin",
            Status = "Active",
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        };
        _context.OrganisationMembers.Add(adminMember);

        var defaultPolicy = new ApprovalPolicy
        {
            Id = Guid.NewGuid(),
            OrganisationId = org.Id,
            RequiredApprovals = 1,
            MonetaryThreshold = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ApprovalPolicies.Add(defaultPolicy);

        await _context.SaveChangesAsync();

        await _publishEndpoint.Publish(new OrganisationCreated(
            org.Id, org.Name, org.RegistrationNumber, userId, org.CreatedAt));

        _logger.LogInformation("Organisation created: {OrgId} by user {UserId}", org.Id, userId);

        return new OrganisationResponse(org.Id, org.Name, org.RegistrationNumber, org.CreatedAt);
    }

    public async Task<object?> GetOrganisationAsync(Guid organisationId, Guid callerOrgId)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();
        var org = await _context.Organisations.FindAsync(organisationId);
        if (org is null) return null;
        return new OrganisationResponse(org.Id, org.Name, org.RegistrationNumber, org.CreatedAt);
    }

    public async Task<object> GetMembersAsync(Guid organisationId, Guid callerOrgId)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();
        var members = await _context.OrganisationMembers
            .Where(m => m.OrganisationId == organisationId)
            .OrderBy(m => m.InvitedAt)
            .ToListAsync();
        return members.Select(m => new MemberResponse(m.Id, m.UserId, m.Email, m.Role, m.Status, m.InvitedAt, m.AcceptedAt));
    }

    public async Task<object> InviteMemberAsync(Guid organisationId, Guid callerOrgId, string callerRole, InviteMemberRequest request)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();
        if (!IsCallerInRole(callerRole, "Admin")) throw new UnauthorizedAccessException();

        if (await _context.OrganisationMembers.AnyAsync(m => m.OrganisationId == organisationId && m.Email == request.Email && m.Status != "Removed"))
            throw new InvalidOperationException("Member already exists in this organisation.");

        var member = new OrganisationMember
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = Guid.Empty,
            Email = request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            Status = "Invited",
            InvitedAt = DateTime.UtcNow
        };

        _context.OrganisationMembers.Add(member);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Member invited: {Email} to org {OrgId} with role {Role}", request.Email, organisationId, request.Role);

        return new MemberResponse(member.Id, member.UserId, member.Email, member.Role, member.Status, member.InvitedAt, member.AcceptedAt);
    }

    public async Task<object?> AssignRoleAsync(Guid organisationId, Guid callerOrgId, string callerRole, Guid memberId, AssignRoleRequest request)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();
        if (!IsCallerInRole(callerRole, "Admin")) throw new UnauthorizedAccessException();

        var member = await _context.OrganisationMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == organisationId);
        if (member is null) return null;

        member.Role = request.Role;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Member {MemberId} role changed to {Role} in org {OrgId}", memberId, request.Role, organisationId);

        return new MemberResponse(member.Id, member.UserId, member.Email, member.Role, member.Status, member.InvitedAt, member.AcceptedAt);
    }

    public async Task<object> GetPoliciesAsync(Guid organisationId, Guid callerOrgId)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();

        var policies = await _context.ApprovalPolicies
            .Where(p => p.OrganisationId == organisationId)
            .ToListAsync();

        return policies.Select(p => new ApprovalPolicyResponse(p.Id, p.RequiredApprovals, p.MonetaryThreshold));
    }

    public async Task<object> CreatePolicyAsync(Guid organisationId, Guid callerOrgId, string callerRole, CreateApprovalPolicyRequest request)
    {
        if (callerOrgId != organisationId) throw new UnauthorizedAccessException();
        if (!IsCallerInRole(callerRole, "Admin")) throw new UnauthorizedAccessException();

        var policy = new ApprovalPolicy
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            RequiredApprovals = request.RequiredApprovals,
            MonetaryThreshold = request.MonetaryThreshold,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ApprovalPolicies.Add(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Approval policy created for org {OrgId}: {RequiredApprovals} approvals, threshold {Threshold}",
            organisationId, request.RequiredApprovals, request.MonetaryThreshold);

        return new ApprovalPolicyResponse(policy.Id, policy.RequiredApprovals, policy.MonetaryThreshold);
    }
}
