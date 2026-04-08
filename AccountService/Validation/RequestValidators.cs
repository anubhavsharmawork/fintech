using FluentValidation;
using AccountService.Controllers;

namespace AccountService.Validation;

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    private static readonly string[] AllowedAccountTypes = ["Checking", "Savings", "Credit Card"];
    private static readonly string[] AllowedCurrencies = ["NZD", "USD", "AUD", "GBP", "EUR"];

    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.AccountType)
            .NotEmpty().WithMessage("Account type is required.")
            .Must(t => AllowedAccountTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Account type must be one of: {string.Join(", ", AllowedAccountTypes)}.");

        RuleFor(x => x.Currency)
            .Must(c => string.IsNullOrWhiteSpace(c) || AllowedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Currency must be one of: {string.Join(", ", AllowedCurrencies)}.");
    }
}
