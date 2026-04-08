using ApiGateway.Models.Dtos;

namespace Tests;

public class SanctionRequestDtoTests
{
    [Fact]
    public void Constructor_WithAllParameters_CreatesValidRecord()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var updatedAt = DateTimeOffset.UtcNow;

        var dto = new SanctionRequestDto(
            Id: id,
            ExternalProjectId: "proj-123",
            ExternalTenantId: "tenant-456",
            UserId: userId,
            AccountId: accountId,
            RequestedAmount: 5000m,
            Currency: "NZD",
            Purpose: "Equipment purchase",
            RiskScore: 25,
            KycStatus: "verified",
            AmlStatus: "clear",
            Status: "Approved",
            ApprovedAmount: 5000m,
            DecisionReason: "Low risk profile",
            FtkTransactionRef: "ftk-tx-789",
            IdempotencyKey: "idem-key-001",
            CreatedAt: createdAt,
            UpdatedAt: updatedAt,
            CreatedBy: "admin@example.com"
        );

        dto.Id.Should().Be(id);
        dto.ExternalProjectId.Should().Be("proj-123");
        dto.ExternalTenantId.Should().Be("tenant-456");
        dto.UserId.Should().Be(userId);
        dto.AccountId.Should().Be(accountId);
        dto.RequestedAmount.Should().Be(5000m);
        dto.Currency.Should().Be("NZD");
        dto.Purpose.Should().Be("Equipment purchase");
        dto.RiskScore.Should().Be(25);
        dto.KycStatus.Should().Be("verified");
        dto.AmlStatus.Should().Be("clear");
        dto.Status.Should().Be("Approved");
        dto.ApprovedAmount.Should().Be(5000m);
        dto.DecisionReason.Should().Be("Low risk profile");
        dto.FtkTransactionRef.Should().Be("ftk-tx-789");
        dto.IdempotencyKey.Should().Be("idem-key-001");
        dto.CreatedAt.Should().Be(createdAt);
        dto.UpdatedAt.Should().Be(updatedAt);
        dto.CreatedBy.Should().Be("admin@example.com");
    }

    [Fact]
    public void Constructor_WithNullOptionalFields_AcceptsNull()
    {
        var dto = new SanctionRequestDto(
            Id: Guid.NewGuid(),
            ExternalProjectId: "proj-123",
            ExternalTenantId: "tenant-456",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: 3000m,
            Currency: "NZD",
            Purpose: "Test purpose",
            RiskScore: 10,
            KycStatus: "pending",
            AmlStatus: "pending",
            Status: "Draft",
            ApprovedAmount: null,
            DecisionReason: null,
            FtkTransactionRef: null,
            IdempotencyKey: "idem-key-002",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedBy: "user@example.com"
        );

        dto.ApprovedAmount.Should().BeNull();
        dto.DecisionReason.Should().BeNull();
        dto.FtkTransactionRef.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var dto1 = new SanctionRequestDto(
            Id: id,
            ExternalProjectId: "proj-1",
            ExternalTenantId: "tenant-1",
            UserId: userId,
            AccountId: accountId,
            RequestedAmount: 1000m,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: 5,
            KycStatus: "verified",
            AmlStatus: "clear",
            Status: "Approved",
            ApprovedAmount: 1000m,
            DecisionReason: "OK",
            FtkTransactionRef: "tx-1",
            IdempotencyKey: "key-1",
            CreatedAt: createdAt,
            UpdatedAt: createdAt,
            CreatedBy: "admin"
        );

        var dto2 = new SanctionRequestDto(
            Id: id,
            ExternalProjectId: "proj-1",
            ExternalTenantId: "tenant-1",
            UserId: userId,
            AccountId: accountId,
            RequestedAmount: 1000m,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: 5,
            KycStatus: "verified",
            AmlStatus: "clear",
            Status: "Approved",
            ApprovedAmount: 1000m,
            DecisionReason: "OK",
            FtkTransactionRef: "tx-1",
            IdempotencyKey: "key-1",
            CreatedAt: createdAt,
            UpdatedAt: createdAt,
            CreatedBy: "admin"
        );

        dto1.Should().Be(dto2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var dto1 = new SanctionRequestDto(
            Id: Guid.NewGuid(),
            ExternalProjectId: "proj-1",
            ExternalTenantId: "tenant-1",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: 1000m,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: 5,
            KycStatus: "verified",
            AmlStatus: "clear",
            Status: "Approved",
            ApprovedAmount: 1000m,
            DecisionReason: "OK",
            FtkTransactionRef: "tx-1",
            IdempotencyKey: "key-1",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedBy: "admin"
        );

        var dto2 = new SanctionRequestDto(
            Id: Guid.NewGuid(), // Different ID
            ExternalProjectId: "proj-1",
            ExternalTenantId: "tenant-1",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: 1000m,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: 5,
            KycStatus: "verified",
            AmlStatus: "clear",
            Status: "Approved",
            ApprovedAmount: 1000m,
            DecisionReason: "OK",
            FtkTransactionRef: "tx-1",
            IdempotencyKey: "key-1",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedBy: "admin"
        );

        dto1.Should().NotBe(dto2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(999999999.99)]
    public void RequestedAmount_AcceptsVariousValues(decimal amount)
    {
        var dto = new SanctionRequestDto(
            Id: Guid.NewGuid(),
            ExternalProjectId: "proj",
            ExternalTenantId: "tenant",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: amount,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: 0,
            KycStatus: "pending",
            AmlStatus: "pending",
            Status: "Draft",
            ApprovedAmount: null,
            DecisionReason: null,
            FtkTransactionRef: null,
            IdempotencyKey: "key",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedBy: "user"
        );

        dto.RequestedAmount.Should().Be(amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void RiskScore_AcceptsValidRanges(int score)
    {
        var dto = new SanctionRequestDto(
            Id: Guid.NewGuid(),
            ExternalProjectId: "proj",
            ExternalTenantId: "tenant",
            UserId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            RequestedAmount: 1000m,
            Currency: "NZD",
            Purpose: "Test",
            RiskScore: score,
            KycStatus: "pending",
            AmlStatus: "pending",
            Status: "Draft",
            ApprovedAmount: null,
            DecisionReason: null,
            FtkTransactionRef: null,
            IdempotencyKey: "key",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CreatedBy: "user"
        );

        dto.RiskScore.Should().Be(score);
    }
}
