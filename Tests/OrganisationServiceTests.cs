using CorporateBankingService.Data;
using CorporateBankingService.Models;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using CorporateBankingService.Validation;
using Contracts.Events;
using FluentAssertions;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class OrganisationServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static CorporateDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<CorporateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OrganisationService BuildService(CorporateDbContext db, Mock<IPublishEndpoint>? pub = null)
    {
        pub ??= new Mock<IPublishEndpoint>();
        return new OrganisationService(
            db,
            pub.Object,
            new Mock<ILogger<OrganisationService>>().Object);
    }

    // ── CreateOrganisationAsync ────────────────────────────────────────────

    [Fact]
    public async Task CreateOrganisation_ReturnsOrganisationResponse_AndPublishesEvent()
    {
        var db = BuildDb();
        var pub = new Mock<IPublishEndpoint>();
        var svc = BuildService(db, pub);
        var userId = Guid.NewGuid();

        var result = await svc.CreateOrganisationAsync(userId, "admin@corp.com",
            new CreateOrganisationRequest("Acme Ltd", "REG-001"));

        result.Should().BeOfType<OrganisationResponse>();
        var resp = (OrganisationResponse)result;
        resp.Name.Should().Be("Acme Ltd");
        resp.RegistrationNumber.Should().Be("REG-001");

        pub.Verify(p => p.Publish(It.IsAny<OrganisationCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrganisation_SeedsAdminMemberAndDefaultPolicy()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        var svc = BuildService(db);

        await svc.CreateOrganisationAsync(userId, "admin@corp.com",
            new CreateOrganisationRequest("Corp", "REG-002"));

        db.OrganisationMembers.Should().ContainSingle(m => m.UserId == userId && m.Role == "Admin");
        db.ApprovalPolicies.Should().ContainSingle(p => p.RequiredApprovals == 1);
    }

    [Fact]
    public async Task CreateOrganisation_Throws_WhenRegistrationNumberAlreadyExists()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com", new CreateOrganisationRequest("A", "REG-DUP"));

        var act = async () => await svc.CreateOrganisationAsync(Guid.NewGuid(), "b@b.com",
            new CreateOrganisationRequest("B", "REG-DUP"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*registration number already exists*");
    }

    // ── GetOrganisationAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetOrganisation_ReturnsOrganisationResponse_WhenCallerMatches()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("CorpX", "REG-X")));

        var result = await svc.GetOrganisationAsync(resp.Id, resp.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrganisation_ReturnsNull_WhenOrgNotFound()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var orgId = Guid.NewGuid();

        var result = await svc.GetOrganisationAsync(orgId, orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganisation_Throws_WhenCallerOrgDiffers()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp2", "REG-2")));

        var act = async () => await svc.GetOrganisationAsync(resp.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── GetMembersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_ReturnsSeededAdminMember()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var userId = Guid.NewGuid();
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(userId, "a@a.com",
            new CreateOrganisationRequest("Corp3", "REG-3")));

        var result = await svc.GetMembersAsync(resp.Id, resp.Id);

        var members = ((IEnumerable<MemberResponse>)result).ToList();
        members.Should().ContainSingle(m => m.Role == "Admin");
    }

    [Fact]
    public async Task GetMembers_Throws_WhenCallerOrgDiffers()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp4", "REG-4")));

        var act = async () => await svc.GetMembersAsync(resp.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── InviteMemberAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task InviteMember_AddsMember_WhenCallerIsAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var adminId = Guid.NewGuid();
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(adminId, "admin@corp.com",
            new CreateOrganisationRequest("Corp5", "REG-5")));

        var result = await svc.InviteMemberAsync(resp.Id, resp.Id, "Admin",
            new InviteMemberRequest("new@corp.com", "Approver"));

        var member = (MemberResponse)result;
        member.Email.Should().Be("new@corp.com");
        member.Role.Should().Be("Approver");
        member.Status.Should().Be("Invited");
    }

    [Fact]
    public async Task InviteMember_Throws_WhenCallerIsNotAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp6", "REG-6")));

        var act = async () => await svc.InviteMemberAsync(resp.Id, resp.Id, "Viewer",
            new InviteMemberRequest("x@corp.com", "Approver"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task InviteMember_Throws_WhenMemberAlreadyExists()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp7", "REG-7")));

        await svc.InviteMemberAsync(resp.Id, resp.Id, "Admin", new InviteMemberRequest("dup@corp.com", "Approver"));

        var act = async () => await svc.InviteMemberAsync(resp.Id, resp.Id, "Admin",
            new InviteMemberRequest("dup@corp.com", "Approver"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Member already exists*");
    }

    [Fact]
    public async Task InviteMember_Throws_WhenCallerOrgDiffers()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp8", "REG-8")));

        var act = async () => await svc.InviteMemberAsync(resp.Id, Guid.NewGuid(), "Admin",
            new InviteMemberRequest("x@corp.com", "Viewer"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── AssignRoleAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRole_UpdatesMemberRole_WhenCallerIsAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp9", "REG-9")));

        var invited = (MemberResponse)(await svc.InviteMemberAsync(resp.Id, resp.Id, "Admin",
            new InviteMemberRequest("user@corp.com", "Viewer")));

        var result = await svc.AssignRoleAsync(resp.Id, resp.Id, "Admin", invited.Id,
            new AssignRoleRequest("Treasurer"));

        var updated = (MemberResponse)result!;
        updated.Role.Should().Be("Treasurer");
    }

    [Fact]
    public async Task AssignRole_ReturnsNull_WhenMemberNotFound()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp10", "REG-10")));

        var result = await svc.AssignRoleAsync(resp.Id, resp.Id, "Admin", Guid.NewGuid(),
            new AssignRoleRequest("Treasurer"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignRole_Throws_WhenCallerIsNotAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp11", "REG-11")));

        var act = async () => await svc.AssignRoleAsync(resp.Id, resp.Id, "Viewer",
            Guid.NewGuid(), new AssignRoleRequest("Treasurer"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── GetPoliciesAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPolicies_ReturnsDefaultPolicy()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp12", "REG-12")));

        var result = await svc.GetPoliciesAsync(resp.Id, resp.Id);

        var policies = ((IEnumerable<ApprovalPolicyResponse>)result).ToList();
        policies.Should().ContainSingle(p => p.RequiredApprovals == 1);
    }

    [Fact]
    public async Task GetPolicies_Throws_WhenCallerOrgDiffers()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp13", "REG-13")));

        var act = async () => await svc.GetPoliciesAsync(resp.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── CreatePolicyAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CreatePolicy_AddsPolicy_WhenCallerIsAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp14", "REG-14")));

        var result = await svc.CreatePolicyAsync(resp.Id, resp.Id, "Admin",
            new CreateApprovalPolicyRequest(2, 50_000m));

        var policy = (ApprovalPolicyResponse)result;
        policy.RequiredApprovals.Should().Be(2);
        policy.MonetaryThreshold.Should().Be(50_000m);
    }

    [Fact]
    public async Task CreatePolicy_Throws_WhenCallerIsNotAdmin()
    {
        var db = BuildDb();
        var svc = BuildService(db);
        var resp = (OrganisationResponse)(await svc.CreateOrganisationAsync(Guid.NewGuid(), "a@a.com",
            new CreateOrganisationRequest("Corp15", "REG-15")));

        var act = async () => await svc.CreatePolicyAsync(resp.Id, resp.Id, "Viewer",
            new CreateApprovalPolicyRequest(2, null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

// ── AssignRoleRequestValidator ─────────────────────────────────────────────

public class AssignRoleRequestValidatorTests
{
    private static readonly AssignRoleRequestValidator Validator = new();

    [Theory]
    [InlineData("Admin")]
    [InlineData("Treasurer")]
    [InlineData("Approver")]
    [InlineData("Viewer")]
    [InlineData("admin")]
    public void Validate_Passes_ForValidRoles(string role)
    {
        var result = Validator.Validate(new AssignRoleRequest(role));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("SuperUser")]
    [InlineData("Owner")]
    public void Validate_Fails_ForInvalidRoles(string role)
    {
        var result = Validator.Validate(new AssignRoleRequest(role));
        result.IsValid.Should().BeFalse();
    }
}
