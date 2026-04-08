using System.Text.Json;
using AccountService.Policy;
using TransactionService.Constants;
using UserService.Converters;
using UserService.Services;

namespace Tests;

public class UtilityAndConstantTests
{
    // ── SpendingTypeConstants.IsValid ──────────────────────────────────────

    [Fact]
    public void IsValid_ReturnsFalse_ForNull()
    {
        SpendingTypeConstants.IsValid(null).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForEmptyString()
    {
        SpendingTypeConstants.IsValid("").Should().BeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForWhitespaceOnly()
    {
        SpendingTypeConstants.IsValid("   ").Should().BeFalse();
    }

    [Theory]
    [InlineData("Fun")]
    [InlineData("Fixed")]
    [InlineData("Future")]
    public void IsValid_ReturnsTrue_ForValidTypes(string type)
    {
        SpendingTypeConstants.IsValid(type).Should().BeTrue();
    }

    [Theory]
    [InlineData("FUN")]
    [InlineData("FIXED")]
    [InlineData("future")]
    [InlineData("fUn")]
    public void IsValid_IsCaseInsensitive(string type)
    {
        SpendingTypeConstants.IsValid(type).Should().BeTrue();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Luxury")]
    [InlineData("Savings")]
    public void IsValid_ReturnsFalse_ForInvalidTypes(string type)
    {
        SpendingTypeConstants.IsValid(type).Should().BeFalse();
    }

    // ── SpendingTypeConstants.Normalize ───────────────────────────────────

    [Fact]
    public void Normalize_ThrowsArgumentNullException_ForNullInput()
    {
        var act = () => SpendingTypeConstants.Normalize(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Normalize_ReturnsTrimmedValue_ForValidInput()
    {
        SpendingTypeConstants.Normalize("  Fun  ").Should().Be("Fun");
    }

    [Fact]
    public void Normalize_ReturnsValue_ForNonWhitespaceInput()
    {
        SpendingTypeConstants.Normalize("Fixed").Should().Be("Fixed");
    }

    // ── Iso8601DateTimeConverter.Read ─────────────────────────────────────

    private static DateTime InvokeRead(string? json)
    {
        var converter = new Iso8601DateTimeConverter();
        var fullJson = json is null ? "null" : $"\"{json}\"";
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(fullJson));
        reader.Read(); // advance to value token
        if (reader.TokenType == JsonTokenType.Null)
            return default;
        return converter.Read(ref reader, typeof(DateTime), new JsonSerializerOptions());
    }

    [Fact]
    public void Read_ReturnsDefault_ForEmptyString()
    {
        var result = InvokeRead("");
        result.Should().Be(default(DateTime));
    }

    [Fact]
    public void Read_ParsesUtcDateTimeString_Correctly()
    {
        var result = InvokeRead("2024-01-15T10:30:00Z");
        result.Year.Should().Be(2024);
        result.Month.Should().Be(1);
        result.Day.Should().Be(15);
        result.Hour.Should().Be(10);
        result.Minute.Should().Be(30);
    }

    [Fact]
    public void Read_ConvertsLocalOffset_ToUtc()
    {
        // +05:00 offset → UTC should be 5 hours earlier
        var result = InvokeRead("2024-01-15T15:30:00+05:00");
        result.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_ReturnsUtcKind()
    {
        var result = InvokeRead("2024-06-01T12:00:00Z");
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── Iso8601DateTimeConverter.Write ────────────────────────────────────

    private static string InvokeWrite(DateTime value)
    {
        var converter = new Iso8601DateTimeConverter();
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        converter.Write(writer, value, new JsonSerializerOptions());
        writer.Flush();
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        return json.Trim('"');
    }

    [Fact]
    public void Write_FormatsUtcDateTime_WithZSuffix()
    {
        var dt = new DateTime(2024, 3, 20, 14, 30, 0, DateTimeKind.Utc);
        var result = InvokeWrite(dt);
        result.Should().Be("2024-03-20T14:30:00.000Z");
    }

    [Fact]
    public void Write_ConvertsLocalDateTime_ToUtcBeforeFormatting()
    {
        var utcEquivalent = new DateTime(2024, 3, 20, 14, 30, 0, DateTimeKind.Utc);
        var localDt = utcEquivalent.ToLocalTime(); // Convert from UTC to local
        var result = InvokeWrite(localDt);
        // The written value should equal the UTC time formatted with Z suffix
        result.Should().EndWith("Z");
        DateTime.Parse(result).ToUniversalTime().Should().BeCloseTo(utcEquivalent, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Write_ProducesIso8601FormatWithMilliseconds()
    {
        var dt = new DateTime(2024, 12, 31, 23, 59, 59, 123, DateTimeKind.Utc);
        var result = InvokeWrite(dt);
        result.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
    }

    // ── PasswordHasherService ─────────────────────────────────────────────

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hasher = new PasswordHasherService();
        var hash = hasher.Hash("MySecurePass@1");
        hasher.Verify("MySecurePass@1", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new PasswordHasherService();
        var hash = hasher.Hash("MySecurePass@1");
        hasher.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyStringAgainstValidHash()
    {
        var hasher = new PasswordHasherService();
        var hash = hasher.Hash("MySecurePass@1");
        hasher.Verify("", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForCaseMismatch()
    {
        var hasher = new PasswordHasherService();
        var hash = hasher.Hash("MySecurePass@1");
        hasher.Verify("mysecurepass@1", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ProducesDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasherService();
        var hash1 = hasher.Hash("SamePassword@1");
        var hash2 = hasher.Hash("SamePassword@1");
        hash1.Should().NotBe(hash2); // different salts
    }

    // ── LimitCheckResult ──────────────────────────────────────────────────

    [Fact]
    public void Allow_ReturnsAllowedResult()
    {
        var result = LimitCheckResult.Allow();
        result.IsAllowed.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Deny_ReturnsDeniedResult_WithCodeAndMessage()
    {
        var result = LimitCheckResult.Deny("ACCOUNT_LIMIT_EXCEEDED", "Too many accounts.");
        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_LIMIT_EXCEEDED");
        result.ErrorMessage.Should().Be("Too many accounts.");
    }

    [Fact]
    public void Deny_WithDifferentCodes_ProducesCorrectResult()
    {
        var result = LimitCheckResult.Deny("KYC_REQUIRED", "Identity verification needed.");
        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("KYC_REQUIRED");
        result.ErrorMessage.Should().Contain("verification");
    }

    [Fact]
    public void Allow_IsIndependentInstance_PerCall()
    {
        var r1 = LimitCheckResult.Allow();
        var r2 = LimitCheckResult.Allow();
        r1.Should().NotBeSameAs(r2);
        r1.IsAllowed.Should().Be(r2.IsAllowed);
    }
}
