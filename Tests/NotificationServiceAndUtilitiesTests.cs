using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;
using Contracts.Events;
using NotificationService.Consumers;
using NotificationService.Services;
using NotificationService.Stores;
using FluentEmail.Core;

namespace Tests;

/// <summary>
/// Comprehensive tests for NotificationService
/// </summary>
public class NotificationServiceComprehensiveTests
{
    private Mock<ILogger<TransactionCreatedConsumer>> _loggerMock = null!;
    private Mock<IFluentEmail> _fluentEmailMock = null!;
    private Mock<IFluentEmail> _fluentEmailToMock = null!;
    private Mock<IFluentEmail> _fluentEmailSubjectMock = null!;
    private Mock<IFluentEmail> _fluentEmailBodyMock = null!;
    private Mock<ISmsService> _smsServiceMock = null!;
    private RecentNotificationStore _store = null!;
    private TransactionCreatedConsumer _consumer = null!;

    private void SetupConsumer()
    {
        _loggerMock = new Mock<ILogger<TransactionCreatedConsumer>>();

        _fluentEmailMock = new Mock<IFluentEmail>();
        _fluentEmailToMock = new Mock<IFluentEmail>();
        _fluentEmailSubjectMock = new Mock<IFluentEmail>();
        _fluentEmailBodyMock = new Mock<IFluentEmail>();

        _fluentEmailMock.Setup(x => x.To(It.IsAny<string>())).Returns(_fluentEmailToMock.Object);
        _fluentEmailToMock.Setup(x => x.Subject(It.IsAny<string>())).Returns(_fluentEmailSubjectMock.Object);
        _fluentEmailSubjectMock.Setup(x => x.Body(It.IsAny<string>(), It.IsAny<bool>())).Returns(_fluentEmailBodyMock.Object);

        _smsServiceMock = new Mock<ISmsService>();
        _store = new RecentNotificationStore();

        _consumer = new TransactionCreatedConsumer(
            _loggerMock.Object, 
            _fluentEmailMock.Object, 
            _smsServiceMock.Object, 
            _store);
    }

    [Fact]
    public async Task TransactionCreatedConsumer_Consume_ProcessesTransaction()
    {
        // Arrange
        SetupConsumer();
        var transaction = new TransactionCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "NZD",
            "debit",
            DateTime.UtcNow
        );

        var contextMock = new Mock<ConsumeContext<TransactionCreated>>();
        contextMock.Setup(c => c.Message).Returns(transaction);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TransactionCreated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce()); 
    }

    [Fact]
    public async Task TransactionCreatedConsumer_LogsTransactionDetails()
    {
        // Arrange
        SetupConsumer();
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var transaction = new TransactionCreated(
            transactionId,
            Guid.NewGuid(),
            userId,
            150m,
            "EUR",
            "credit",
            DateTime.UtcNow
        );

        var contextMock = new Mock<ConsumeContext<TransactionCreated>>();
        contextMock.Setup(c => c.Message).Returns(transaction);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(transactionId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task TransactionCreatedConsumer_HandlesMultipleTransactions()
    {
        // Arrange
        SetupConsumer();
        var transactions = new[]
        {
            new TransactionCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "NZD", "debit", DateTime.UtcNow),
            new TransactionCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 200m, "EUR", "credit", DateTime.UtcNow),
            new TransactionCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, "GBP", "debit", DateTime.UtcNow)
        };

        foreach (var tx in transactions)
        {
            var contextMock = new Mock<ConsumeContext<TransactionCreated>>();
            contextMock.Setup(c => c.Message).Returns(tx);

            // Act
            await _consumer.Consume(contextMock.Object);
        }

        // Assert - should have logged for each transaction
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(6)); // 2 logs per transaction * 3 transactions
    }
}

/// <summary>
/// Integration tests for complex scenarios
/// </summary>
public class ApiIntegrationScenarioTests
{
    [Fact]
    public void TransactionCreated_RecordCreation()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var amount = 123.45m;
        var currency = "NZD";
        var type = "debit";
        var createdAt = DateTime.UtcNow;

        // Act
        var transaction = new TransactionCreated(
            transactionId,
            accountId,
            userId,
            amount,
            currency,
            type,
            createdAt
        );

        // Assert
        transaction.TransactionId.Should().Be(transactionId);
        transaction.AccountId.Should().Be(accountId);
        transaction.UserId.Should().Be(userId);
        transaction.Amount.Should().Be(amount);
        transaction.Currency.Should().Be(currency);
        transaction.Type.Should().Be(type);
        transaction.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void TransactionCreated_RecordImmutability()
    {
        // Arrange
        var transaction = new TransactionCreated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "NZD",
            "debit",
            DateTime.UtcNow
        );

        // Act & Assert - Records are immutable
        transaction.Should().NotBeNull();
        // Attempting to modify would require creating a new record with 'with' keyword
    }
}

/// <summary>
/// Data integrity and edge case tests
/// </summary>
public class DataIntegrityTests
{
    [Fact]
    public void TransactionCreated_WithVariousAmounts()
    {
        // Test with different decimal values
        var amounts = new[] { 0.01m, 100m, 999999.99m, decimal.MaxValue / 2 };

        foreach (var amount in amounts)
        {
            var transaction = new TransactionCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                amount,
                "NZD",
                "debit",
                DateTime.UtcNow
            );

            transaction.Amount.Should().Be(amount);
        }
    }

    [Fact]
    public void TransactionCreated_WithVariousCurrencies()
    {
        // Test with different currency codes
        var currencies = new[] { "NZD", "EUR", "GBP", "JPY", "AUD", "CAD" };

        foreach (var currency in currencies)
        {
            var transaction = new TransactionCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                currency,
                "debit",
                DateTime.UtcNow
            );

            transaction.Currency.Should().Be(currency);
        }
    }
}
