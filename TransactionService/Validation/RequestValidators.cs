using FluentValidation;
using TransactionService.Models.Dtos;

namespace TransactionService.Validation;

public class CreatePaymentRequestDtoValidator : AbstractValidator<CreatePaymentRequestDto>
{
    private static readonly string[] AllowedTypes = ["credit", "debit"];
    private static readonly string[] AllowedSpendingTypes = ["Fun", "Fixed", "Future"];

    public CreatePaymentRequestDtoValidator()
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

        RuleFor(x => x.SpendingType)
            .Must(s => string.IsNullOrEmpty(s) || AllowedSpendingTypes.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Spending type must be one of: {string.Join(", ", AllowedSpendingTypes)}.");
    }
}
