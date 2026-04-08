using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AccountService.Controllers;
using AccountService.Data;
using AccountService.Policy;
using AccountService.Services;
using Tests.Mocks;

namespace Tests;

/// <summary>
/// Unit tests for AccountService caching behavior.
/// Verifies cache-aside pattern: check cache first, fall back to database, write to cache.
/// </summary>
public class AccountsCachingTests
{
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<AccountService.Services.AccountService>> _mockLogger;

    public AccountsCachingTests()
    {
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<AccountService.Services.AccountService>>();
    }

    private (AccountsController Controller, AccountDbContext Db) BuildController()
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AccountDbContext(options);
        var service = new AccountService.Services.AccountService(db, _mockLogger.Object, _mockCacheService.Object, new AllowAllLimitPolicy());

        var controller = new AccountsController(service)
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

    #region GetAccounts Caching Tests

    [Fact]
    public async Task GetAccounts_WhenCacheHit_ReturnsCachedData()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:list";

        var cachedAccounts = new List<AccountDto>
        {
            new(Guid.NewGuid(), "1234567890", "Checking", 1000m, "NZD", DateTime.UtcNow, "Individual", null, 1000m, 0m)
        };

        _mockCacheService
            .Setup(x => x.GetAsync<List<AccountDto>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedAccounts);

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify cache was checked
        _mockCacheService.Verify(x => x.GetAsync<List<AccountDto>>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify cache was NOT set (because we got a hit)
        _mockCacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<AccountDto>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAccounts_WhenCacheMiss_QueriesDatabaseAndCachesResult()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:list";

        // Setup cache miss
        _mockCacheService
            .Setup(x => x.GetAsync<List<AccountDto>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AccountDto>?)null);

        // Add account to database
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1234567890",
            AccountType = "Checking",
            Balance = 1000m,
            Currency = "NZD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var items = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
        items.Should().HaveCount(1);

        // Verify cache was checked first
        _mockCacheService.Verify(x => x.GetAsync<List<AccountDto>>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify result was cached with 5 minute TTL
        _mockCacheService.Verify(x => x.SetAsync(cacheKey, It.IsAny<List<AccountDto>>(), 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAccount_InvalidatesCacheForUser()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var cacheKey = $"user:{userId}:list";

        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

        var request = new CreateAccountRequest("Checking", "NZD");

        // Act
        var result = await controller.CreateAccount(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify cache was invalidated
        _mockCacheService.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccounts_CacheKeyIsNamespacedByUserId()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        _mockCacheService
            .Setup(x => x.GetAsync<List<AccountDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<AccountDto>?)null);

        // First user request
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId1);
        await controller.GetAccounts();

        // Second user request
        controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId2);
        await controller.GetAccounts();

        // Assert - verify different cache keys were used
        _mockCacheService.Verify(x => x.GetAsync<List<AccountDto>>($"user:{userId1}:list", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheService.Verify(x => x.GetAsync<List<AccountDto>>($"user:{userId2}:list", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
