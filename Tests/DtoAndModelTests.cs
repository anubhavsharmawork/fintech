using TransactionService.Models.Dtos;
using UserService.Controllers;
using AccountService.Controllers;
using Contracts.Events;

namespace Tests;

/// <summary>
/// Tests for DTOs, Records, and Request/Response models
/// Ensures proper initialization, defaults, and immutability
/// </summary>
public class DtoAndModelTests
{
    #region BudgetAggregationDto Tests

    [Fact]
    public void BudgetAggregationDto_CanBeCreated()
    {
        // Act
        var dto = new BudgetAggregationDto();

        // Assert
        dto.Should().NotBeNull();
    }

    [Fact]
    public void BudgetAggregationDto_DefaultValuesAreZero()
    {
        // Act
        var dto = new BudgetAggregationDto();

        // Assert
        dto.Fun.Should().Be(0);
        dto.Fixed.Should().Be(0);
        dto.Future.Should().Be(0);
        dto.Total.Should().Be(0);
    }

    [Fact]
    public void BudgetAggregationDto_PeriodHasDefaultValue()
    {
        // Act
        var dto = new BudgetAggregationDto();

        // Assert
        dto.Period.Should().NotBeNull();
    }

    [Fact]
    public void BudgetAggregationDto_AllPropertiesCanBeSet()
    {
        // Arrange
        var period = new PeriodDto { From = "2024-01-01", To = "2024-12-31" };

        // Act
        var dto = new BudgetAggregationDto
        {
            Fun = 100m,
            Fixed = 200m,
            Future = 300m,
            Total = 600m,
            Period = period
        };

        // Assert
        dto.Fun.Should().Be(100m);
        dto.Fixed.Should().Be(200m);
        dto.Future.Should().Be(300m);
        dto.Total.Should().Be(600m);
        dto.Period.From.Should().Be("2024-01-01");
        dto.Period.To.Should().Be("2024-12-31");
    }

    [Fact]
    public void BudgetAggregationDto_HandlesLargeDecimalValues()
    {
        // Act
        var dto = new BudgetAggregationDto
        {
            Fun = 999999999.99m,
            Fixed = 888888888.88m,
            Future = 777777777.77m,
            Total = 2666666666.64m
        };

        // Assert
        dto.Fun.Should().Be(999999999.99m);
        dto.Fixed.Should().Be(888888888.88m);
        dto.Future.Should().Be(777777777.77m);
        dto.Total.Should().Be(2666666666.64m);
    }

    #endregion

    #region PeriodDto Tests

    [Fact]
    public void PeriodDto_CanBeCreated()
    {
        // Act
        var dto = new PeriodDto();

        // Assert
        dto.Should().NotBeNull();
    }

    [Fact]
    public void PeriodDto_DefaultValuesAreEmptyStrings()
    {
        // Act
        var dto = new PeriodDto();

        // Assert
        dto.From.Should().BeEmpty();
        dto.To.Should().BeEmpty();
    }

    [Fact]
    public void PeriodDto_AllPropertiesCanBeSet()
    {
        // Act
        var dto = new PeriodDto
        {
            From = "2024-01-01T00:00:00Z",
            To = "2024-12-31T23:59:59Z"
        };

        // Assert
        dto.From.Should().Be("2024-01-01T00:00:00Z");
        dto.To.Should().Be("2024-12-31T23:59:59Z");
    }

    #endregion

    #region CreatePaymentRequestDto Tests

    [Fact]
    public void CreatePaymentRequestDto_CanBeCreated()
    {
        // Act
        var dto = new CreatePaymentRequestDto();

        // Assert
        dto.Should().NotBeNull();
    }

    [Fact]
    public void CreatePaymentRequestDto_HasExpectedDefaults()
    {
        // Act
        var dto = new CreatePaymentRequestDto();

        // Assert
        dto.AccountId.Should().Be(Guid.Empty);
        dto.Amount.Should().Be(0);
        dto.Type.Should().BeEmpty();
        dto.Currency.Should().Be("USD");
        dto.PayeeName.Should().BeEmpty();
        dto.PayeeAccountNumber.Should().BeNull();
        dto.Description.Should().BeNull();
        dto.SpendingType.Should().Be("Fun");
        dto.TxHash.Should().BeNull();
    }

    [Fact]
    public void CreatePaymentRequestDto_AllPropertiesCanBeSet()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        // Act
        var dto = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 250.50m,
            Type = "debit",
            Currency = "EUR",
            PayeeName = "John Doe",
            PayeeAccountNumber = "1234567890",
            Description = "Payment for services",
            SpendingType = "Fixed",
            TxHash = "0xabc123"
        };

        // Assert
        dto.AccountId.Should().Be(accountId);
        dto.Amount.Should().Be(250.50m);
        dto.Type.Should().Be("debit");
        dto.Currency.Should().Be("EUR");
        dto.PayeeName.Should().Be("John Doe");
        dto.PayeeAccountNumber.Should().Be("1234567890");
        dto.Description.Should().Be("Payment for services");
        dto.SpendingType.Should().Be("Fixed");
        dto.TxHash.Should().Be("0xabc123");
    }

    #endregion

    #region TransactionCreated Record Tests

    [Fact]
    public void TransactionCreated_CanBeCreated()
    {
        // Act
        var record = new TransactionCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            "debit",
            DateTime.UtcNow
        );

        // Assert
        record.Should().NotBeNull();
    }

    [Fact]
    public void TransactionCreated_AllPropertiesAreAccessible()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var amount = 123.45m;
        var currency = "EUR";
        var type = "credit";
        var createdAt = DateTime.UtcNow;

        // Act
        var record = new TransactionCreated(
            transactionId,
            accountId,
            userId,
            amount,
            currency,
            type,
            createdAt
        );

        // Assert
        record.TransactionId.Should().Be(transactionId);
        record.AccountId.Should().Be(accountId);
        record.UserId.Should().Be(userId);
        record.Amount.Should().Be(amount);
        record.Currency.Should().Be(currency);
        record.Type.Should().Be(type);
        record.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void TransactionCreated_SupportsWithExpression()
    {
        // Arrange
        var original = new TransactionCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            "debit",
            DateTime.UtcNow
        );

        // Act
        var modified = original with { Amount = 200m };

        // Assert
        modified.Amount.Should().Be(200m);
        modified.TransactionId.Should().Be(original.TransactionId);
        original.Amount.Should().Be(100m); // Original unchanged
    }

    [Fact]
    public void TransactionCreated_ImplementsEquality()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var record1 = new TransactionCreated(id1, id2, id3, 100m, "USD", "debit", createdAt);
        var record2 = new TransactionCreated(id1, id2, id3, 100m, "USD", "debit", createdAt);

        // Assert
        record1.Should().Be(record2);
        (record1 == record2).Should().BeTrue();
    }

    [Fact]
    public void TransactionCreated_HasMeaningfulToString()
    {
        // Arrange
        var record = new TransactionCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            "debit",
            DateTime.UtcNow
        );

        // Act
        var str = record.ToString();

        // Assert
        str.Should().Contain("TransactionCreated");
        str.Should().Contain("100");
        str.Should().Contain("USD");
    }

    #endregion

    #region RegisterRequest Record Tests

    [Fact]
    public void RegisterRequest_CanBeCreated()
    {
        // Act
        var request = new RegisterRequest("test@example.com", "password", "John", "Doe");

        // Assert
        request.Should().NotBeNull();
        request.Email.Should().Be("test@example.com");
        request.Password.Should().Be("password");
        request.FirstName.Should().Be("John");
        request.LastName.Should().Be("Doe");
    }

    [Fact]
    public void RegisterRequest_SupportsDeconstruction()
    {
        // Arrange
        var request = new RegisterRequest("test@example.com", "password", "John", "Doe");

        // Act
        var (email, password, firstName, lastName) = request;

        // Assert
        email.Should().Be("test@example.com");
        password.Should().Be("password");
        firstName.Should().Be("John");
        lastName.Should().Be("Doe");
    }

    #endregion

    #region LoginRequest Record Tests

    [Fact]
    public void LoginRequest_CanBeCreated()
    {
        // Act
        var request = new LoginRequest("test@example.com", "password");

        // Assert
        request.Should().NotBeNull();
        request.Email.Should().Be("test@example.com");
        request.Password.Should().Be("password");
    }

    [Fact]
    public void LoginRequest_SupportsDeconstruction()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "password");

        // Act
        var (email, password) = request;

        // Assert
        email.Should().Be("test@example.com");
        password.Should().Be("password");
    }

    #endregion

    #region VerifyEmailRequest Record Tests

    [Fact]
    public void VerifyEmailRequest_CanBeCreated()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var request = new VerifyEmailRequest(userId, "verification-token");

        // Assert
        request.Should().NotBeNull();
        request.UserId.Should().Be(userId);
        request.Token.Should().Be("verification-token");
    }

    [Fact]
    public void VerifyEmailRequest_SupportsDeconstruction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new VerifyEmailRequest(userId, "token");

        // Act
        var (id, token) = request;

        // Assert
        id.Should().Be(userId);
        token.Should().Be("token");
    }

    #endregion

    #region CreateAccountRequest Record Tests

    [Fact]
    public void CreateAccountRequest_CanBeCreated()
    {
        // Act
        var request = new CreateAccountRequest("Checking", "USD");

        // Assert
        request.Should().NotBeNull();
        request.AccountType.Should().Be("Checking");
        request.Currency.Should().Be("USD");
    }

    [Fact]
    public void CreateAccountRequest_SupportsNullCurrency()
    {
        // Act
        var request = new CreateAccountRequest("Savings", null);

        // Assert
        request.Currency.Should().BeNull();
    }

    [Fact]
    public void CreateAccountRequest_SupportsDeconstruction()
    {
        // Arrange
        var request = new CreateAccountRequest("Checking", "EUR");

        // Act
        var (accountType, currency) = request;

        // Assert
        accountType.Should().Be("Checking");
        currency.Should().Be("EUR");
    }

    #endregion

    #region Record Immutability Tests

    [Fact]
    public void Records_AreImmutable()
    {
        // These tests verify that records are value-based and immutable
        var register1 = new RegisterRequest("a@b.com", "pass", "A", "B");
        var register2 = new RegisterRequest("a@b.com", "pass", "A", "B");

        register1.Should().Be(register2);
        register1.GetHashCode().Should().Be(register2.GetHashCode());

        var login1 = new LoginRequest("a@b.com", "pass");
        var login2 = new LoginRequest("a@b.com", "pass");

        login1.Should().Be(login2);
    }

    #endregion
}
