using AccountService.Controllers;
using AccountService.Validation;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Validation;
using FluentValidation;
using TransactionService.Models.Dtos;
using TransactionService.Validation;
using UserService.Controllers;
using UserService.Validation;

namespace Tests;

public class ValidationTests
{
    // ── AccountService: CreateAccountRequestValidator ─────────────────────

    [Theory]
    [InlineData("Checking")]
    [InlineData("Savings")]
    [InlineData("Credit Card")]
    public void CreateAccountValidator_AcceptsValidAccountTypes(string accountType)
    {
        var validator = new CreateAccountRequestValidator();
        var result = validator.Validate(new CreateAccountRequest(accountType, null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mortgage")]
    [InlineData("Loan")]
    public void CreateAccountValidator_RejectsInvalidAccountTypes(string accountType)
    {
        var validator = new CreateAccountRequestValidator();
        var result = validator.Validate(new CreateAccountRequest(accountType, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AccountType");
    }

    [Theory]
    [InlineData("NZD")]
    [InlineData("NZD")]
    [InlineData("AUD")]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("")]
    [InlineData(null)]
    public void CreateAccountValidator_AcceptsValidCurrencies(string? currency)
    {
        var validator = new CreateAccountRequestValidator();
        var result = validator.Validate(new CreateAccountRequest("Checking", currency));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("JPY")]
    [InlineData("BTC")]
    public void CreateAccountValidator_RejectsInvalidCurrencies(string currency)
    {
        var validator = new CreateAccountRequestValidator();
        var result = validator.Validate(new CreateAccountRequest("Checking", currency));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency");
    }

    // ── TransactionService: CreatePaymentRequestDtoValidator ──────────────

    [Fact]
    public void CreatePaymentValidator_RejectsEmptyAccountId()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto { AccountId = Guid.Empty, Amount = 100m, Type = "credit" };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AccountId");
    }

    [Fact]
    public void CreatePaymentValidator_RejectsZeroAmount()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto { AccountId = Guid.NewGuid(), Amount = 0m, Type = "credit" };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void CreatePaymentValidator_RejectsNegativeAmount()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto { AccountId = Guid.NewGuid(), Amount = -10m, Type = "credit" };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("debit")]
    [InlineData("CREDIT")]
    [InlineData("DEBIT")]
    public void CreatePaymentValidator_AcceptsValidTypes(string type)
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto { AccountId = Guid.NewGuid(), Amount = 50m, Type = type };
        var result = validator.Validate(dto);
        result.Errors.Should().NotContain(e => e.PropertyName == "Type");
    }

    [Fact]
    public void CreatePaymentValidator_RejectsInvalidType()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto { AccountId = Guid.NewGuid(), Amount = 50m, Type = "transfer" };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Fact]
    public void CreatePaymentValidator_RejectsDescriptionOver500Chars()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 50m,
            Type = "credit",
            Description = new string('a', 501)
        };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Theory]
    [InlineData("Fun")]
    [InlineData("Fixed")]
    [InlineData("Future")]
    [InlineData("")]
    [InlineData(null)]
    public void CreatePaymentValidator_AcceptsValidSpendingTypes(string? spendingType)
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 50m,
            Type = "credit",
            SpendingType = spendingType
        };
        var result = validator.Validate(dto);
        result.Errors.Should().NotContain(e => e.PropertyName == "SpendingType");
    }

    [Fact]
    public void CreatePaymentValidator_RejectsInvalidSpendingType()
    {
        var validator = new CreatePaymentRequestDtoValidator();
        var dto = new CreatePaymentRequestDto
        {
            AccountId = Guid.NewGuid(),
            Amount = 50m,
            Type = "credit",
            SpendingType = "Luxury"
        };
        var result = validator.Validate(dto);
        result.IsValid.Should().BeFalse();
    }

    // ── UserService: RegisterRequestValidator ─────────────────────────────

    [Fact]
    public void RegisterValidator_RejectsEmptyEmail()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("", "Pass@1234", "First", "Last"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void RegisterValidator_RejectsInvalidEmailFormat()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("notanemail", "Pass@1234", "First", "Last"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void RegisterValidator_RejectsPasswordShorterThan8Chars()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "short", "First", "Last"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void RegisterValidator_RejectsEmptyFirstName()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "Pass@1234", "", "Last"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void RegisterValidator_RejectsFirstNameOver100Chars()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "Pass@1234", new string('A', 101), "Last"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void RegisterValidator_RejectsInvalidClientType()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "Pass@1234", "First", "Last", ClientType: "Unknown"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientType");
    }

    [Fact]
    public void RegisterValidator_RejectsCorporateWithoutCompanyName()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest(
            "a@b.com", "Pass@1234", "First", "Last",
            ClientType: "Corporate", CompanyName: null, RegistrationNumber: "REG-001"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CompanyName");
    }

    [Fact]
    public void RegisterValidator_RejectsCorporateWithoutRegistrationNumber()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest(
            "a@b.com", "Pass@1234", "First", "Last",
            ClientType: "Corporate", CompanyName: "Acme", RegistrationNumber: null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RegistrationNumber");
    }

    [Fact]
    public void RegisterValidator_AcceptsValidIndividualRequest()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest("a@b.com", "Pass@1234", "First", "Last"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterValidator_AcceptsValidCorporateRequest()
    {
        var validator = new RegisterRequestValidator();
        var result = validator.Validate(new RegisterRequest(
            "corp@b.com", "Pass@1234", "Alice", "Smith",
            ClientType: "Corporate", CompanyName: "Acme Ltd", RegistrationNumber: "REG-001"));
        result.IsValid.Should().BeTrue();
    }

    // ── UserService: ChangePasswordRequestValidator ───────────────────────

    [Fact]
    public void ChangePasswordValidator_RejectsEmptyCurrentPassword()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest("", "New@1234X"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CurrentPassword");
    }

    [Fact]
    public void ChangePasswordValidator_RejectsWeakNewPassword()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest("OldPass", "short"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void ChangePasswordValidator_RejectsNewPasswordWithoutUppercase()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest("OldPass", "alllower1@"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ChangePasswordValidator_AcceptsValidRequest()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest("OldPass@1", "New@1234X"));
        result.IsValid.Should().BeTrue();
    }

    // ── UserService: UpdateTimezoneRequestValidator ───────────────────────

    [Fact]
    public void UpdateTimezoneValidator_RejectsOffsetBelowMinus720()
    {
        var validator = new UpdateTimezoneRequestValidator();
        var result = validator.Validate(new UpdateTimezoneRequest("UTC", -721));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UtcOffsetMinutes");
    }

    [Fact]
    public void UpdateTimezoneValidator_RejectsOffsetAbove840()
    {
        var validator = new UpdateTimezoneRequestValidator();
        var result = validator.Validate(new UpdateTimezoneRequest("UTC", 841));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateTimezoneValidator_RejectsTimeZoneIdWithInvalidChars()
    {
        var validator = new UpdateTimezoneRequestValidator();
        var result = validator.Validate(new UpdateTimezoneRequest("UTC<script>", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TimeZoneId");
    }

    [Fact]
    public void UpdateTimezoneValidator_AcceptsValidTimezone()
    {
        var validator = new UpdateTimezoneRequestValidator();
        var result = validator.Validate(new UpdateTimezoneRequest("Pacific/Auckland", 720));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateTimezoneValidator_AcceptsNullValues()
    {
        var validator = new UpdateTimezoneRequestValidator();
        var result = validator.Validate(new UpdateTimezoneRequest(null, null));
        result.IsValid.Should().BeTrue();
    }

    // ── UserService: LoginRequestValidator ───────────────────────────────

    [Fact]
    public void LoginValidator_RejectsEmptyEmail()
    {
        var validator = new LoginRequestValidator();
        var result = validator.Validate(new LoginRequest("", "pass"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void LoginValidator_RejectsEmptyPassword()
    {
        var validator = new LoginRequestValidator();
        var result = validator.Validate(new LoginRequest("a@b.com", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    // ── CorporateBankingService: CreateOrganisationRequestValidator ────────

    [Fact]
    public void CreateOrganisationValidator_RejectsEmptyName()
    {
        var validator = new CreateOrganisationRequestValidator();
        var result = validator.Validate(new CreateOrganisationRequest("", "REG-001"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void CreateOrganisationValidator_RejectsEmptyRegistrationNumber()
    {
        var validator = new CreateOrganisationRequestValidator();
        var result = validator.Validate(new CreateOrganisationRequest("Acme", ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RegistrationNumber");
    }

    // ── CorporateBankingService: InviteMemberRequestValidator ─────────────

    [Theory]
    [InlineData("Admin")]
    [InlineData("Treasurer")]
    [InlineData("Approver")]
    [InlineData("Viewer")]
    public void InviteMemberValidator_AcceptsValidRoles(string role)
    {
        var validator = new InviteMemberRequestValidator();
        var result = validator.Validate(new InviteMemberRequest("a@b.com", role));
        result.Errors.Should().NotContain(e => e.PropertyName == "Role");
    }

    [Fact]
    public void InviteMemberValidator_RejectsInvalidRole()
    {
        var validator = new InviteMemberRequestValidator();
        var result = validator.Validate(new InviteMemberRequest("a@b.com", "SuperUser"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Fact]
    public void InviteMemberValidator_RejectsInvalidEmail()
    {
        var validator = new InviteMemberRequestValidator();
        var result = validator.Validate(new InviteMemberRequest("notanemail", "Admin"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    // ── CorporateBankingService: CreateApprovalPolicyRequestValidator ──────

    [Fact]
    public void CreateApprovalPolicyValidator_RejectsZeroRequiredApprovals()
    {
        var validator = new CreateApprovalPolicyRequestValidator();
        var result = validator.Validate(new CreateApprovalPolicyRequest(0, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RequiredApprovals");
    }

    [Fact]
    public void CreateApprovalPolicyValidator_RejectsNegativeThreshold()
    {
        var validator = new CreateApprovalPolicyRequestValidator();
        var result = validator.Validate(new CreateApprovalPolicyRequest(1, -100m));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MonetaryThreshold");
    }

    [Fact]
    public void CreateApprovalPolicyValidator_AcceptsNullThreshold()
    {
        var validator = new CreateApprovalPolicyRequestValidator();
        var result = validator.Validate(new CreateApprovalPolicyRequest(2, null));
        result.IsValid.Should().BeTrue();
    }

    // ── CorporateBankingService: CreatePaymentBatchRequestValidator ────────

    [Fact]
    public void CreatePaymentBatchValidator_RejectsEmptyCurrency()
    {
        var validator = new CreatePaymentBatchRequestValidator();
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "Alice", null, 100m, null)
        };
        var result = validator.Validate(new CreatePaymentBatchRequest("", items));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency");
    }

    [Fact]
    public void CreatePaymentBatchValidator_RejectsEmptyItemsList()
    {
        var validator = new CreatePaymentBatchRequestValidator();
        var result = validator.Validate(new CreatePaymentBatchRequest("NZD", new List<PaymentBatchItemDto>()));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void CreatePaymentBatchValidator_RejectsItemWithEmptySourceAccountId()
    {
        var validator = new CreatePaymentBatchRequestValidator();
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.Empty, "Alice", null, 100m, null)
        };
        var result = validator.Validate(new CreatePaymentBatchRequest("NZD", items));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreatePaymentBatchValidator_RejectsItemWithZeroAmount()
    {
        var validator = new CreatePaymentBatchRequestValidator();
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "Alice", null, 0m, null)
        };
        var result = validator.Validate(new CreatePaymentBatchRequest("NZD", items));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreatePaymentBatchValidator_RejectsItemWithEmptyPayeeName()
    {
        var validator = new CreatePaymentBatchRequestValidator();
        var items = new List<PaymentBatchItemDto>
        {
            new(Guid.NewGuid(), "", null, 100m, null)
        };
        var result = validator.Validate(new CreatePaymentBatchRequest("NZD", items));
        result.IsValid.Should().BeFalse();
    }

    // ── CorporateBankingService: ApprovalDecisionRequestValidator ──────────

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void ApprovalDecisionValidator_AcceptsValidDecisions(string decision)
    {
        var validator = new ApprovalDecisionRequestValidator();
        var result = validator.Validate(new ApprovalDecisionRequest(decision, null));
        result.Errors.Should().NotContain(e => e.PropertyName == "Decision");
    }

    [Fact]
    public void ApprovalDecisionValidator_RejectsInvalidDecision()
    {
        var validator = new ApprovalDecisionRequestValidator();
        var result = validator.Validate(new ApprovalDecisionRequest("Maybe", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Decision");
    }

    [Fact]
    public void ApprovalDecisionValidator_RejectsCommentsOver500Chars()
    {
        var validator = new ApprovalDecisionRequestValidator();
        var result = validator.Validate(new ApprovalDecisionRequest("Approved", new string('x', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Comments");
    }

    [Fact]
    public void ApprovalDecisionValidator_AcceptsNullComments()
    {
        var validator = new ApprovalDecisionRequestValidator();
        var result = validator.Validate(new ApprovalDecisionRequest("Approved", null));
        result.IsValid.Should().BeTrue();
    }
}
