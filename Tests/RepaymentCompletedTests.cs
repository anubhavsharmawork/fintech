using Contracts.Events;

namespace Tests;

public class RepaymentCompletedTests
{
    [Fact]
    public void Constructor_WithAllParameters_CreatesValidRecord()
    {
        var repaymentId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        var evt = new RepaymentCompleted(
            RepaymentId: repaymentId,
            FacilityId: facilityId,
            UserId: userId,
            WalletAddress: "0x1234567890123456789012345678901234567890",
            Amount: 500m,
            Currency: "NZD",
            OutstandingBalance: 1500m,
            Status: "Completed",
            CompletedAt: completedAt
        );

        evt.RepaymentId.Should().Be(repaymentId);
        evt.FacilityId.Should().Be(facilityId);
        evt.UserId.Should().Be(userId);
        evt.WalletAddress.Should().Be("0x1234567890123456789012345678901234567890");
        evt.Amount.Should().Be(500m);
        evt.Currency.Should().Be("NZD");
        evt.OutstandingBalance.Should().Be(1500m);
        evt.Status.Should().Be("Completed");
        evt.CompletedAt.Should().Be(completedAt);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(100.00)]
    [InlineData(50000.00)]
    [InlineData(999999999.99)]
    public void Amount_AcceptsVariousValues(decimal amount)
    {
        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0xABC",
            Amount: amount,
            Currency: "NZD",
            OutstandingBalance: 0m,
            Status: "Completed",
            CompletedAt: DateTime.UtcNow
        );

        evt.Amount.Should().Be(amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000.50)]
    [InlineData(999999.99)]
    public void OutstandingBalance_AcceptsVariousValues(decimal balance)
    {
        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0xDEF",
            Amount: 100m,
            Currency: "NZD",
            OutstandingBalance: balance,
            Status: "Completed",
            CompletedAt: DateTime.UtcNow
        );

        evt.OutstandingBalance.Should().Be(balance);
    }

    [Theory]
    [InlineData("NZD")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("AUD")]
    public void Currency_AcceptsVariousCodes(string currency)
    {
        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0x123",
            Amount: 100m,
            Currency: currency,
            OutstandingBalance: 0m,
            Status: "Completed",
            CompletedAt: DateTime.UtcNow
        );

        evt.Currency.Should().Be(currency);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Processing")]
    public void Status_AcceptsVariousValues(string status)
    {
        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0x123",
            Amount: 100m,
            Currency: "NZD",
            OutstandingBalance: 0m,
            Status: status,
            CompletedAt: DateTime.UtcNow
        );

        evt.Status.Should().Be(status);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var repaymentId = Guid.NewGuid();
        var facilityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        var evt1 = new RepaymentCompleted(
            RepaymentId: repaymentId,
            FacilityId: facilityId,
            UserId: userId,
            WalletAddress: "0xABC",
            Amount: 500m,
            Currency: "NZD",
            OutstandingBalance: 1500m,
            Status: "Completed",
            CompletedAt: completedAt
        );

        var evt2 = new RepaymentCompleted(
            RepaymentId: repaymentId,
            FacilityId: facilityId,
            UserId: userId,
            WalletAddress: "0xABC",
            Amount: 500m,
            Currency: "NZD",
            OutstandingBalance: 1500m,
            Status: "Completed",
            CompletedAt: completedAt
        );

        evt1.Should().Be(evt2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var completedAt = DateTime.UtcNow;

        var evt1 = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0xABC",
            Amount: 500m,
            Currency: "NZD",
            OutstandingBalance: 1500m,
            Status: "Completed",
            CompletedAt: completedAt
        );

        var evt2 = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(), // Different ID
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0xABC",
            Amount: 500m,
            Currency: "NZD",
            OutstandingBalance: 1500m,
            Status: "Completed",
            CompletedAt: completedAt
        );

        evt1.Should().NotBe(evt2);
    }

    [Fact]
    public void WalletAddress_AcceptsValidEthereumAddress()
    {
        var validAddress = "0x742d35Cc6634C0532925a3b844Bc9e7595f2bD70";

        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: validAddress,
            Amount: 100m,
            Currency: "NZD",
            OutstandingBalance: 0m,
            Status: "Completed",
            CompletedAt: DateTime.UtcNow
        );

        evt.WalletAddress.Should().Be(validAddress);
    }

    [Fact]
    public void CompletedAt_StoresDateTimeCorrectly()
    {
        var specificTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        var evt = new RepaymentCompleted(
            RepaymentId: Guid.NewGuid(),
            FacilityId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            WalletAddress: "0x123",
            Amount: 100m,
            Currency: "NZD",
            OutstandingBalance: 0m,
            Status: "Completed",
            CompletedAt: specificTime
        );

        evt.CompletedAt.Should().Be(specificTime);
    }
}
