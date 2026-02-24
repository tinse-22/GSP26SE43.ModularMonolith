using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Enhanced semantic token matching for API dependency detection.
/// Source: SPDG paper (arXiv:2411.07098) Section 3.2 - Semantic Property Dependency Graph.
///
/// Improves token matching beyond simple string equality:
/// - Plural/singular normalization (users ↔ user)
/// - Common API abbreviations (cat ↔ category, org ↔ organization)
/// - Stem matching (creating ↔ create)
/// - Substring containment (userId contains user)
/// </summary>
public interface ISemanticTokenMatcher
{
    /// <summary>
    /// Find all matching pairs between source tokens and target tokens.
    /// Returns matches ordered by decreasing confidence.
    /// </summary>
    /// <param name="sourceTokens">Tokens from consumer side (e.g., parameter names).</param>
    /// <param name="targetTokens">Tokens from producer side (e.g., resource path segments).</param>
    /// <param name="minScore">Minimum similarity score to include (default: 0.65).</param>
    IReadOnlyCollection<TokenMatchResult> FindMatches(
        IReadOnlyCollection<string> sourceTokens,
        IReadOnlyCollection<string> targetTokens,
        double minScore = 0.65);

    /// <summary>
    /// Check if two individual tokens are semantically related.
    /// </summary>
    TokenMatchResult Match(string sourceToken, string targetToken);

    /// <summary>
    /// Normalize a token for comparison (lowercase, trim, singularize).
    /// </summary>
    string NormalizeToken(string token);
}
