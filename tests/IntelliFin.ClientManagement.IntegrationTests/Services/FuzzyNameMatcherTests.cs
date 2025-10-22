using FluentAssertions;
using IntelliFin.ClientManagement.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Tests for FuzzyNameMatcher service
/// Validates Levenshtein distance and Soundex algorithms
/// </summary>
public class FuzzyNameMatcherTests
{
    private readonly FuzzyNameMatcher _matcher;

    public FuzzyNameMatcherTests()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _matcher = new FuzzyNameMatcher(loggerFactory.CreateLogger<FuzzyNameMatcher>());
    }

    #region Exact Match Tests

    [Fact]
    public void CalculateMatch_ExactMatch_Returns100Confidence()
    {
        // Arrange
        var clientName = "VLADIMIR PUTIN";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
        result.IsMatch.Should().BeTrue();
        result.LevenshteinDistance.Should().Be(0);
        result.SoundexMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_ExactMatchCaseInsensitive_Returns100Confidence()
    {
        // Arrange
        var clientName = "vladimir putin";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
    }

    [Fact]
    public void CalculateMatch_ExactAliasMatch_Returns98Confidence()
    {
        // Arrange
        var clientName = "V. Putin";
        var targetName = "VLADIMIR PUTIN";
        var aliases = new[] { "V. Putin", "Vladimir Vladimirovich Putin" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().Be(98);
        result.MatchType.Should().Be("ExactAlias");
        result.MatchedName.Should().Be("V. Putin");
        result.IsMatch.Should().BeTrue();
    }

    #endregion

    #region Levenshtein Distance Tests

    [Fact]
    public void CalculateMatch_OneCharacterDifference_ReturnsHighConfidence()
    {
        // Arrange
        var clientName = "VLADIMIR PUTIN";
        var targetName = "VLADMIR PUTIN"; // Missing 'i'

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThan(90);
        result.IsMatch.Should().BeTrue();
        result.LevenshteinDistance.Should().Be(1);
    }

    [Fact]
    public void CalculateMatch_SimilarNames_ReturnsMediumConfidence()
    {
        // Arrange
        var clientName = "JOHN SMITH";
        var targetName = "JOHN SMYTH"; // Different spelling

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThan(80);
        result.MatchType.Should().Contain("Similarity");
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_CompletelyDifferentNames_ReturnsLowConfidence()
    {
        // Arrange
        var clientName = "JOHN BANDA";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeLessThan(30);
        result.IsMatch.Should().BeFalse();
    }

    #endregion

    #region Soundex Tests

    [Fact]
    public void CalculateMatch_PhoneticMatch_ReturnsSoundexMatch()
    {
        // Arrange (These sound similar)
        var clientName = "SMITH";
        var targetName = "SMYTH";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.SoundexMatch.Should().BeTrue();
        result.MatchType.Should().Contain("Phonetic");
        result.Confidence.Should().BeGreaterThan(60);
    }

    [Fact]
    public void CalculateMatch_PhoneticallySimilarNames_ReturnsSoundexMatch()
    {
        // Arrange
        var clientName = "CATHERINE";
        var targetName = "KATHRYN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.SoundexMatch.Should().BeTrue();
    }

    #endregion

    #region Alias Matching Tests

    [Fact]
    public void CalculateMatch_MatchesAlias_ReturnsHighConfidence()
    {
        // Arrange
        var clientName = "KIM JONG-UN";
        var targetName = "KIM JONG UN";
        var aliases = new[] { "Kim Jong-un", "Kim Jong Un", "KIM JONG-UN" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().Be(98); // Exact alias match
        result.MatchedName.Should().Be("KIM JONG-UN");
    }

    [Fact]
    public void CalculateMatch_BestAliasSelected_ReturnsHighestConfidence()
    {
        // Arrange
        var clientName = "NICOLAS MADURO";
        var targetName = "NICOLAS MADURO MOROS";
        var aliases = new[] { "Nicolás Maduro", "Nicolas Maduro", "MADURO MOROS" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().Be(98); // Should match "Nicolas Maduro" alias exactly
        result.MatchedName.Should().Be("Nicolas Maduro");
    }

    #endregion

    #region Zambian Name Patterns

    [Fact]
    public void CalculateMatch_ZambianName_MultipleWords_HandledCorrectly()
    {
        // Arrange
        var clientName = "HAKAINDE HICHILEMA";
        var targetName = "HAKAINDE SAMMY HICHILEMA";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThan(70);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_NameWithInitials_MatchesFullName()
    {
        // Arrange
        var clientName = "H.H. HICHILEMA";
        var targetName = "HAKAINDE HICHILEMA";
        var aliases = new[] { "HH", "H. Hichilema" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().BeGreaterThan(50);
        // May not be exact match due to initials vs full name
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CalculateMatch_EmptyClientName_ReturnsZeroConfidence()
    {
        // Arrange
        var clientName = "";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(0);
        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void CalculateMatch_NullAliases_HandlesGracefully()
    {
        // Arrange
        var clientName = "VLADIMIR PUTIN";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, null);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
    }

    [Fact]
    public void CalculateMatch_SpecialCharacters_HandledCorrectly()
    {
        // Arrange
        var clientName = "O'BRIEN";
        var targetName = "OBRIEN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThan(85);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_ExtraSpaces_NormalizedCorrectly()
    {
        // Arrange
        var clientName = "VLADIMIR    PUTIN"; // Multiple spaces
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
    }

    [Fact]
    public void CalculateMatch_LeadingTrailingSpaces_NormalizedCorrectly()
    {
        // Arrange
        var clientName = "  VLADIMIR PUTIN  ";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
    }

    #endregion

    #region Real-World Sanctions Test Cases

    [Fact]
    public void CalculateMatch_SanctionedPerson_TestName_MatchesCorrectly()
    {
        // Arrange
        var clientName = "SANCTIONED PERSON";
        var targetName = "SANCTIONED PERSON";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_TypoInSanctionedName_StillMatches()
    {
        // Arrange
        var clientName = "VLADMIR PUTIN"; // Common typo (missing 'i')
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThan(90);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public void CalculateMatch_PartialNameMatch_LowerConfidence()
    {
        // Arrange
        var clientName = "PUTIN";
        var targetName = "VLADIMIR PUTIN";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        // Should have lower confidence due to missing first name
        result.Confidence.Should().BeLessThan(70);
    }

    #endregion

    #region Real-World PEP Test Cases

    [Fact]
    public void CalculateMatch_PoliticalFigure_TestName_MatchesCorrectly()
    {
        // Arrange
        var clientName = "POLITICAL FIGURE";
        var targetName = "POLITICAL FIGURE";

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().Be(100);
        result.MatchType.Should().Be("Exact");
    }

    [Fact]
    public void CalculateMatch_GovernmentOfficial_TestName_MatchesCorrectly()
    {
        // Arrange
        var clientName = "GOVERNMENT OFFICIAL";
        var targetName = "GOVERNMENT OFFICIAL";
        var aliases = new[] { "Govt Official", "Test Official" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().Be(100);
    }

    [Fact]
    public void CalculateMatch_ZambianPresident_VariousSpellings_MatchesCorrectly()
    {
        // Arrange
        var clientName = "HAKAINDE HICHILEMA";
        var targetName = "HAKAINDE HICHILEMA";
        var aliases = new[] { "HH", "Hakainde S. Hichilema" };

        // Act
        var result = _matcher.CalculateMatch(clientName, targetName, aliases);

        // Assert
        result.Confidence.Should().Be(100);
    }

    #endregion

    #region Confidence Threshold Validation

    [Theory]
    [InlineData("VLADIMIR PUTIN", "VLADIMIR PUTIN", 100)] // Exact
    [InlineData("VLADIMIR PUTIN", "VLADMIR PUTIN", 90)] // 1 char diff, min 90
    [InlineData("JOHN SMITH", "JOHN SMYTH", 80)] // Similar, min 80
    [InlineData("JOHN BANDA", "JOHN DOE", 40)] // Different last names, ~40
    public void CalculateMatch_VariousNames_ReturnsExpectedConfidenceRange(
        string clientName, string targetName, int minExpectedConfidence)
    {
        // Act
        var result = _matcher.CalculateMatch(clientName, targetName);

        // Assert
        result.Confidence.Should().BeGreaterThanOrEqualTo(minExpectedConfidence);
    }

    #endregion
}
