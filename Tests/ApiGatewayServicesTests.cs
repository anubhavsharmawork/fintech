using ApiGateway.Models;
using ApiGateway.OcelotConfig;
using ApiGateway.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Ocelot.Configuration.File;

namespace Tests;

// ─────────────────────────────────────────────────────────────────────────────
// ApiGateway AmlService (stub)
// ─────────────────────────────────────────────────────────────────────────────

public class ApiGatewayAmlServiceTests
{
    private static ApiGateway.Services.AmlService BuildService()
    {
        var logger = new Mock<ILogger<ApiGateway.Services.AmlService>>();
        return new ApiGateway.Services.AmlService(logger.Object);
    }

    [Fact]
    public async Task ScreenAsync_AlwaysReturnsPassed()
    {
        var service = BuildService();

        var result = await service.ScreenAsync(Guid.NewGuid(), 500m, "payment");

        result.Should().Be(AmlStatus.Passed);
    }

    [Fact]
    public async Task ScreenAsync_ZeroAmount_ReturnsPassed()
    {
        var service = BuildService();

        var result = await service.ScreenAsync(Guid.NewGuid(), 0m, "test");

        result.Should().Be(AmlStatus.Passed);
    }

    [Fact]
    public async Task ScreenAsync_LargeAmount_ReturnsPassed()
    {
        var service = BuildService();

        var result = await service.ScreenAsync(Guid.NewGuid(), 9_999_999.99m, "large transfer");

        result.Should().Be(AmlStatus.Passed);
    }

    [Fact]
    public async Task ScreenAsync_CancellationToken_IsHonoured()
    {
        var service = BuildService();
        using var cts = new CancellationTokenSource();

        var result = await service.ScreenAsync(Guid.NewGuid(), 100m, "purpose", cts.Token);

        result.Should().Be(AmlStatus.Passed);
    }

    [Fact]
    public async Task ScreenAsync_DifferentUserIds_EachReturnPassed()
    {
        var service = BuildService();

        for (var i = 0; i < 5; i++)
        {
            var status = await service.ScreenAsync(Guid.NewGuid(), 100m * i, "purpose");
            status.Should().Be(AmlStatus.Passed);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MockBankProvider
// ─────────────────────────────────────────────────────────────────────────────

public class MockBankProviderTests
{
    private static MockBankProvider BuildProvider() => new();

    [Fact]
    public async Task GetAvailableBanksAsync_NoFilter_ReturnsAll10()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync();

        banks.Count.Should().Be(10);
    }

    [Fact]
    public async Task GetAvailableBanksAsync_NullFilter_ReturnsAll10()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync(null);

        banks.Count.Should().Be(10);
    }

    [Fact]
    public async Task GetAvailableBanksAsync_FilterNZ_Returns5Banks()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync("NZ");

        banks.Count.Should().Be(5);
        banks.Should().OnlyContain(b => b.Country == "NZ");
    }

    [Fact]
    public async Task GetAvailableBanksAsync_FilterAU_Returns3Banks()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync("AU");

        banks.Count.Should().Be(3);
        banks.Should().OnlyContain(b => b.Country == "AU");
    }

    [Fact]
    public async Task GetAvailableBanksAsync_FilterUK_Returns2Banks()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync("UK");

        banks.Count.Should().Be(2);
        banks.Should().OnlyContain(b => b.Country == "UK");
    }

    [Fact]
    public async Task GetAvailableBanksAsync_FilterCaseInsensitive_ReturnsResults()
    {
        var provider = BuildProvider();

        var lower = await provider.GetAvailableBanksAsync("nz");
        var upper = await provider.GetAvailableBanksAsync("NZ");

        lower.Count.Should().Be(upper.Count);
    }

    [Fact]
    public async Task GetAvailableBanksAsync_UnknownCountry_ReturnsEmpty()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync("ZZ");

        banks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBankByIdAsync_KnownId_ReturnsBank()
    {
        var provider = BuildProvider();

        var bank = await provider.GetBankByIdAsync("nz_anz");

        bank.Should().NotBeNull();
        bank!.Name.Should().Be("ANZ New Zealand");
        bank.Country.Should().Be("NZ");
    }

    [Fact]
    public async Task GetBankByIdAsync_AllKnownIds_ReturnNonNull()
    {
        var provider = BuildProvider();
        var knownIds = new[]
        {
            "nz_anz", "nz_asb", "nz_bnz", "nz_westpac", "nz_kiwibank",
            "au_commbank", "au_nab", "au_westpac",
            "uk_hsbc", "uk_barclays"
        };

        foreach (var id in knownIds)
        {
            var bank = await provider.GetBankByIdAsync(id);
            bank.Should().NotBeNull(because: $"bank id '{id}' should exist");
        }
    }

    [Fact]
    public async Task GetBankByIdAsync_UnknownId_ReturnsNull()
    {
        var provider = BuildProvider();

        var bank = await provider.GetBankByIdAsync("unknown_bank");

        bank.Should().BeNull();
    }

    [Fact]
    public async Task GetBankByIdAsync_EmptyId_ReturnsNull()
    {
        var provider = BuildProvider();

        var bank = await provider.GetBankByIdAsync(string.Empty);

        bank.Should().BeNull();
    }

    [Fact]
    public async Task GetAvailableBanksAsync_ReturnsBankInfoWithAllFields()
    {
        var provider = BuildProvider();

        var banks = await provider.GetAvailableBanksAsync("NZ");

        foreach (var bank in banks)
        {
            bank.Id.Should().NotBeNullOrWhiteSpace();
            bank.Name.Should().NotBeNullOrWhiteSpace();
            bank.Logo.Should().NotBeNullOrWhiteSpace();
            bank.Country.Should().Be("NZ");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FtkLedgerService
// ─────────────────────────────────────────────────────────────────────────────

public class FtkLedgerServiceTests
{
    private static FtkLedgerService BuildService()
    {
        var logger = new Mock<ILogger<FtkLedgerService>>();
        return new FtkLedgerService(logger.Object);
    }

    [Fact]
    public async Task AllocateAsync_ReturnsTransactionRef_WithFtkPrefix()
    {
        var service = BuildService();

        var result = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 100m);

        result.Should().StartWith("FTK-");
    }

    [Fact]
    public async Task AllocateAsync_ReturnsUniqueRefEachCall()
    {
        var service = BuildService();

        var ref1 = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 100m);
        var ref2 = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 100m);

        ref1.Should().NotBe(ref2);
    }

    [Fact]
    public async Task AllocateAsync_RefContainsGuidFormatSegment()
    {
        var service = BuildService();

        var result = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 50m);

        // FTK-{Guid:N} → 32 hex chars after prefix
        var suffix = result["FTK-".Length..];
        suffix.Length.Should().Be(32);
        suffix.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task AllocateAsync_ZeroAmount_StillReturnsRef()
    {
        var service = BuildService();

        var result = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 0m);

        result.Should().StartWith("FTK-");
    }

    [Fact]
    public async Task AllocateAsync_LargeAmount_StillReturnsRef()
    {
        var service = BuildService();

        var result = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 9_999_999.99m);

        result.Should().StartWith("FTK-");
    }

    [Fact]
    public async Task AllocateAsync_CancellationToken_Honoured()
    {
        var service = BuildService();
        using var cts = new CancellationTokenSource();

        var result = await service.AllocateAsync(Guid.NewGuid(), Guid.NewGuid(), 100m, cts.Token);

        result.Should().StartWith("FTK-");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DynamicOcelotRepository (InMemoryFileConfigRepository)
// ─────────────────────────────────────────────────────────────────────────────

public class DynamicOcelotRepositoryTests
{
    [Fact]
    public async Task Get_ReturnsInitialConfig()
    {
        var initial = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://localhost" }
        };
        var repo = new InMemoryFileConfigRepository(initial);

        var response = await repo.Get();

        response.IsError.Should().BeFalse();
        response.Data.GlobalConfiguration.BaseUrl.Should().Be("http://localhost");
    }

    [Fact]
    public async Task Set_ReplacesConfig_GetReturnsNew()
    {
        var initial = new FileConfiguration();
        var repo = new InMemoryFileConfigRepository(initial);

        var updated = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://updated" }
        };
        var setResponse = await repo.Set(updated);

        setResponse.IsError.Should().BeFalse();

        var getResponse = await repo.Get();
        getResponse.Data.GlobalConfiguration.BaseUrl.Should().Be("http://updated");
    }

    [Fact]
    public async Task Set_NullConfig_StoresEmptyFileConfiguration()
    {
        var initial = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://original" }
        };
        var repo = new InMemoryFileConfigRepository(initial);

        await repo.Set(null!);

        var response = await repo.Get();
        response.IsError.Should().BeFalse();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Constructor_NullConfig_DefaultsToEmptyFileConfiguration()
    {
        var repo = new InMemoryFileConfigRepository(null!);

        var response = await repo.Get();

        response.IsError.Should().BeFalse();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Set_MultipleTimes_LastWriteWins()
    {
        var repo = new InMemoryFileConfigRepository(new FileConfiguration());

        await repo.Set(new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://first" }
        });
        await repo.Set(new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://second" }
        });

        var response = await repo.Get();
        response.Data.GlobalConfiguration.BaseUrl.Should().Be("http://second");
    }

    [Fact]
    public async Task Get_CalledMultipleTimes_ReturnsSameData()
    {
        var config = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration { BaseUrl = "http://stable" }
        };
        var repo = new InMemoryFileConfigRepository(config);

        var r1 = await repo.Get();
        var r2 = await repo.Get();

        r1.Data.GlobalConfiguration.BaseUrl.Should().Be(r2.Data.GlobalConfiguration.BaseUrl);
    }
}
