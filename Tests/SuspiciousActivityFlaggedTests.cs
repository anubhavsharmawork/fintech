using Contracts.Events;

namespace Tests;

public class SuspiciousActivityFlaggedTests
{
    [Fact]
    public void Constructor_WithAllParameters_CreatesValidRecord()
    {
        var id = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var flaggedAt = DateTime.UtcNow;

        var evt = new SuspiciousActivityFlagged(
            Id: id,
            TransactionId: transactionId,
            UserId: userId,
            AccountId: accountId,
            Amount: 10000m,
            Currency: "NZD",
            Reason: "Large unusual transaction",
            RiskLevel: "High",
            FlaggedAt: flaggedAt
        );

        evt.Id.Should().Be(id);
        evt.TransactionId.Should().Be(transactionId);
        evt.UserId.Should().Be(userId);
        evt.AccountId.Should().Be(accountId);
        evt.Amount.Should().Be(10000m);
        evt.Currency.Should().Be("NZD");
        evt.Reason.Should().Be("Large unusual transaction");
        evt.RiskLevel.Should().Be("High");
        evt.FlaggedAt.Should().Be(flaggedAt);
        evt.ClientType.Should().Be(ClientType.Individual);
        evt.OrganisationId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithClientTypeAndOrganisation_SetsCorrectly()
    {
        var orgId = Guid.NewGuid();

        var evt = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: 50000m,
            Currency: "USD",
            Reason: "Corporate transaction flagged",
            RiskLevel: "Medium",
            FlaggedAt: DateTime.UtcNow,
            ClientType: ClientType.Corporate,
            OrganisationId: orgId
        );

        evt.ClientType.Should().Be(ClientType.Corporate);
        evt.OrganisationId.Should().Be(orgId);
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Critical")]
    public void RiskLevel_AcceptsVariousValues(string riskLevel)
    {
        var evt = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: 1000m,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: riskLevel,
            FlaggedAt: DateTime.UtcNow
        );

        evt.RiskLevel.Should().Be(riskLevel);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(100.00)]
    [InlineData(999999999.99)]
    public void Amount_AcceptsVariousValues(decimal amount)
    {
        var evt = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: amount,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: "Low",
            FlaggedAt: DateTime.UtcNow
        );

        evt.Amount.Should().Be(amount);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var flaggedAt = DateTime.UtcNow;

        var evt1 = new SuspiciousActivityFlagged(
            Id: id,
            TransactionId: txId,
            UserId: userId,
            AccountId: accountId,
            Amount: 1000m,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: "High",
            FlaggedAt: flaggedAt
        );

        var evt2 = new SuspiciousActivityFlagged(
            Id: id,
            TransactionId: txId,
            UserId: userId,
            AccountId: accountId,
            Amount: 1000m,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: "High",
            FlaggedAt: flaggedAt
        );

        evt1.Should().Be(evt2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var evt1 = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: 1000m,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: "High",
            FlaggedAt: DateTime.UtcNow
        );

        var evt2 = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(), // Different ID
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: 1000m,
            Currency: "NZD",
            Reason: "Test",
            RiskLevel: "High",
            FlaggedAt: DateTime.UtcNow
        );

        evt1.Should().NotBe(evt2);
    }

    [Fact]
    public void DefaultClientType_IsIndividual()
    {
        var evt = new SuspiciousActivityFlagged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            Amount: 500m,
            Currency: "NZD",
            Reason: "Default test",
            RiskLevel: "Low",
            FlaggedAt: DateTime.UtcNow
        );

        evt.ClientType.Should().Be(ClientType.Individual);
    }
}
