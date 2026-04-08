using Contracts.Events;

namespace Tests;

public class ExternalBankDepositInitiatedTests
{
    [Fact]
    public void Constructor_WithIndividualClient_SetsPropertiesCorrectly()
    {
        // Arrange
        var depositId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalBankAccountId = Guid.NewGuid();
        var amount = 1000.50m;
        var currency = "NZD";
        var initiatedAt = DateTime.UtcNow;

        // Act
        var evt = new ExternalBankDepositInitiated(
            depositId,
            accountId,
            userId,
            externalBankAccountId,
            amount,
            currency,
            initiatedAt
        );

        // Assert
        evt.DepositId.Should().Be(depositId);
        evt.AccountId.Should().Be(accountId);
        evt.UserId.Should().Be(userId);
        evt.ExternalBankAccountId.Should().Be(externalBankAccountId);
        evt.Amount.Should().Be(amount);
        evt.Currency.Should().Be(currency);
        evt.InitiatedAt.Should().Be(initiatedAt);
        evt.ClientType.Should().Be(ClientType.Individual);
        evt.OrganisationId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithCorporateClient_SetsPropertiesCorrectly()
    {
        // Arrange
        var depositId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalBankAccountId = Guid.NewGuid();
        var organisationId = Guid.NewGuid();
        var amount = 5000m;
        var currency = "EUR";
        var initiatedAt = DateTime.UtcNow;

        // Act
        var evt = new ExternalBankDepositInitiated(
            depositId,
            accountId,
            userId,
            externalBankAccountId,
            amount,
            currency,
            initiatedAt,
            ClientType.Corporate,
            organisationId
        );

        // Assert
        evt.DepositId.Should().Be(depositId);
        evt.AccountId.Should().Be(accountId);
        evt.UserId.Should().Be(userId);
        evt.ExternalBankAccountId.Should().Be(externalBankAccountId);
        evt.Amount.Should().Be(amount);
        evt.Currency.Should().Be(currency);
        evt.InitiatedAt.Should().Be(initiatedAt);
        evt.ClientType.Should().Be(ClientType.Corporate);
        evt.OrganisationId.Should().Be(organisationId);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var depositId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalBankAccountId = Guid.NewGuid();
        var amount = 1000m;
        var currency = "NZD";
        var initiatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var evt1 = new ExternalBankDepositInitiated(
            depositId, accountId, userId, externalBankAccountId, amount, currency, initiatedAt
        );
        var evt2 = new ExternalBankDepositInitiated(
            depositId, accountId, userId, externalBankAccountId, amount, currency, initiatedAt
        );

        // Act & Assert
        evt1.Should().Be(evt2);
        (evt1 == evt2).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_DifferentDepositId_AreNotEqual()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalBankAccountId = Guid.NewGuid();
        var amount = 1000m;
        var currency = "NZD";
        var initiatedAt = DateTime.UtcNow;

        var evt1 = new ExternalBankDepositInitiated(
            Guid.NewGuid(), accountId, userId, externalBankAccountId, amount, currency, initiatedAt
        );
        var evt2 = new ExternalBankDepositInitiated(
            Guid.NewGuid(), accountId, userId, externalBankAccountId, amount, currency, initiatedAt
        );

        // Act & Assert
        evt1.Should().NotBe(evt2);
        (evt1 != evt2).Should().BeTrue();
    }

    [Fact]
    public void Deconstruct_ExtractsAllProperties()
    {
        // Arrange
        var depositId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var externalBankAccountId = Guid.NewGuid();
        var organisationId = Guid.NewGuid();
        var amount = 2500m;
        var currency = "GBP";
        var initiatedAt = DateTime.UtcNow;

        var evt = new ExternalBankDepositInitiated(
            depositId, accountId, userId, externalBankAccountId, amount, currency, initiatedAt,
            ClientType.Corporate, organisationId
        );

        // Act
        var (d, a, u, e, amt, cur, init, client, org) = evt;

        // Assert
        d.Should().Be(depositId);
        a.Should().Be(accountId);
        u.Should().Be(userId);
        e.Should().Be(externalBankAccountId);
        amt.Should().Be(amount);
        cur.Should().Be(currency);
        init.Should().Be(initiatedAt);
        client.Should().Be(ClientType.Corporate);
        org.Should().Be(organisationId);
    }
}
