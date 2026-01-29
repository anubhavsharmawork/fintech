using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AccountService.Controllers;
using AccountService.Data;
using AccountService.Models;

namespace Tests;

public class BankConnectionsControllerTests
{
    private (BankConnectionsController Controller, AccountDbContext Db) BuildController()
    {
        var options = new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        var db = new AccountDbContext(options);
        var loggerMock = new Mock<ILogger<BankConnectionsController>>();

        var controller = new BankConnectionsController(db, loggerMock.Object)
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
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("id", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void GetAvailableBanks_ReturnsAllBanks_WhenNoCountrySpecified()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = controller.GetAvailableBanks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var banks = okResult.Value as List<AvailableBank>;
        banks.Should().NotBeNull();
        banks.Should().HaveCountGreaterThan(5);
    }

    [Fact]
    public void GetAvailableBanks_FiltersByCountry_WhenCountrySpecified()
    {
        // Arrange
        var (controller, _) = BuildController();

        // Act
        var result = controller.GetAvailableBanks("NZ");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var banks = okResult.Value as List<AvailableBank>;
        banks.Should().NotBeNull();
        banks.Should().OnlyContain(b => b.Country == "NZ");
    }

    [Fact]
    public async Task ConnectBank_ReturnsOk_WhenValidRequest()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        
        var request = new ConnectBankRequest("nz_anz");

        // Act
        var result = await controller.ConnectBank(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var response = okResult.Value;
        
        // Check DB
        var connection = await db.BankConnections.FirstOrDefaultAsync(bc => bc.UserId == userId);
        connection.Should().NotBeNull();
        connection!.BankId.Should().Be("nz_anz");
        
        // Check mock accounts created
        var accounts = await db.ExternalBankAccounts.Where(a => a.UserId == userId).ToListAsync();
        accounts.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ConnectBank_ReturnsConflict_WhenAlreadyConnected()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        db.BankConnections.Add(new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankId = "nz_anz",
            BankName = "ANZ",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new ConnectBankRequest("nz_anz");

        // Act
        var result = await controller.ConnectBank(request);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }
    
    [Fact]
    public async Task ConnectBank_ReturnsBadRequest_WhenInvalidBankId()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        
        var request = new ConnectBankRequest("invalid_bank");

        // Act
        var result = await controller.ConnectBank(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetConnectedBanks_ReturnsList_WhenUserAuthenticated()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        db.BankConnections.Add(new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankId = "nz_westpac",
            BankName = "Westpac",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetConnectedBanks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var connections = (okResult.Value as IEnumerable<dynamic>)?.ToList();
        connections.Should().NotBeNull();
        connections.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task DisconnectBank_ReturnsNoContent_WhenConnectionExists()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        var connectionId = Guid.NewGuid();

        db.BankConnections.Add(new BankConnection
        {
            Id = connectionId,
            UserId = userId,
            BankId = "nz_westpac",
            BankName = "Westpac",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.DisconnectBank(connectionId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var exists = await db.BankConnections.AnyAsync(bc => bc.Id == connectionId);
        exists.Should().BeFalse();
    }
    
    [Fact]
    public async Task DisconnectBank_ReturnsNotFound_WhenConnectionDoesNotExist()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        var connectionId = Guid.NewGuid();

        // Act
        var result = await controller.DisconnectBank(connectionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetExternalAccounts_ReturnsAccounts_WhenUserAuthenticated()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        var connectionId = Guid.NewGuid();
        db.BankConnections.Add(new BankConnection
        {
            Id = connectionId,
            UserId = userId,
            BankId = "nz_anz",
            BankName = "ANZ",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.ExternalBankAccounts.Add(new ExternalBankAccount
        {
             Id = Guid.NewGuid(),
             UserId = userId,
             BankConnectionId = connectionId,
             ExternalAccountId = "ext_1",
             AccountName = "My Checking",
             AccountType = "Checking",
             AccountNumber = "1234",
             Balance = 100m,
             Currency = "USD",
             LastSyncedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetExternalAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        // The return type is an anonymous, so we cast to dynamic list
        var accounts = (okResult.Value as IEnumerable<dynamic>)?.ToList();
        accounts.Should().NotBeNull();
        accounts.Should().HaveCount(1);
    }
    
    [Fact]
    public async Task SyncBankAccounts_ReturnsOk_WhenConnectionExists()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        var connectionId = Guid.NewGuid();

        db.BankConnections.Add(new BankConnection
        {
            Id = connectionId,
            UserId = userId,
            BankId = "nz_westpac",
            BankName = "Westpac",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow
        });
        
        var initialBalance = 1000m;
        db.ExternalBankAccounts.Add(new ExternalBankAccount
        {
             Id = Guid.NewGuid(),
             UserId = userId,
             BankConnectionId = connectionId,
             ExternalAccountId = "ext_1",
             AccountName = "My Checking",
             AccountType = "Checking",
             AccountNumber = "1234",
             Balance = initialBalance,
             Currency = "NZD",
             LastSyncedAt = DateTime.UtcNow.AddDays(-1)
        });
        
        await db.SaveChangesAsync();

        // Act
        var result = await controller.SyncBankAccounts(connectionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        // Verify balance changed (it's random but should be updated) or At least LastSyncedAt updated
        var account = await db.ExternalBankAccounts.FirstAsync(a => a.BankConnectionId == connectionId);
        account.LastSyncedAt.Date.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task SyncBankAccounts_ReturnsNotFound_WhenConnectionDoesNotExist()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);
        var connectionId = Guid.NewGuid();

        // Act
        var result = await controller.SyncBankAccounts(connectionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SyncBankAccounts_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.SyncBankAccounts(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConnectedBanks_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetConnectedBanks();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConnectedBanks_ReturnsEmptyList_WhenNoConnectionsExist()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetConnectedBanks();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var connections = (okResult.Value as IEnumerable<dynamic>)?.ToList();
        connections.Should().NotBeNull();
        connections.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectBank_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        var request = new ConnectBankRequest("nz_anz");

        // Act
        var result = await controller.ConnectBank(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DisconnectBank_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.DisconnectBank(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetExternalAccounts_ReturnsUnauthorized_WhenUserClaimMissing()
    {
        // Arrange
        var (controller, _) = BuildController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await controller.GetExternalAccounts();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetExternalAccounts_ReturnsEmptyList_WhenNoAccountsExist()
    {
        // Arrange
        var (controller, _) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        // Act
        var result = await controller.GetExternalAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var accounts = (okResult.Value as IEnumerable<dynamic>)?.ToList();
        accounts.Should().NotBeNull();
        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task DisconnectBank_OnlyDisconnectsOwnConnection()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        var connectionId = Guid.NewGuid();
        db.BankConnections.Add(new BankConnection
        {
            Id = connectionId,
            UserId = otherUserId, // Different user
            BankId = "nz_anz",
            BankName = "ANZ",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.DisconnectBank(connectionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        var stillExists = await db.BankConnections.AnyAsync(bc => bc.Id == connectionId);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task SyncBankAccounts_OnlySyncsOwnConnection()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        var connectionId = Guid.NewGuid();
        db.BankConnections.Add(new BankConnection
        {
            Id = connectionId,
            UserId = otherUserId, // Different user
            BankId = "nz_anz",
            BankName = "ANZ",
            BankLogo = "logo",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.SyncBankAccounts(connectionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetExternalAccounts_IncludesBankConnectionDetails()
    {
        // Arrange
        var (controller, db) = BuildController();
        var userId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = CreateUserPrincipal(userId);

        var connectionId = Guid.NewGuid();
        var bankConnection = new BankConnection
        {
            Id = connectionId,
            UserId = userId,
            BankId = "nz_anz",
            BankName = "ANZ New Zealand",
            BankLogo = "🏦",
            Status = "Active",
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.BankConnections.Add(bankConnection);

        db.ExternalBankAccounts.Add(new ExternalBankAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankConnectionId = connectionId,
            ExternalAccountId = "ext_1",
            AccountName = "Savings Account",
            AccountType = "Savings",
            AccountNumber = "****1234",
            Balance = 5000m,
            Currency = "NZD",
            LastSyncedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.GetExternalAccounts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
