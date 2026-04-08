using FluentValidation;
using UserService.Controllers;

namespace UserService.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] ValidClientTypes = ["Individual", "Corporate"];

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.ClientType)
            .Must(ct => ct is null || ValidClientTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Client type must be 'Individual' or 'Corporate'.");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required for corporate accounts.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.")
            .When(x => string.Equals(x.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Registration number is required for corporate accounts.")
            .MaximumLength(50).WithMessage("Registration number must not exceed 50 characters.")
            .When(x => string.Equals(x.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase));
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("A valid user ID is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required.");
    }
}

public class UpdateTimezoneRequestValidator : AbstractValidator<UpdateTimezoneRequest>
{
    public UpdateTimezoneRequestValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .MaximumLength(100).WithMessage("Timezone identifier must not exceed 100 characters.")
            .Matches(@"^[A-Za-z0-9_+\-/]+$").WithMessage("Timezone identifier contains invalid characters.")
            .When(x => x.TimeZoneId is not null);

        RuleFor(x => x.UtcOffsetMinutes)
            .InclusiveBetween(-720, 840).WithMessage("UTC offset must be between -720 and +840 minutes.")
            .When(x => x.UtcOffsetMinutes.HasValue);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters long.")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$")
            .WithMessage("New password must contain uppercase, lowercase, number, and special character.");
    }
}
