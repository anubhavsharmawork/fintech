using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace Tests;

/// <summary>
/// Unit tests for TransactionService caching behavior.
/// Verifies cache-aside pattern: check cache first, fall back to database, write to cache.
/// </summary>
public class TransactionsCachingTests
{
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger;
    private readonly Mock<IPublishEndpoint> _mockPublisher;
    private readonly Mock<IAmlScreeningChannel> _mockAmlChannel;

    public TransactionsCachingTests()
    {
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
        _mockPublisher = new Mock<IPublishEndpoint>();
        _mockAmlChannel = new Mock<IAmlScreeningChannel>();
        _mockAmlChannel.Setup(a => a.TryEnqueue(It.IsAny<Transaction>())).Returns(true);
    }

    private (TransactionsController Controller, TransactionDbContext Db) BuildController()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new TransactionDbContext(options);
        var service = new TransactionService.Services.TransactionService(
            db,
            _mockPublisher.Object,
            _mockAmlChannel.Object,
            _mockCacheService.Object,
            _mockLogger.Object);

        var controller = new TransactionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, db);
    }

    private ClaimsPrincipal CreateUserPrincipal(Guid userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
        return new ClaimsPrincipal(identity);
    }

    #region GetTransactions Caching Tests

    [Fact]
    public async Task GetTransactions_WhenCacheHit_ReturnsCachedData()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:page:1";

        var cachedDtos = new List<TransactionDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 100m, "NZD", "debit", "Test", "Fun", null, DateTime.UtcNow, "Individual", null, null, "Completed")
        };

        _mockCacheService
            .Setup(x => x.GetAsync<List<TransactionDto>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDtos);

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify cache was checked
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify cache was NOT set (because we got a hit)
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TransactionDto>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTransactions_WhenCacheMiss_QueriesDatabaseAndCachesResult()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:page:1";

        // Setup cache miss
        _mockCacheService
            .Setup(x => x.GetAsync<List<TransactionDto>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TransactionDto>?)null);

        // Add transaction to database
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = userId,
            Amount = 100m,
            Currency = "NZD",
            Type = "debit",
            Description = "Test transaction",
            SpendingType = "Fun",
            CreatedAt = DateTime.UtcNow
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetTransactions();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var paged = (TransactionPagedResponse<object>)okResult.Value!;
        var items = paged.Data.ToList();
        items.Should().HaveCount(1);

        // Verify cache was checked first
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify result was cached with 2 minute TTL
        _mockCacheService.Verify(x => x.SetAsync(cacheKey, It.IsAny<List<TransactionDto>>(), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransactions_WithFilters_DoesNotUseCache()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // Add transaction to database
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            Amount = 100m,
            Currency = "NZD",
            Type = "debit",
            Description = "Test",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act - request with accountId filter
        var result = await controller.GetTransactions(accountId: accountId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify cache was NOT checked (filtered queries bypass cache)
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify cache was NOT set
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TransactionDto>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTransaction_InvalidatesCacheForUser()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:page:1";

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        var request = new CreatePaymentRequestDto
        {
            AccountId = accountId,
            Amount = 100m,
            Currency = "NZD",
            Type = "debit",
            Description = "Test payment"
        };

        // Act
        var result = await controller.CreateTransaction(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify cache was invalidated for page 1
        _mockCacheService.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransactions_DifferentPages_UseDifferentCacheKeys()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();

        _mockCacheService
            .Setup(x => x.GetAsync<List<TransactionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TransactionDto>?)null);

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act - request page 1
        await controller.GetTransactions(page: 1);

        // Act - request page 2
        await controller.GetTransactions(page: 2);

        // Assert - verify different cache keys were used
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>($"user:{userId}:page:1", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>($"user:{userId}:page:2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransactions_CacheKeyIsNamespacedByUserId()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        _mockCacheService
            .Setup(x => x.GetAsync<List<TransactionDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<TransactionDto>?)null);

        // First user request
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId1);
        await controller.GetTransactions();

        // Second user request
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId2);
        await controller.GetTransactions();

        // Assert - verify different cache keys were used for different users
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>($"user:{userId1}:page:1", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(x => x.GetAsync<List<TransactionDto>>($"user:{userId2}:page:1", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

