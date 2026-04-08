using ApiGateway.Models;

namespace Tests;

public class RequestModelsTests
{
    [Fact]
    public void LoginRequest_WithCredentials_CreatesValidRecord()
    {
        var request = new LoginRequest("user@example.com", "SecureP@ss123");

        request.Email.Should().Be("user@example.com");
        request.Password.Should().Be("SecureP@ss123");
    }

    [Fact]
    public void RegisterRequest_WithRequiredFields_CreatesValidRecord()
    {
        var request = new RegisterRequest(
            "newuser@example.com",
            "SecurePassword123!",
            "John",
            "Doe"
        );

        request.Email.Should().Be("newuser@example.com");
        request.Password.Should().Be("SecurePassword123!");
        request.FirstName.Should().Be("John");
        request.LastName.Should().Be("Doe");
        request.ClientType.Should().Be("Individual");
        request.CompanyName.Should().BeNull();
        request.RegistrationNumber.Should().BeNull();
    }

    [Fact]
    public void RegisterRequest_WithCorporateDetails_CreatesValidRecord()
    {
        var request = new RegisterRequest(
            "corp@company.com",
            "CorpPassword!",
            "Jane",
            "Smith",
            "Corporate",
            "Acme Corp",
            "NZ123456"
        );

        request.ClientType.Should().Be("Corporate");
        request.CompanyName.Should().Be("Acme Corp");
        request.RegistrationNumber.Should().Be("NZ123456");
    }

    [Fact]
    public void CreateTransactionRequest_WithAllFields_CreatesValidRecord()
    {
        var accountId = Guid.NewGuid();
        var request = new CreateTransactionRequest(
            accountId,
            150.50m,
            "NZD",
            "debit",
            "Coffee shop purchase",
            "idem-key-123"
        );

        request.AccountId.Should().Be(accountId);
        request.Amount.Should().Be(150.50m);
        request.Currency.Should().Be("NZD");
        request.Type.Should().Be("debit");
        request.Description.Should().Be("Coffee shop purchase");
        request.IdempotencyKey.Should().Be("idem-key-123");
    }

    [Fact]
    public void CreateTransactionRequest_WithoutOptionalFields_UsesDefaults()
    {
        var accountId = Guid.NewGuid();
        var request = new CreateTransactionRequest(
            accountId,
            100m,
            null,
            "credit",
            null
        );

        request.Currency.Should().BeNull();
        request.Description.Should().BeNull();
        request.IdempotencyKey.Should().BeNull();
    }

    [Fact]
    public void CreateAccountRequest_WithValues_CreatesValidRecord()
    {
        var request = new CreateAccountRequest("Savings", "NZD");

        request.AccountType.Should().Be("Savings");
        request.Currency.Should().Be("NZD");
    }

    [Fact]
    public void CreateAccountRequest_WithNullValues_AcceptsNull()
    {
        var request = new CreateAccountRequest(null, null);

        request.AccountType.Should().BeNull();
        request.Currency.Should().BeNull();
    }

    [Fact]
    public void DepositFromExternalRequest_CreatesValidRecord()
    {
        var externalId = Guid.NewGuid();
        var request = new DepositFromExternalRequest(externalId, 5000m);

        request.ExternalBankAccountId.Should().Be(externalId);
        request.Amount.Should().Be(5000m);
    }

    [Fact]
    public void ConnectBankRequest_CreatesValidRecord()
    {
        var request = new ConnectBankRequest("bank-id-123");

        request.BankId.Should().Be("bank-id-123");
    }

    [Fact]
    public void CreatePayeeRequest_CreatesValidRecord()
    {
        var request = new CreatePayeeRequest("John Doe", "1234567890");

        request.Name.Should().Be("John Doe");
        request.AccountNumber.Should().Be("1234567890");
    }

    [Fact]
    public void CreatePaymentRequest_CreatesValidRecord()
    {
        var accountId = Guid.NewGuid();
        var request = new CreatePaymentRequest(
            accountId,
            250.00m,
            "Jane Smith",
            "0987654321",
            "Invoice payment"
        );

        request.AccountId.Should().Be(accountId);
        request.Amount.Should().Be(250.00m);
        request.PayeeName.Should().Be("Jane Smith");
        request.PayeeAccountNumber.Should().Be("0987654321");
        request.Description.Should().Be("Invoice payment");
    }

    [Fact]
    public void TransactionResponse_CreatesValidRecord()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var response = new TransactionResponse(
            id,
            accountId,
            500m,
            "NZD",
            "credit",
            "Salary deposit",
            createdAt
        );

        response.Id.Should().Be(id);
        response.AccountId.Should().Be(accountId);
        response.Amount.Should().Be(500m);
        response.Currency.Should().Be("NZD");
        response.Type.Should().Be("credit");
        response.Description.Should().Be("Salary deposit");
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void SeedStatus_CreatesValidRecord()
    {
        var migrations = new List<string> { "Migration1", "Migration2" };
        var status = new SeedStatus(10, 50, true, migrations);

        status.AccountsCount.Should().Be(10);
        status.TransactionsCount.Should().Be(50);
        status.DemoTransactionsPresent.Should().BeTrue();
        status.Migrations.Should().BeEquivalentTo(migrations);
    }

    [Fact]
    public void InviteMemberInlineRequest_CreatesValidRecord()
    {
        var request = new InviteMemberInlineRequest("member@company.com", "Admin");

        request.Email.Should().Be("member@company.com");
        request.Role.Should().Be("Admin");
    }

    [Fact]
    public void CreateBatchInlineRequest_CreatesValidRecord()
    {
        var items = new List<CreateBatchItemInline>
        {
            new(Guid.NewGuid(), "Vendor A", "1111", 100m, "Payment 1"),
            new(Guid.NewGuid(), "Vendor B", "2222", 200m, "Payment 2")
        };

        var request = new CreateBatchInlineRequest("NZD", items);

        request.Currency.Should().Be("NZD");
        request.Items.Should().HaveCount(2);
    }

    [Fact]
    public void CreateBatchItemInline_CreatesValidRecord()
    {
        var sourceAccountId = Guid.NewGuid();
        var item = new CreateBatchItemInline(
            sourceAccountId,
            "Supplier Inc",
            "9876543210",
            1500m,
            "Monthly invoice"
        );

        item.SourceAccountId.Should().Be(sourceAccountId);
        item.PayeeName.Should().Be("Supplier Inc");
        item.PayeeAccountNumber.Should().Be("9876543210");
        item.Amount.Should().Be(1500m);
        item.Description.Should().Be("Monthly invoice");
    }

    [Fact]
    public void ApprovalDecisionInlineRequest_CreatesValidRecord()
    {
        var request = new ApprovalDecisionInlineRequest("Approved", "Looks good");

        request.Decision.Should().Be("Approved");
        request.Comments.Should().Be("Looks good");
    }

    [Fact]
    public void ApprovalDecisionInlineRequest_WithNullComments_AcceptsNull()
    {
        var request = new ApprovalDecisionInlineRequest("Rejected", null);

        request.Decision.Should().Be("Rejected");
        request.Comments.Should().BeNull();
    }

    [Fact]
    public void WeatherForecast_CalculatesTemperatureF()
    {
        var forecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Today), 20, "Warm");

        forecast.TemperatureC.Should().Be(20);
        forecast.TemperatureF.Should().Be(68);
        forecast.Summary.Should().Be("Warm");
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(-40, -40)]
    [InlineData(37, 99)]
    public void WeatherForecast_TemperatureConversion_IsCorrect(int celsius, int expectedFahrenheit)
    {
        var forecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Today), celsius, null);

        forecast.TemperatureF.Should().Be(expectedFahrenheit);
    }
}
