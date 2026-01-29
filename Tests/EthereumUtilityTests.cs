using AccountService.Utilities;

namespace Tests;

/// <summary>
/// Comprehensive tests for EthereumUtility class
/// Covers address validation, checksumming, and edge cases
/// </summary>
public class EthereumUtilityTests
{
    #region ValidateAndChecksumAddress Tests

    [Theory]
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C")]
    [InlineData("0x742D35CC6634C0532925A3B844BC9E7595F8FE0C")]
    [InlineData("0x742d35cc6634c0532925a3b844bc9e7595f8fe0c")]
    public void ValidateAndChecksumAddress_WithValidAddress_ReturnsNormalizedAddress(string address)
    {
        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().StartWith("0x");
        result.Length.Should().Be(42);
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithLeadingWhitespace_TrimsAndReturns()
    {
        // Arrange
        var address = "  0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C  ";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ValidateAndChecksumAddress_WithNullOrWhitespace_ThrowsArgumentException(string? address)
    {
        // Act & Assert
        var act = () => EthereumUtility.ValidateAndChecksumAddress(address!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid Ethereum address format*");
    }

    [Theory]
    [InlineData("742d35Cc6634C0532925a3b844Bc9e7595f8fE0C")]   // Missing 0x prefix
    [InlineData("1x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C")] // Wrong prefix
    [InlineData("Ox742d35Cc6634C0532925a3b844Bc9e7595f8fE0C")] // Capital O instead of 0
    public void ValidateAndChecksumAddress_WithoutProperPrefix_ThrowsArgumentException(string address)
    {
        // Act & Assert
        var act = () => EthereumUtility.ValidateAndChecksumAddress(address);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid Ethereum address format*");
    }

    [Theory]
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE")]   // 41 chars - too short
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C1")] // 43 chars - too long
    [InlineData("0x742d")]                                       // Way too short
    [InlineData("0x")]                                           // Only prefix
    public void ValidateAndChecksumAddress_WithWrongLength_ThrowsArgumentException(string address)
    {
        // Act & Assert
        var act = () => EthereumUtility.ValidateAndChecksumAddress(address);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Address must be 42 characters*");
    }

    [Theory]
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595f8fGHI")] // Contains G, H, I
    [InlineData("0xZZZd35Cc6634C0532925a3b844Bc9e7595f8fe0c")] // Contains Z
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595f8f!@#")] // Contains special chars
    [InlineData("0x742d35Cc6634C0532925a3b844Bc9e7595 f8fe0")] // Contains space
    public void ValidateAndChecksumAddress_WithNonHexCharacters_ThrowsArgumentException(string address)
    {
        // Act & Assert
        var act = () => EthereumUtility.ValidateAndChecksumAddress(address);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Address must be hexadecimal*");
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithValidMixedCaseAddress_PreservesCasing()
    {
        // Arrange
        var address = "0xAbCdEf1234567890AbCdEf1234567890AbCdEf12";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be(address);
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithAllLowercase_ReturnsLowercase()
    {
        // Arrange
        var address = "0xabcdef1234567890abcdef1234567890abcdef12";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be(address);
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithAllUppercase_ReturnsUppercase()
    {
        // Arrange
        var address = "0xABCDEF1234567890ABCDEF1234567890ABCDEF12";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be(address);
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithAllZeros_ReturnsAddress()
    {
        // Arrange
        var address = "0x0000000000000000000000000000000000000000";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be(address);
    }

    [Fact]
    public void ValidateAndChecksumAddress_WithMaxValueAddress_ReturnsAddress()
    {
        // Arrange - Max possible hex address
        var address = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

        // Act
        var result = EthereumUtility.ValidateAndChecksumAddress(address);

        // Assert
        result.Should().Be(address);
    }

    #endregion
}
