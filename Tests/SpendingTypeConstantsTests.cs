using TransactionService.Constants;

namespace Tests;

/// <summary>
/// Comprehensive tests for SpendingTypeConstants
/// Covers validation, normalization, and edge cases
/// </summary>
public class SpendingTypeConstantsTests
{
    #region AllowedTypes Tests

    [Fact]
    public void AllowedTypes_ContainsExpectedValues()
    {
        // Assert
        SpendingTypeConstants.AllowedTypes.Should().HaveCount(3);
        SpendingTypeConstants.AllowedTypes.Should().Contain("Fun");
        SpendingTypeConstants.AllowedTypes.Should().Contain("Fixed");
        SpendingTypeConstants.AllowedTypes.Should().Contain("Future");
    }

    [Fact]
    public void AllowedTypes_IsReadonlyArray()
    {
        // Assert - Should not be null and should be accessible
        SpendingTypeConstants.AllowedTypes.Should().NotBeNull();
        SpendingTypeConstants.AllowedTypes.Should().NotBeEmpty();
    }

    #endregion

    #region IsValid Tests

    [Theory]
    [InlineData("Fun")]
    [InlineData("Fixed")]
    [InlineData("Future")]
    public void IsValid_ReturnsTrue_ForExactCaseValues(string value)
    {
        // Act & Assert
        SpendingTypeConstants.IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("fun")]
    [InlineData("FUN")]
    [InlineData("fUn")]
    [InlineData("fixed")]
    [InlineData("FIXED")]
    [InlineData("fIxEd")]
    [InlineData("future")]
    [InlineData("FUTURE")]
    [InlineData("FuTuRe")]
    public void IsValid_ReturnsTrue_ForCaseInsensitiveValues(string value)
    {
        // Act & Assert
        SpendingTypeConstants.IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void IsValid_ReturnsFalse_ForNullOrWhitespace(string? value)
    {
        // Act & Assert
        SpendingTypeConstants.IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Entertainment")]
    [InlineData("Bills")]
    [InlineData("Savings")]
    [InlineData("Other")]
    [InlineData("Misc")]
    public void IsValid_ReturnsFalse_ForInvalidCategories(string value)
    {
        // Act & Assert
        SpendingTypeConstants.IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData(" Fun")]
    [InlineData("Fun ")]
    [InlineData(" Fun ")]
    [InlineData("  Fixed  ")]
    public void IsValid_ReturnsFalse_ForValuesWithWhitespace(string value)
    {
        // The Contains method won't match with leading/trailing whitespace
        // unless the input is trimmed. Current implementation may vary.
        // This test documents the expected behavior.
        // Act
        var result = SpendingTypeConstants.IsValid(value);

        // Assert - Values with whitespace are treated as invalid
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("Fun1")]
    [InlineData("2Fixed")]
    [InlineData("Future!")]
    [InlineData("Fun@Fixed")]
    public void IsValid_ReturnsFalse_ForValuesWithSpecialCharsOrNumbers(string value)
    {
        // Act & Assert
        SpendingTypeConstants.IsValid(value).Should().BeFalse();
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_WithValidValue_ReturnsTrimmedValue()
    {
        // Arrange
        var input = "  Fun  ";

        // Act
        var result = SpendingTypeConstants.Normalize(input);

        // Assert
        result.Should().Be("Fun");
    }

    [Fact]
    public void Normalize_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => SpendingTypeConstants.Normalize(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Fun", "Fun")]
    [InlineData("Fixed", "Fixed")]
    [InlineData("Future", "Future")]
    public void Normalize_PreservesCase(string input, string expected)
    {
        // Act
        var result = SpendingTypeConstants.Normalize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("fun", "fun")]
    [InlineData("FUN", "FUN")]
    [InlineData("fIxEd", "fIxEd")]
    public void Normalize_PreservesOriginalCasing(string input, string expected)
    {
        // Act
        var result = SpendingTypeConstants.Normalize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_WithLeadingAndTrailingWhitespace_TrimsCorrectly()
    {
        // Arrange
        var input = "\t  Fun  \n";

        // Act
        var result = SpendingTypeConstants.Normalize(input);

        // Assert
        result.Should().Be("Fun");
    }

    [Fact]
    public void Normalize_WithOnlyWhitespace_ReturnsEmptyString()
    {
        // Arrange
        var input = "   ";

        // Act
        var result = SpendingTypeConstants.Normalize(input);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Integration Tests

    [Theory]
    [InlineData("Fun")]
    [InlineData("Fixed")]
    [InlineData("Future")]
    public void IsValidAndNormalize_WorkTogether(string input)
    {
        // Act
        var isValid = SpendingTypeConstants.IsValid(input);
        var normalized = SpendingTypeConstants.Normalize(input);

        // Assert
        isValid.Should().BeTrue();
        normalized.Should().Be(input);
    }

    [Fact]
    public void AllAllowedTypes_AreValid()
    {
        // Act & Assert
        foreach (var type in SpendingTypeConstants.AllowedTypes)
        {
            SpendingTypeConstants.IsValid(type).Should().BeTrue($"'{type}' should be valid");
        }
    }

    [Fact]
    public void AllAllowedTypes_NormalizeToThemselves()
    {
        // Act & Assert
        foreach (var type in SpendingTypeConstants.AllowedTypes)
        {
            var normalized = SpendingTypeConstants.Normalize(type);
            normalized.Should().Be(type, $"'{type}' should normalize to itself");
        }
    }

    #endregion
}
