namespace ClassifiedAds.Modules.TestGeneration.Algorithms.Models;

/// <summary>
/// Result of semantic token matching between two identifier tokens.
/// Source: SPDG paper (arXiv:2411.07098) - semantic property matching.
/// </summary>
public class TokenMatchResult
{
    /// <summary>
    /// The original source token (from parameter/consumer side).
    /// </summary>
    public string SourceToken { get; set; }

    /// <summary>
    /// The matched target token (from resource/producer side).
    /// </summary>
    public string MatchedToken { get; set; }

    /// <summary>
    /// Similarity score [0.0 - 1.0]. 1.0 = exact match.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// How the match was determined.
    /// </summary>
    public TokenMatchType MatchType { get; set; }
}

/// <summary>
/// Classification of semantic token match types.
/// Ordered by decreasing confidence.
/// </summary>
public enum TokenMatchType
{
    /// <summary>
    /// Case-insensitive exact match. Score = 1.0.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Singular/plural variant match. "user" ↔ "users". Score = 0.95.
    /// </summary>
    PluralSingular = 1,

    /// <summary>
    /// Common abbreviation match. "cat" ↔ "category". Score = 0.85.
    /// </summary>
    Abbreviation = 2,

    /// <summary>
    /// Stem/lemma match after suffix stripping. "creating" ↔ "create". Score = 0.80.
    /// </summary>
    StemMatch = 3,

    /// <summary>
    /// One token is a substring of the other. "user" in "userId". Score = 0.70.
    /// </summary>
    Substring = 4,
}
