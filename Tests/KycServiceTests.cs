using ApiGateway.Models;
using ApiGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class KycServiceTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsPassedStatus()
    {
        // Arrange
        var logger = new Mock<ILogger<KycService>>();
        var service = new KycService(logger.Object);
        var userId = Guid.NewGuid();

        // Act
        var result = await service.ValidateAsync(userId);

        // Assert
        result.Should().Be(KycStatus.Passed);
    }

    [Fact]
    public async Task ValidateAsync_LogsValidationAttempt()
    {
        // Arrange
        var logger = new Mock<ILogger<KycService>>();
        var service = new KycService(logger.Object);
        var userId = Guid.NewGuid();

        // Act
        await service.ValidateAsync(userId);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Running KYC validation")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var logger = new Mock<ILogger<KycService>>();
        var service = new KycService(logger.Object);
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();

        // Act
        var result = await service.ValidateAsync(userId, cts.Token);

        // Assert
        result.Should().Be(KycStatus.Passed);
    }

    [Fact]
    public async Task ValidateAsync_MultipleUsers_ReturnsPassedForAll()
    {
        // Arrange
        var logger = new Mock<ILogger<KycService>>();
        var service = new KycService(logger.Object);
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act & Assert
        foreach (var userId in userIds)
        {
            var result = await service.ValidateAsync(userId);
            result.Should().Be(KycStatus.Passed);
        }
    }
}
