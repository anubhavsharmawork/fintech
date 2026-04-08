using FluentValidation;
using CorporateBankingService.Models.Dtos;

namespace CorporateBankingService.Validation;

public class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    public CreateOrganisationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organisation name is required.")
            .MaximumLength(200).WithMessage("Organisation name must not exceed 200 characters.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Registration number is required.")
            .MaximumLength(50).WithMessage("Registration number must not exceed 50 characters.");
    }
}

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    private static readonly string[] ValidRoles = ["Admin", "Treasurer", "Approver", "Viewer"];

    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => ValidRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Admin, Treasurer, Approver, or Viewer.");
    }
}

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    private static readonly string[] ValidRoles = ["Admin", "Treasurer", "Approver", "Viewer"];

    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => ValidRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Admin, Treasurer, Approver, or Viewer.");
    }
}

public class CreateApprovalPolicyRequestValidator : AbstractValidator<CreateApprovalPolicyRequest>
{
    public CreateApprovalPolicyRequestValidator()
    {
        RuleFor(x => x.RequiredApprovals)
            .GreaterThan(0).WithMessage("At least one approval is required.");

        RuleFor(x => x.MonetaryThreshold)
            .GreaterThan(0).When(x => x.MonetaryThreshold.HasValue)
            .WithMessage("Monetary threshold must be greater than zero.");
    }
}

public class CreatePaymentBatchRequestValidator : AbstractValidator<CreatePaymentBatchRequest>
{
    public CreatePaymentBatchRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(3).WithMessage("Currency must not exceed 3 characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one payment item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SourceAccountId)
                .NotEqual(Guid.Empty).WithMessage("Source account ID is required.");

            item.RuleFor(i => i.PayeeName)
                .NotEmpty().WithMessage("Payee name is required.")
                .MaximumLength(200).WithMessage("Payee name must not exceed 200 characters.");

            item.RuleFor(i => i.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than zero.");
        });
    }
}

public class ApprovalDecisionRequestValidator : AbstractValidator<ApprovalDecisionRequest>
{
    private static readonly string[] ValidDecisions = ["Approved", "Rejected"];

    public ApprovalDecisionRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Decision is required.")
            .Must(d => ValidDecisions.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Decision must be 'Approved' or 'Rejected'.");

        RuleFor(x => x.Comments)
            .MaximumLength(500).When(x => x.Comments is not null)
            .WithMessage("Comments must not exceed 500 characters.");
    }
}
