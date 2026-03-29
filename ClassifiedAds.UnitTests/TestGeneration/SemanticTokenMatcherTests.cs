using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for SemanticTokenMatcher.
/// Covers: exact match, plural/singular, abbreviations, stem matching, substring containment.
/// Source: SPDG paper (arXiv:2411.07098) Section 3.2 - Semantic Property Dependency Graph.
/// </summary>
public class SemanticTokenMatcherTests
{
    private readonly SemanticTokenMatcher _sut;

    public SemanticTokenMatcherTests()
    {
        _sut = new SemanticTokenMatcher();
    }

    #region Match - Null/Empty Tests

    [Fact]
    public void Match_Should_ReturnNull_WhenSourceIsNull()
    {
        // Act
        var result = _sut.Match(null, "target");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Match_Should_ReturnNull_WhenTargetIsNull()
    {
        // Act
        var result = _sut.Match("source", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Match_Should_ReturnNull_WhenBothAreEmpty()
    {
        // Act
        var result = _sut.Match("", "");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Match_Should_ReturnNull_WhenNoMatch()
    {
        // Act
        var result = _sut.Match("apple", "banana");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Match - Exact Match (Score 1.0)

    [Fact]
    public void Match_Should_ReturnExactMatch_WhenTokensAreIdentical()
    {
        // Act
        var result = _sut.Match("user", "user");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(1.0);
        result.MatchType.Should().Be(TokenMatchType.Exact);
    }

    [Fact]
    public void Match_Should_ReturnExactMatch_CaseInsensitive()
    {
        // Act
        var result = _sut.Match("User", "USER");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(1.0);
        result.MatchType.Should().Be(TokenMatchType.Exact);
    }

    [Fact]
    public void Match_Should_PreserveOriginalTokens()
    {
        // Act
        var result = _sut.Match("UserId", "USERID");

        // Assert
        result.SourceToken.Should().Be("UserId");
        result.MatchedToken.Should().Be("USERID");
    }

    #endregion

    #region Match - Plural/Singular (Score 0.95)

    [Fact]
    public void Match_Should_ReturnPluralSingular_WhenSingularMatchesPlural()
    {
        // Act
        var result = _sut.Match("user", "users");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.95);
        result.MatchType.Should().Be(TokenMatchType.PluralSingular);
    }

    [Fact]
    public void Match_Should_ReturnPluralSingular_WhenPluralMatchesSingular()
    {
        // Act
        var result = _sut.Match("categories", "category");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.95);
        result.MatchType.Should().Be(TokenMatchType.PluralSingular);
    }

    [Fact]
    public void Match_Should_HandleIrregularPlurals()
    {
        // Act
        var result = _sut.Match("people", "person");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.95);
        result.MatchType.Should().Be(TokenMatchType.PluralSingular);
    }

    [Fact]
    public void Match_Should_Handle_IesPlural()
    {
        // Act
        var result = _sut.Match("category", "categories");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.95);
    }

    [Fact]
    public void Match_Should_Handle_EsPlural()
    {
        // Act
        var result = _sut.Match("address", "addresses");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.95);
    }

    #endregion

    #region Match - Abbreviation (Score 0.85)

    [Fact]
    public void Match_Should_ReturnAbbreviation_WhenAbbrMatchesFullForm()
    {
        // Act
        var result = _sut.Match("cat", "category");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.85);
        result.MatchType.Should().Be(TokenMatchType.Abbreviation);
    }

    [Fact]
    public void Match_Should_ReturnAbbreviation_WhenFullFormMatchesAbbr()
    {
        // Act
        var result = _sut.Match("organization", "org");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.85);
        result.MatchType.Should().Be(TokenMatchType.Abbreviation);
    }

    [Theory]
    [InlineData("repo", "repository")]
    [InlineData("auth", "authentication")]
    [InlineData("config", "configuration")]
    [InlineData("env", "environment")]
    [InlineData("msg", "message")]
    [InlineData("doc", "document")]
    [InlineData("usr", "user")]
    [InlineData("acct", "account")]
    public void Match_Should_RecognizeCommonAbbreviations(string abbr, string fullForm)
    {
        // Act
        var result = _sut.Match(abbr, fullForm);

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.85);
        result.MatchType.Should().Be(TokenMatchType.Abbreviation);
    }

    [Fact]
    public void Match_Should_MatchAbbreviationBidirectionally()
    {
        // Act
        var result1 = _sut.Match("tx", "transaction");
        var result2 = _sut.Match("transaction", "tx");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Score.Should().Be(0.85);
        result2.Score.Should().Be(0.85);
    }

    #endregion

    #region Match - Stem Match (Score 0.80)

    [Fact]
    public void Match_Should_ReturnStemMatch_WhenStemsAreEqual()
    {
        // "creating" → "creat", "creation" → "creat"
        // Act
        var result = _sut.Match("creating", "creation");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.80);
        result.MatchType.Should().Be(TokenMatchType.StemMatch);
    }

    [Fact]
    public void Match_Should_ReturnStemMatch_ForTionSuffix()
    {
        // "validation" stem = "valida" (strip "tion")
        // "valida" stem = "valida" (no suffix to strip, too short)
        // These match as stems.
        // Act
        var result = _sut.Match("valida", "validation");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.80);
        result.MatchType.Should().Be(TokenMatchType.StemMatch);
    }

    #endregion

    #region Match - Substring (Score 0.70)

    [Fact]
    public void Match_Should_ReturnSubstring_WhenOneContainsOther()
    {
        // Act
        var result = _sut.Match("user", "userId");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.70);
        result.MatchType.Should().Be(TokenMatchType.Substring);
    }

    [Fact]
    public void Match_Should_ReturnSubstring_Bidirectionally()
    {
        // Act
        var result = _sut.Match("categoryName", "category");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(0.70);
        result.MatchType.Should().Be(TokenMatchType.Substring);
    }

    [Fact]
    public void Match_Should_RequireMinimum3CharsForSubstring()
    {
        // "ab" is too short for substring match
        // Act
        var result = _sut.Match("ab", "abc");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindMatches Tests

    [Fact]
    public void FindMatches_Should_ReturnEmpty_WhenSourceTokensIsNull()
    {
        // Act
        var result = _sut.FindMatches(null, new[] { "target" });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindMatches_Should_ReturnEmpty_WhenTargetTokensIsNull()
    {
        // Act
        var result = _sut.FindMatches(new[] { "source" }, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindMatches_Should_ReturnEmpty_WhenBothAreEmpty()
    {
        // Act
        var result = _sut.FindMatches(Array.Empty<string>(), Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindMatches_Should_FindAllMatchingPairs()
    {
        // Arrange: Use tokens that match with the matcher's logic.
        // Note: ApiTestOrderAlgorithm strips "Id" suffix before calling FindMatches,
        // so here we test with already-stripped tokens (user, category).
        var sourceTokens = new[] { "user", "category", "random" };
        var targetTokens = new[] { "users", "categories", "other" };

        // Act
        var result = _sut.FindMatches(sourceTokens, targetTokens, minScore: 0.65);

        // Assert: user↔users (plural/singular 0.95), category↔categories (plural/singular 0.95)
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.SourceToken == "user" && r.MatchedToken == "users");
        result.Should().Contain(r => r.SourceToken == "category" && r.MatchedToken == "categories");
    }

    [Fact]
    public void FindMatches_Should_FilterByMinScore()
    {
        // Arrange
        var sourceTokens = new[] { "user", "abc" };
        var targetTokens = new[] { "user", "abcdef" }; // abc→abcdef is substring (0.70)

        // Act
        var highThreshold = _sut.FindMatches(sourceTokens, targetTokens, minScore: 0.90);
        var lowThreshold = _sut.FindMatches(sourceTokens, targetTokens, minScore: 0.65);

        // Assert
        highThreshold.Should().HaveCount(1); // Only exact match
        lowThreshold.Should().HaveCount(2); // Exact + substring
    }

    [Fact]
    public void FindMatches_Should_OrderByScoreDescending()
    {
        // Arrange
        var sourceTokens = new[] { "user", "usr", "userInfo" };
        var targetTokens = new[] { "user" };

        // Act
        var result = _sut.FindMatches(sourceTokens, targetTokens, minScore: 0.65);

        // Assert
        result.Should().BeInDescendingOrder(r => r.Score);
        result.First().Score.Should().Be(1.0); // Exact match first
    }

    #endregion

    #region NormalizeToken Tests

    [Fact]
    public void NormalizeToken_Should_ReturnNull_WhenInputIsNull()
    {
        // Act
        var result = _sut.NormalizeToken(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeToken_Should_ReturnNull_WhenInputIsWhitespace()
    {
        // Act
        var result = _sut.NormalizeToken("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NormalizeToken_Should_TrimAndLowercase()
    {
        // Act
        var result = _sut.NormalizeToken("  UserId  ");

        // Assert
        result.Should().Be("userid");
    }

    #endregion

    #region Singularize Tests

    [Fact]
    public void Singularize_Should_ReturnOriginal_WhenTooShort()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("ab");

        // Assert
        result.Should().Be("ab");
    }

    [Fact]
    public void Singularize_Should_Handle_RegularSPlural()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("users");

        // Assert
        result.Should().Be("user");
    }

    [Fact]
    public void Singularize_Should_Handle_IesPlural()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("categories");

        // Assert
        result.Should().Be("category");
    }

    [Fact]
    public void Singularize_Should_Handle_EsPlural()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("boxes");

        // Assert
        result.Should().Be("box");
    }

    [Fact]
    public void Singularize_Should_NotRemove_DoubleSS()
    {
        // "class" should not become "clas"
        // Act
        var result = SemanticTokenMatcher.Singularize("class");

        // Assert
        result.Should().Be("class");
    }

    [Fact]
    public void Singularize_Should_Handle_IrregularPlural_People()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("people");

        // Assert
        result.Should().Be("person");
    }

    [Fact]
    public void Singularize_Should_Handle_IrregularPlural_Children()
    {
        // Act
        var result = SemanticTokenMatcher.Singularize("children");

        // Assert
        result.Should().Be("child");
    }

    [Fact]
    public void Singularize_Should_NotChangeAlreadySingular()
    {
        // "status" ends with "us", should not be changed
        // Act
        var result = SemanticTokenMatcher.Singularize("status");

        // Assert
        result.Should().Be("status");
    }

    #endregion

    #region Priority/Ordering Tests

    [Fact]
    public void Match_Should_PreferExactOverPluralSingular()
    {
        // "user" vs "user" should be Exact (1.0), not PluralSingular (0.95)
        var result = _sut.Match("user", "user");

        result.MatchType.Should().Be(TokenMatchType.Exact);
        result.Score.Should().Be(1.0);
    }

    [Fact]
    public void Match_Should_PreferPluralSingularOverAbbreviation()
    {
        // "users" vs "user" should be PluralSingular (0.95), not something else
        var result = _sut.Match("users", "user");

        result.MatchType.Should().Be(TokenMatchType.PluralSingular);
        result.Score.Should().Be(0.95);
    }

    [Fact]
    public void Match_Should_PreferAbbreviationOverSubstring()
    {
        // "cat" vs "category" should be Abbreviation (0.85), not Substring (0.70)
        var result = _sut.Match("cat", "category");

        result.MatchType.Should().Be(TokenMatchType.Abbreviation);
        result.Score.Should().Be(0.85);
    }

    #endregion
}
