using FluentValidation;
using ApiGateway.Models;

namespace ApiGateway.Validation;

internal class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

internal class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
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
    }
}

internal class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    private static readonly string[] AllowedAccountTypes = ["Checking", "Savings", "Credit Card"];
    private static readonly string[] AllowedCurrencies = ["NZD", "USD", "AUD", "GBP", "EUR"];

    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.AccountType)
            .Must(t => string.IsNullOrWhiteSpace(t) || AllowedAccountTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Account type must be one of: {string.Join(", ", AllowedAccountTypes)}.");

        RuleFor(x => x.Currency)
            .Must(c => string.IsNullOrWhiteSpace(c) || AllowedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Currency must be one of: {string.Join(", ", AllowedCurrencies)}.");
    }
}

internal class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    private static readonly string[] AllowedTypes = ["credit", "debit"];

    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEqual(Guid.Empty).WithMessage("A valid account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Transaction type is required.")
            .Must(t => AllowedTypes.Contains(t?.Trim().ToLowerInvariant()))
            .WithMessage($"Transaction type must be one of: {string.Join(", ", AllowedTypes)}.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

internal class ConnectBankRequestValidator : AbstractValidator<ConnectBankRequest>
{
    public ConnectBankRequestValidator()
    {
        RuleFor(x => x.BankId)
            .NotEmpty().WithMessage("Bank ID is required.");
    }
}

internal class DepositFromExternalRequestValidator : AbstractValidator<DepositFromExternalRequest>
{
    public DepositFromExternalRequestValidator()
    {
        RuleFor(x => x.ExternalBankAccountId)
            .NotEqual(Guid.Empty).WithMessage("A valid external bank account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}

internal class CreatePayeeRequestValidator : AbstractValidator<CreatePayeeRequest>
{
    public CreatePayeeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Payee name is required.")
            .MaximumLength(200).WithMessage("Payee name must not exceed 200 characters.");

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Account number is required.")
            .MaximumLength(50).WithMessage("Account number must not exceed 50 characters.");
    }
}

internal class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEqual(Guid.Empty).WithMessage("A valid account ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");
    }
}
