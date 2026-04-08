using ApiGateway.Configuration;
using ApiGateway.Models;
using ApiGateway.Services.Underwriting;
using Microsoft.Extensions.Options;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Stores;
using Microsoft.EntityFrameworkCore;
using UserService.Services;
using System.Security.Cryptography;
using System.Text;

namespace Tests;

// ── Underwriting Rules ─────────────────────────────────────────────────────

public class UnderwritingRuleTests
{
    private static IOptions<UnderwritingSettings> Opts(UnderwritingSettings? s = null) =>
        Options.Create(s ?? new UnderwritingSettings
        {
            AmountThreshold = 50_000m,
            AmountThresholdRiskPoints = 30,
            KycPassedRiskReduction = 10,
            AmlPassedRiskReduction = 10,
            NoPriorApprovalsRiskPoints = 10,
            MaxAcceptableRiskScore = 60
        });

    private static SanctionRequest MakeRequest(
        decimal amount = 10_000m,
        KycStatus kyc = KycStatus.Pending,
        AmlStatus aml = AmlStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        ExternalProjectId = "p",
        ExternalTenantId = "t",
        UserId = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        RequestedAmount = amount,
        Purpose = "test",
        IdempotencyKey = Guid.NewGuid().ToString(),
        CreatedBy = "test",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        KycStatus = kyc,
        AmlStatus = aml
    };

    // ── AmountThresholdRule ────────────────────────────────────────────────

    [Fact]
    public void AmountThresholdRule_ReturnsPoints_WhenAmountExceedsThreshold()
    {
        var rule = new AmountThresholdRule(Opts());
        var req = MakeRequest(amount: 100_000m);

        rule.Evaluate(req, 0).Should().Be(30);
    }

    [Fact]
    public void AmountThresholdRule_ReturnsZero_WhenAmountBelowThreshold()
    {
        var rule = new AmountThresholdRule(Opts());
        var req = MakeRequest(amount: 1_000m);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    [Fact]
    public void AmountThresholdRule_ReturnsZero_WhenAmountEqualsThreshold()
    {
        var rule = new AmountThresholdRule(Opts());
        var req = MakeRequest(amount: 50_000m);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    // ── KycPassedRule ──────────────────────────────────────────────────────

    [Fact]
    public void KycPassedRule_ReturnsNegativePoints_WhenKycPassed()
    {
        var rule = new KycPassedRule(Opts());
        var req = MakeRequest(kyc: KycStatus.Passed);

        rule.Evaluate(req, 0).Should().Be(-10);
    }

    [Fact]
    public void KycPassedRule_ReturnsZero_WhenKycFailed()
    {
        var rule = new KycPassedRule(Opts());
        var req = MakeRequest(kyc: KycStatus.Failed);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    [Fact]
    public void KycPassedRule_ReturnsZero_WhenKycPending()
    {
        var rule = new KycPassedRule(Opts());
        var req = MakeRequest(kyc: KycStatus.Pending);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    // ── AmlPassedRule ──────────────────────────────────────────────────────

    [Fact]
    public void AmlPassedRule_ReturnsNegativePoints_WhenAmlPassed()
    {
        var rule = new AmlPassedRule(Opts());
        var req = MakeRequest(aml: AmlStatus.Passed);

        rule.Evaluate(req, 0).Should().Be(-10);
    }

    [Fact]
    public void AmlPassedRule_ReturnsZero_WhenAmlFlagged()
    {
        var rule = new AmlPassedRule(Opts());
        var req = MakeRequest(aml: AmlStatus.Flagged);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    [Fact]
    public void AmlPassedRule_ReturnsZero_WhenAmlPending()
    {
        var rule = new AmlPassedRule(Opts());
        var req = MakeRequest(aml: AmlStatus.Pending);

        rule.Evaluate(req, 0).Should().Be(0);
    }

    // ── NoPriorApprovalsRule ───────────────────────────────────────────────

    [Fact]
    public void NoPriorApprovalsRule_ReturnsPoints_WhenNoPriorApprovals()
    {
        var rule = new NoPriorApprovalsRule(Opts());
        var req = MakeRequest();

        rule.Evaluate(req, priorApprovedCount: 0).Should().Be(10);
    }

    [Fact]
    public void NoPriorApprovalsRule_ReturnsZero_WhenHasPriorApprovals()
    {
        var rule = new NoPriorApprovalsRule(Opts());
        var req = MakeRequest();

        rule.Evaluate(req, priorApprovedCount: 3).Should().Be(0);
    }

    // ── Custom settings propagation ────────────────────────────────────────

    [Fact]
    public void AmountThresholdRule_UsesCustomSettings()
    {
        var rule = new AmountThresholdRule(Opts(new UnderwritingSettings
        {
            AmountThreshold = 1_000m,
            AmountThresholdRiskPoints = 99
        }));
        var req = MakeRequest(amount: 2_000m);

        rule.Evaluate(req, 0).Should().Be(99);
    }
}

// ── NotificationPreferenceService ─────────────────────────────────────────

public class NotificationPreferenceServiceTests
{
    private static NotificationDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetPreferences_ReturnsEmpty_WhenNoneExist()
    {
        var db = BuildDb();
        var svc = new NotificationPreferenceService(db);

        var result = await svc.GetPreferences(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdatePreference_CreatesNew_WhenNotExists()
    {
        var db = BuildDb();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdatePreference(userId, "payment.approved", emailEnabled: true, smsEnabled: false);

        var prefs = await svc.GetPreferences(userId);
        prefs.Should().HaveCount(1);
        prefs[0].EmailEnabled.Should().BeTrue();
        prefs[0].SmsEnabled.Should().BeFalse();
        prefs[0].EventType.Should().Be("payment.approved");
    }

    [Fact]
    public async Task UpdatePreference_UpdatesExisting_WhenAlreadyExists()
    {
        var db = BuildDb();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdatePreference(userId, "payment.approved", emailEnabled: true, smsEnabled: false);
        await svc.UpdatePreference(userId, "payment.approved", emailEnabled: false, smsEnabled: true);

        var prefs = await svc.GetPreferences(userId);
        prefs.Should().HaveCount(1);
        prefs[0].EmailEnabled.Should().BeFalse();
        prefs[0].SmsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetPreferences_ReturnsOnlyMatchingUserId()
    {
        var db = BuildDb();
        var svc = new NotificationPreferenceService(db);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await svc.UpdatePreference(userId1, "payment.approved", true, true);
        await svc.UpdatePreference(userId2, "payment.approved", false, false);

        var prefs1 = await svc.GetPreferences(userId1);
        prefs1.Should().HaveCount(1);
        prefs1[0].EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePreference_CanStoreMultipleEventTypes()
    {
        var db = BuildDb();
        var svc = new NotificationPreferenceService(db);
        var userId = Guid.NewGuid();

        await svc.UpdatePreference(userId, "payment.approved", true, false);
        await svc.UpdatePreference(userId, "payment.rejected", false, true);

        var prefs = await svc.GetPreferences(userId);
        prefs.Should().HaveCount(2);
    }
}

// ── RecentNotificationStore ────────────────────────────────────────────────

public class RecentNotificationStoreTests
{
    [Fact]
    public void GetRecent_ReturnsEmpty_WhenNoEntriesForUser()
    {
        var store = new RecentNotificationStore();

        var result = store.GetRecent(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Add_ThenGetRecent_ReturnsMostRecentFirst()
    {
        var store = new RecentNotificationStore();
        var userId = Guid.NewGuid();

        store.Add(userId, "event.a", "First");
        store.Add(userId, "event.b", "Second");

        var result = store.GetRecent(userId);
        result.Should().HaveCount(2);
        result[0].Message.Should().Be("Second");
    }

    [Fact]
    public void GetRecent_RespectsCountParameter()
    {
        var store = new RecentNotificationStore();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
            store.Add(userId, "event", $"Message {i}");

        var result = store.GetRecent(userId, count: 3);
        result.Should().HaveCount(3);
    }

    [Fact]
    public void Add_EvictsOldestEntry_WhenMaxCapacityReached()
    {
        var store = new RecentNotificationStore();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 51; i++)
            store.Add(userId, "event", $"Message {i}");

        var result = store.GetRecent(userId, count: 100);
        result.Should().HaveCount(50);
    }

    [Fact]
    public void Add_IsolatesEntriesPerUser()
    {
        var store = new RecentNotificationStore();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        store.Add(user1, "event", "User1 message");

        store.GetRecent(user2).Should().BeEmpty();
        store.GetRecent(user1).Should().HaveCount(1);
    }

    [Fact]
    public void GetRecent_DefaultCount_ReturnsFive()
    {
        var store = new RecentNotificationStore();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
            store.Add(userId, "event", $"Msg {i}");

        store.GetRecent(userId).Should().HaveCount(5);
    }
}

// ── PasswordHasherService legacy path ─────────────────────────────────────

public class PasswordHasherLegacyTests
{
    [Fact]
    public void Verify_ReturnsFalse_ForMalformedV1Hash()
    {
        var svc = new PasswordHasherService();

        // v1 format but parts[2] is not valid base64
        var result = svc.Verify("password", "v1$310000$!!!notbase64!!!$abc");

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForLegacyHashWithWrongPassword()
    {
        var svc = new PasswordHasherService();
        // Build a real SHA256 legacy hash for "correctpassword"
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("correctpassword" + "salt"));
        var legacyHash = Convert.ToBase64String(bytes);

        svc.Verify("wrongpassword", legacyHash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsTrue_ForLegacyHash()
    {
        var svc = new PasswordHasherService();
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("mypassword" + "salt"));
        var legacyHash = Convert.ToBase64String(bytes);

        svc.Verify("mypassword", legacyHash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForCompletelyInvalidHash()
    {
        var svc = new PasswordHasherService();

        svc.Verify("password", "not-a-hash-at-all!!@@##").Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenV1IterationCountIsNotNumeric()
    {
        var svc = new PasswordHasherService();
        var salt = Convert.ToBase64String(new byte[16]);
        var key = Convert.ToBase64String(new byte[32]);

        svc.Verify("password", $"v1$NaN${salt}${key}").Should().BeFalse();
    }
}
