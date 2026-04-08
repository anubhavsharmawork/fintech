using System.Security.Claims;
using ApiGateway.Models;
using ApiGateway.Validation;
using Contracts;
using CorporateBankingService.Controllers;
using CorporateBankingService.Data;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Tests;

// ── OrganisationsController ────────────────────────────────────────────────

public class OrganisationsControllerTests
{
    private static CorporateDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<CorporateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OrganisationsController BuildController(
        IOrganisationService svc, Guid? userId = null, Guid? orgId = null, string? role = null)
    {
        var ctrl = new OrganisationsController(svc);
        ctrl.ControllerContext = MakeContext(userId, orgId, role);
        return ctrl;
    }

    private static ControllerContext MakeContext(Guid? userId, Guid? orgId, string? role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue) claims.Add(new Claim("sub", userId.Value.ToString()));
        if (orgId.HasValue) claims.Add(new Claim("organisation_id", orgId.Value.ToString()));
        if (role != null) claims.Add(new Claim("organisation_role", role));
        claims.Add(new Claim("email", "test@corp.com"));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
    }

    // ── CreateOrganisation ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrganisation_ReturnsOk_WhenValid()
    {
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreateOrganisationAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CreateOrganisationRequest>()))
            .ReturnsAsync(new OrganisationResponse(Guid.NewGuid(), "Corp", "REG-1", DateTime.UtcNow));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.CreateOrganisation(new CreateOrganisationRequest("Corp", "REG-1"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateOrganisation_ReturnsBadRequest_WhenDuplicateRegistration()
    {
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreateOrganisationAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CreateOrganisationRequest>()))
            .ThrowsAsync(new InvalidOperationException("already exists"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.CreateOrganisation(new CreateOrganisationRequest("Corp", "REG-DUP"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateOrganisation_ReturnsUnauthorized_WhenMissingClaim()
    {
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreateOrganisationAsync(It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CreateOrganisationRequest>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.CreateOrganisation(new CreateOrganisationRequest("Corp", "REG-1"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetOrganisation ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrganisation_ReturnsOk_WhenFound()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetOrganisationAsync(orgId, orgId))
            .ReturnsAsync(new OrganisationResponse(orgId, "Corp", "REG", DateTime.UtcNow));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisation(orgId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrganisation_ReturnsNotFound_WhenNull()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetOrganisationAsync(orgId, orgId)).ReturnsAsync((object?)null);
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisation(orgId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetOrganisation_ReturnsForbid_WhenUnauthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetOrganisationAsync(orgId, orgId))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisation(orgId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetOrganisation_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetOrganisationAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetOrganisation(orgId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetMembers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_ReturnsOk_WhenAuthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetMembersAsync(orgId, orgId))
            .ReturnsAsync(new List<MemberResponse>());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetMembers(orgId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMembers_ReturnsForbid_WhenUnauthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetMembersAsync(orgId, orgId)).ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetMembers(orgId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetMembers_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetMembersAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetMembers(orgId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── InviteMember ───────────────────────────────────────────────────────

    [Fact]
    public async Task InviteMember_ReturnsOk_WhenValid()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.InviteMemberAsync(orgId, orgId, "Admin", It.IsAny<InviteMemberRequest>()))
            .ReturnsAsync(new MemberResponse(Guid.NewGuid(), Guid.Empty, "x@x.com", "Viewer", "Invited",
                DateTime.UtcNow, null));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.InviteMember(orgId, new InviteMemberRequest("x@x.com", "Viewer"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InviteMember_ReturnsForbid_WhenNotAdmin()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.InviteMemberAsync(orgId, orgId, "Viewer", It.IsAny<InviteMemberRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Viewer");

        var result = await ctrl.InviteMember(orgId, new InviteMemberRequest("x@x.com", "Viewer"));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task InviteMember_ReturnsBadRequest_WhenDuplicate()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.InviteMemberAsync(orgId, orgId, "Admin", It.IsAny<InviteMemberRequest>()))
            .ThrowsAsync(new InvalidOperationException("already exists"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.InviteMember(orgId, new InviteMemberRequest("dup@x.com", "Viewer"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task InviteMember_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.InviteMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<InviteMemberRequest>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.InviteMember(orgId, new InviteMemberRequest("x@x.com", "Viewer"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── AssignRole ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRole_ReturnsOk_WhenFound()
    {
        var orgId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.AssignRoleAsync(orgId, orgId, "Admin", memberId, It.IsAny<AssignRoleRequest>()))
            .ReturnsAsync(new MemberResponse(memberId, Guid.NewGuid(), "x@x.com", "Treasurer", "Active",
                DateTime.UtcNow, DateTime.UtcNow));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.AssignRole(orgId, memberId, new AssignRoleRequest("Treasurer"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignRole_ReturnsNotFound_WhenNull()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.AssignRoleAsync(orgId, orgId, "Admin", It.IsAny<Guid>(),
                It.IsAny<AssignRoleRequest>()))
            .ReturnsAsync((object?)null);
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.AssignRole(orgId, Guid.NewGuid(), new AssignRoleRequest("Treasurer"));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AssignRole_ReturnsForbid_WhenUnauthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.AssignRoleAsync(orgId, orgId, "Viewer", It.IsAny<Guid>(),
                It.IsAny<AssignRoleRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Viewer");

        var result = await ctrl.AssignRole(orgId, Guid.NewGuid(), new AssignRoleRequest("Treasurer"));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AssignRole_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.AssignRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<AssignRoleRequest>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.AssignRole(orgId, Guid.NewGuid(), new AssignRoleRequest("Treasurer"));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetPolicies ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPolicies_ReturnsOk_WhenAuthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetPoliciesAsync(orgId, orgId))
            .ReturnsAsync(new List<ApprovalPolicyResponse>());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetPolicies(orgId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPolicies_ReturnsForbid_WhenUnauthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetPoliciesAsync(orgId, orgId)).ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetPolicies(orgId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPolicies_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.GetPoliciesAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetPolicies(orgId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── CreatePolicy ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePolicy_ReturnsOk_WhenAdmin()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreatePolicyAsync(orgId, orgId, "Admin", It.IsAny<CreateApprovalPolicyRequest>()))
            .ReturnsAsync(new ApprovalPolicyResponse(Guid.NewGuid(), 2, 50000m));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.CreatePolicy(orgId, new CreateApprovalPolicyRequest(2, 50000m));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreatePolicy_ReturnsForbid_WhenNotAdmin()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreatePolicyAsync(orgId, orgId, "Viewer", It.IsAny<CreateApprovalPolicyRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Viewer");

        var result = await ctrl.CreatePolicy(orgId, new CreateApprovalPolicyRequest(2, null));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreatePolicy_ReturnsUnauthorized_WhenMissingClaim()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IOrganisationService>();
        svc.Setup(s => s.CreatePolicyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CreateApprovalPolicyRequest>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Admin");

        var result = await ctrl.CreatePolicy(orgId, new CreateApprovalPolicyRequest(2, null));

        result.Should().BeOfType<UnauthorizedResult>();
    }
}

// ── ApprovalsController ────────────────────────────────────────────────────

public class ApprovalsControllerTests
{
    private static ApprovalsController BuildController(
        IApprovalService svc, Guid? userId = null, Guid? orgId = null, string? role = null)
    {
        var ctrl = new ApprovalsController(svc);
        var claims = new List<Claim>();
        if (userId.HasValue) claims.Add(new Claim("sub", userId.Value.ToString()));
        if (orgId.HasValue) claims.Add(new Claim("organisation_id", orgId.Value.ToString()));
        if (role != null) claims.Add(new Claim("organisation_role", role));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task GetPendingBatches_ReturnsOk_WhenAuthorized()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetPendingBatchesAsync(orgId, "Approver"))
            .ReturnsAsync(new List<object>());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId, role: "Approver");

        var result = await ctrl.GetPendingBatches();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPendingBatches_ReturnsUnauthorized_WhenMissingClaim()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetPendingBatchesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.GetPendingBatches();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPendingBatches_ReturnsForbid_WhenUnauthorized()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetPendingBatchesAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.GetPendingBatches();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBatchDetail_ReturnsOk_WhenFound()
    {
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetBatchDetailAsync(batchId, orgId)).ReturnsAsync(new object());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetBatchDetail(batchId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBatchDetail_ReturnsNotFound_WhenNull()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetBatchDetailAsync(It.IsAny<Guid>(), orgId)).ReturnsAsync((object?)null);
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.GetBatchDetail(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBatchDetail_ReturnsUnauthorized_WhenMissingClaim()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.GetBatchDetailAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.GetBatchDetail(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Decide_ReturnsOk_WhenSuccessful()
    {
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.DecideAsync(batchId, orgId, userId, "Approver", It.IsAny<ApprovalDecisionRequest>()))
            .ReturnsAsync(new object());
        var ctrl = BuildController(svc.Object, userId: userId, orgId: orgId, role: "Approver");

        var result = await ctrl.Decide(batchId, new ApprovalDecisionRequest("Approve", null));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Decide_ReturnsNotFound_WhenNull()
    {
        var orgId = Guid.NewGuid();
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.DecideAsync(It.IsAny<Guid>(), orgId, It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<ApprovalDecisionRequest>()))
            .ReturnsAsync((object?)null);
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: orgId);

        var result = await ctrl.Decide(Guid.NewGuid(), new ApprovalDecisionRequest("Approve", null));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Decide_ReturnsBadRequest_WhenInvalidOperation()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.DecideAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<ApprovalDecisionRequest>()))
            .ThrowsAsync(new InvalidOperationException("already decided"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.Decide(Guid.NewGuid(), new ApprovalDecisionRequest("Approve", null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Decide_ReturnsUnauthorized_WhenMissingClaim()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.DecideAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<ApprovalDecisionRequest>()))
            .ThrowsAsync(new MissingClaimUnauthorizedException("missing"));
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.Decide(Guid.NewGuid(), new ApprovalDecisionRequest("Approve", null));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Decide_ReturnsForbid_WhenUnauthorized()
    {
        var svc = new Mock<IApprovalService>();
        svc.Setup(s => s.DecideAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<ApprovalDecisionRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var ctrl = BuildController(svc.Object, userId: Guid.NewGuid(), orgId: Guid.NewGuid());

        var result = await ctrl.Decide(Guid.NewGuid(), new ApprovalDecisionRequest("Approve", null));

        result.Should().BeOfType<ForbidResult>();
    }
}

// ── PaymentBatchesController ───────────────────────────────────────────────

public class PaymentBatchesControllerTests
{
    private static CorporateDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<CorporateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PaymentBatchesController BuildController(
        CorporateDbContext db, Guid? orgId = null, Guid? userId = null, string? role = null)
    {
        var ctrl = new PaymentBatchesController(
            db,
            new Mock<IApprovalWorkflowService>().Object,
            new Mock<IPublishEndpoint>().Object,
            new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<PaymentBatchesController>>().Object);

        var claims = new List<Claim>();
        if (orgId.HasValue) claims.Add(new Claim("organisation_id", orgId.Value.ToString()));
        if (userId.HasValue)
        {
            claims.Add(new Claim("sub", userId.Value.ToString()));
            claims.Add(new Claim("id", userId.Value.ToString()));
        }
        if (role != null) claims.Add(new Claim("organisation_role", role));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task GetBatches_ReturnsForbid_WhenNoOrgClaim()
    {
        var ctrl = BuildController(BuildDb(), orgId: null);

        var result = await ctrl.GetBatches();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBatches_ReturnsOkEmptyList_WhenNoData()
    {
        var orgId = Guid.NewGuid();
        var ctrl = BuildController(BuildDb(), orgId: orgId, userId: Guid.NewGuid());

        var result = await ctrl.GetBatches();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBatch_ReturnsForbid_WhenNoOrgClaim()
    {
        var ctrl = BuildController(BuildDb(), orgId: null);

        var result = await ctrl.GetBatch(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBatch_ReturnsNotFound_WhenBatchMissing()
    {
        var orgId = Guid.NewGuid();
        var ctrl = BuildController(BuildDb(), orgId: orgId, userId: Guid.NewGuid());

        var result = await ctrl.GetBatch(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateBatch_ReturnsForbid_WhenNoOrgClaim()
    {
        var ctrl = BuildController(BuildDb(), orgId: null);
        var req = new CreatePaymentBatchRequest("NZD",
            new List<PaymentBatchItemDto> { new(Guid.NewGuid(), "Payee", "ACC-1", 100m, null) });

        var result = await ctrl.CreateBatch(req);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateBatch_ReturnsForbid_WhenCallerNotAdminOrTreasurer()
    {
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: Guid.NewGuid(), role: "Viewer");
        var req = new CreatePaymentBatchRequest("NZD",
            new List<PaymentBatchItemDto> { new(Guid.NewGuid(), "Payee", "ACC-1", 100m, null) });

        var result = await ctrl.CreateBatch(req);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateBatch_ReturnsOk_WhenValid()
    {
        var userId = Guid.NewGuid();
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: userId, role: "Admin");
        var req = new CreatePaymentBatchRequest("NZD",
            new List<PaymentBatchItemDto> { new(Guid.NewGuid(), "Payee", "ACC-1", 500m, "desc") });

        var result = await ctrl.CreateBatch(req);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsForbid_WhenNoOrgClaim()
    {
        var ctrl = BuildController(BuildDb(), orgId: null);

        var result = await ctrl.SubmitForApproval(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsForbid_WhenNotAdminOrTreasurer()
    {
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: Guid.NewGuid(), role: "Viewer");

        var result = await ctrl.SubmitForApproval(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsNotFound_WhenBatchMissing()
    {
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: Guid.NewGuid(), role: "Admin");

        var result = await ctrl.SubmitForApproval(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsBadRequest_WhenNotDraft()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var batch = new CorporateBankingService.Models.PaymentBatch
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, SubmittedByUserId = userId,
            Status = "PendingApproval", Currency = "NZD", TotalAmount = 100m,
            ItemCount = 1, CreatedAt = DateTime.UtcNow
        };
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();

        var ctrl = BuildController(db, orgId: orgId, userId: userId, role: "Admin");

        var result = await ctrl.SubmitForApproval(batch.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitForApproval_ReturnsOk_WhenDraft()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pub = new Mock<IPublishEndpoint>();
        var batch = new CorporateBankingService.Models.PaymentBatch
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, SubmittedByUserId = userId,
            Status = "Draft", Currency = "NZD", TotalAmount = 100m,
            ItemCount = 1, CreatedAt = DateTime.UtcNow
        };
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();

        var ctrl = new PaymentBatchesController(db, new Mock<IApprovalWorkflowService>().Object,
            pub.Object, new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<PaymentBatchesController>>().Object);
        var claims = new List<Claim>
        {
            new("organisation_id", orgId.ToString()),
            new("sub", userId.ToString()),
            new("organisation_role", "Admin")
        };
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };

        var result = await ctrl.SubmitForApproval(batch.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsForbid_WhenNoOrgClaim()
    {
        var ctrl = BuildController(BuildDb(), orgId: null);

        var result = await ctrl.ExecuteBatch(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsForbid_WhenNotAdminOrTreasurer()
    {
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: Guid.NewGuid(), role: "Viewer");

        var result = await ctrl.ExecuteBatch(Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsNotFound_WhenBatchMissing()
    {
        var ctrl = BuildController(BuildDb(), orgId: Guid.NewGuid(), userId: Guid.NewGuid(), role: "Admin");

        var result = await ctrl.ExecuteBatch(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsBadRequest_WhenNotApproved()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var batch = new CorporateBankingService.Models.PaymentBatch
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, SubmittedByUserId = userId,
            Status = "Draft", Currency = "NZD", TotalAmount = 100m,
            ItemCount = 0, CreatedAt = DateTime.UtcNow
        };
        db.PaymentBatches.Add(batch);
        await db.SaveChangesAsync();

        var ctrl = BuildController(db, orgId: orgId, userId: userId, role: "Admin");

        var result = await ctrl.ExecuteBatch(batch.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExecuteBatch_ReturnsOk_WhenApprovedBatchExecutes()
    {
        var db = BuildDb();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var batch = new CorporateBankingService.Models.PaymentBatch
        {
            Id = Guid.NewGuid(), OrganisationId = orgId, SubmittedByUserId = userId,
            Status = "Approved", Currency = "NZD", TotalAmount = 200m,
            ItemCount = 1, CreatedAt = DateTime.UtcNow, SubmittedAt = DateTime.UtcNow
        };
        db.PaymentBatches.Add(batch);
        db.PaymentBatchItems.Add(new CorporateBankingService.Models.PaymentBatchItem
        {
            Id = Guid.NewGuid(), PaymentBatchId = batch.Id, SourceAccountId = Guid.NewGuid(),
            PayeeName = "Test Payee", PayeeAccountNumber = "12345", Amount = 200m,
            Description = "Test payment"
        });
        await db.SaveChangesAsync();

        var pub = new Mock<IPublishEndpoint>();
        var httpFactory = new Mock<IHttpClientFactory>();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.Created));
        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("http://localhost") };
        httpFactory.Setup(f => f.CreateClient("transactions")).Returns(httpClient);

        var ctrl = new PaymentBatchesController(db, new Mock<IApprovalWorkflowService>().Object,
            pub.Object, httpFactory.Object, new Mock<ILogger<PaymentBatchesController>>().Object);
        var claims = new List<Claim>
        {
            new("organisation_id", orgId.ToString()),
            new("sub", userId.ToString()),
            new("organisation_role", "Treasurer")
        };
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Principal.GenericPrincipal(
                    new ClaimsIdentity(claims, "test"), null)
            }
        };
        ctrl.HttpContext.Request.Headers["Authorization"] = "Bearer test-token";

        var result = await ctrl.ExecuteBatch(batch.Id);

        result.Should().BeOfType<OkObjectResult>();
        var updatedBatch = await db.PaymentBatches.FindAsync(batch.Id);
        updatedBatch!.Status.Should().Be("Executed");
        updatedBatch.ExecutedAt.Should().NotBeNull();
    }
}

// ── ApiGateway Validators ──────────────────────────────────────────────────

public class ApiGatewayValidatorTests
{
    [Theory]
    [InlineData("user@test.com", "password123", true)]
    [InlineData("", "password123", false)]
    [InlineData("user@test.com", "", false)]
    public void LoginRequestValidator_ValidatesCorrectly(string email, string password, bool valid)
    {
        var v = new LoginRequestValidator();
        v.Validate(new LoginRequest(email, password)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("user@test.com", "password123", "John", "Doe", true)]
    [InlineData("bad-email", "password123", "John", "Doe", false)]
    [InlineData("user@test.com", "short", "John", "Doe", false)]
    [InlineData("user@test.com", "password123", "", "Doe", false)]
    [InlineData("user@test.com", "password123", "John", "", false)]
    public void RegisterRequestValidator_ValidatesCorrectly(
        string email, string pass, string first, string last, bool valid)
    {
        var v = new RegisterRequestValidator();
        v.Validate(new RegisterRequest(email, pass, first, last)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("Checking", "NZD", true)]
    [InlineData("InvalidType", "NZD", false)]
    [InlineData("Savings", "INVALID", false)]
    [InlineData(null, null, true)]
    public void CreateAccountRequestValidator_ValidatesCorrectly(string? type, string? currency, bool valid)
    {
        var v = new CreateAccountRequestValidator();
        v.Validate(new CreateAccountRequest(type, currency)).IsValid.Should().Be(valid);
    }

    [Fact]
    public void CreateTransactionRequestValidator_Passes_WhenValid()
    {
        var v = new CreateTransactionRequestValidator();
        var result = v.Validate(new CreateTransactionRequest(
            Guid.NewGuid(), 100m, "NZD", "credit", "desc"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 100, "credit", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 0, "credit", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 100, "invalid", false)]
    public void CreateTransactionRequestValidator_Fails_WhenInvalid(
        string acctId, decimal amount, string type, bool valid)
    {
        var v = new CreateTransactionRequestValidator();
        var result = v.Validate(new CreateTransactionRequest(Guid.Parse(acctId), amount, "NZD", type, null));
        result.IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("bank-1", true)]
    [InlineData("", false)]
    public void ConnectBankRequestValidator_ValidatesCorrectly(string bankId, bool valid)
    {
        var v = new ConnectBankRequestValidator();
        v.Validate(new ConnectBankRequest(bankId)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 100, false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 0, false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 50, true)]
    public void DepositFromExternalRequestValidator_ValidatesCorrectly(
        string extAcctId, decimal amount, bool valid)
    {
        var v = new DepositFromExternalRequestValidator();
        v.Validate(new DepositFromExternalRequest(Guid.Parse(extAcctId), amount)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("Payee Name", "12345678", true)]
    [InlineData("", "12345678", false)]
    [InlineData("Payee", "", false)]
    public void CreatePayeeRequestValidator_ValidatesCorrectly(string name, string acct, bool valid)
    {
        var v = new CreatePayeeRequestValidator();
        v.Validate(new CreatePayeeRequest(name, acct)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", 100, false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 0, false)]
    [InlineData("11111111-1111-1111-1111-111111111111", 100, true)]
    public void CreatePaymentRequestValidator_ValidatesCorrectly(
        string acctId, decimal amount, bool valid)
    {
        var v = new CreatePaymentRequestValidator();
        v.Validate(new CreatePaymentRequest(Guid.Parse(acctId), amount, "Payee", "ACC-1", "desc")).IsValid.Should().Be(valid);
    }
}
