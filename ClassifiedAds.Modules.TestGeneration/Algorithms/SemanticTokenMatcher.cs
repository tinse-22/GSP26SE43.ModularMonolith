using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Enhanced semantic token matching for API dependency detection.
/// Source: SPDG paper (arXiv:2411.07098) Section 3.2 - Semantic Property Dependency Graph.
///
/// Matching pipeline (in priority order):
/// 1. Exact match (case-insensitive) → score 1.0
/// 2. Plural/singular normalization → score 0.95
/// 3. Known abbreviation expansion → score 0.85
/// 4. Stem match (suffix stripping) → score 0.80
/// 5. Substring containment (min 3 chars) → score 0.70
/// </summary>
public class SemanticTokenMatcher : ISemanticTokenMatcher
{
    /// <summary>
    /// Common API abbreviations: abbreviation → full form.
    /// Sourced from SPDG paper analysis of GitLab/Stripe/GitHub APIs.
    /// </summary>
    private static readonly Dictionary<string, string[]> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cat"] = new[] { "category", "categories" },
        ["org"] = new[] { "organization", "organisations", "organizations" },
        ["repo"] = new[] { "repository", "repositories" },
        ["auth"] = new[] { "authentication", "authorization", "authenticate", "authorize" },
        ["admin"] = new[] { "administrator", "administrators" },
        ["config"] = new[] { "configuration", "configurations" },
        ["env"] = new[] { "environment", "environments" },
        ["msg"] = new[] { "message", "messages" },
        ["desc"] = new[] { "description", "descriptions" },
        ["img"] = new[] { "image", "images" },
        ["doc"] = new[] { "document", "documents", "documentation" },
        ["info"] = new[] { "information" },
        ["addr"] = new[] { "address", "addresses" },
        ["req"] = new[] { "request", "requests", "requirement", "requirements" },
        ["resp"] = new[] { "response", "responses" },
        ["param"] = new[] { "parameter", "parameters" },
        ["spec"] = new[] { "specification", "specifications" },
        ["perm"] = new[] { "permission", "permissions" },
        ["notif"] = new[] { "notification", "notifications" },
        ["grp"] = new[] { "group", "groups" },
        ["proj"] = new[] { "project", "projects" },
        ["ref"] = new[] { "reference", "references" },
        ["stat"] = new[] { "status", "statistic", "statistics" },
        ["idx"] = new[] { "index", "indexes", "indices" },
        ["usr"] = new[] { "user", "users" },
        ["acct"] = new[] { "account", "accounts" },
        ["dept"] = new[] { "department", "departments" },
        ["sess"] = new[] { "session", "sessions" },
        ["tx"] = new[] { "transaction", "transactions" },
        ["svc"] = new[] { "service", "services" },
        // Additional common API abbreviations
        ["id"] = new[] { "identifier", "identifiers", "identity", "identities" },
        ["num"] = new[] { "number", "numbers" },
        ["qty"] = new[] { "quantity", "quantities" },
        ["amt"] = new[] { "amount", "amounts" },
        ["pwd"] = new[] { "password", "passwords" },
        ["cred"] = new[] { "credential", "credentials" },
        ["creds"] = new[] { "credentials" },
        ["loc"] = new[] { "location", "locations" },
        ["pos"] = new[] { "position", "positions" },
        ["src"] = new[] { "source", "sources" },
        ["dst"] = new[] { "destination", "destinations" },
        ["dest"] = new[] { "destination", "destinations" },
        ["tmp"] = new[] { "temporary", "temp" },
        ["temp"] = new[] { "temporary", "temperature" },
        ["prev"] = new[] { "previous" },
        ["curr"] = new[] { "current" },
        ["max"] = new[] { "maximum" },
        ["min"] = new[] { "minimum" },
        ["avg"] = new[] { "average" },
        ["cnt"] = new[] { "count", "counter" },
        ["len"] = new[] { "length" },
        ["sz"] = new[] { "size" },
        ["buf"] = new[] { "buffer", "buffers" },
        ["err"] = new[] { "error", "errors" },
        ["warn"] = new[] { "warning", "warnings" },
        ["val"] = new[] { "value", "values", "validation" },
        ["ver"] = new[] { "version", "versions" },
        ["ts"] = new[] { "timestamp", "timestamps" },
        ["dt"] = new[] { "datetime", "date" },
        ["str"] = new[] { "string", "strings" },
        ["obj"] = new[] { "object", "objects" },
        ["arr"] = new[] { "array", "arrays" },
        ["attr"] = new[] { "attribute", "attributes" },
        ["prop"] = new[] { "property", "properties" },
        ["pkg"] = new[] { "package", "packages" },
        ["lib"] = new[] { "library", "libraries" },
        ["fn"] = new[] { "function", "functions" },
        ["func"] = new[] { "function", "functions" },
        ["proc"] = new[] { "process", "processes", "procedure", "procedures" },
        ["op"] = new[] { "operation", "operations" },
        ["cmd"] = new[] { "command", "commands" },
        ["exec"] = new[] { "execute", "execution" },
        ["init"] = new[] { "initialize", "initialization" },
        ["impl"] = new[] { "implementation", "implement" },
        ["ext"] = new[] { "extension", "extensions", "external" },
        ["int"] = new[] { "internal", "integer" },
        ["pub"] = new[] { "public", "publish", "publication" },
        ["priv"] = new[] { "private" },
        ["mgr"] = new[] { "manager", "managers" },
        ["svc"] = new[] { "service", "services" },
        ["srv"] = new[] { "server", "servers" },
        ["ctx"] = new[] { "context", "contexts" },
        ["conn"] = new[] { "connection", "connections" },
        ["db"] = new[] { "database", "databases" },
        ["tbl"] = new[] { "table", "tables" },
        ["col"] = new[] { "column", "columns" },
        ["rec"] = new[] { "record", "records" },
        ["fld"] = new[] { "field", "fields" },
        ["lbl"] = new[] { "label", "labels" },
        ["btn"] = new[] { "button", "buttons" },
        ["chk"] = new[] { "check", "checkbox" },
        ["dlg"] = new[] { "dialog", "dialogs" },
        ["frm"] = new[] { "form", "forms" },
        ["nav"] = new[] { "navigation", "navigate" },
        ["pg"] = new[] { "page", "pages" },
        ["sec"] = new[] { "section", "sections", "security" },
        ["hdr"] = new[] { "header", "headers" },
        ["ftr"] = new[] { "footer", "footers" },
        ["cmt"] = new[] { "comment", "comments" },
        ["txt"] = new[] { "text", "texts" },
        ["fmt"] = new[] { "format", "formats" },
        ["lnk"] = new[] { "link", "links" },
        ["url"] = new[] { "uniform resource locator" },
        ["uri"] = new[] { "uniform resource identifier" },
        ["api"] = new[] { "application programming interface" },
    };

    /// <summary>
    /// Reverse map: full form → abbreviation for bidirectional matching.
    /// Built from Abbreviations dictionary.
    /// </summary>
    private static readonly Dictionary<string, string> ReverseAbbreviations;

    /// <summary>
    /// Common English suffixes for stem matching (pre-sorted by length descending for greedy match).
    /// </summary>
    private static readonly string[] CommonSuffixes =
    {
        "tion", "sion", "ment", "ness", "ated", "ting", "ance", "ence",
        "able", "ible", "less", "ity", "ing", "ous", "ive", "ful", "ted", "ed",
    };

    /// <summary>
    /// Pre-sorted suffixes by length descending for greedy matching (cached for performance).
    /// </summary>
    private static readonly string[] SortedSuffixesByLengthDesc =
        CommonSuffixes.OrderByDescending(s => s.Length).ToArray();

    /// <summary>
    /// Irregular plural → singular mappings common in APIs.
    /// </summary>
    private static readonly Dictionary<string, string> IrregularPlurals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["people"] = "person",
        ["children"] = "child",
        ["men"] = "man",
        ["women"] = "woman",
        ["mice"] = "mouse",
        ["data"] = "datum",
        ["criteria"] = "criterion",
        ["analyses"] = "analysis",
        ["indices"] = "index",
        ["matrices"] = "matrix",
        ["vertices"] = "vertex",
        ["statuses"] = "status",
        ["addresses"] = "address",
    };

    static SemanticTokenMatcher()
    {
        ReverseAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (abbr, fullForms) in Abbreviations)
        {
            foreach (var fullForm in fullForms)
            {
                ReverseAbbreviations[fullForm] = abbr;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TokenMatchResult> FindMatches(
        IReadOnlyCollection<string> sourceTokens,
        IReadOnlyCollection<string> targetTokens,
        double minScore = 0.65)
    {
        if (sourceTokens == null || sourceTokens.Count == 0
            || targetTokens == null || targetTokens.Count == 0)
        {
            return Array.Empty<TokenMatchResult>();
        }

        var results = new List<TokenMatchResult>();

        foreach (var source in sourceTokens)
        {
            foreach (var target in targetTokens)
            {
                var result = Match(source, target);
                if (result != null && result.Score >= minScore)
                {
                    results.Add(result);
                }
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.SourceToken, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.MatchedToken, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public TokenMatchResult Match(string sourceToken, string targetToken)
    {
        if (string.IsNullOrWhiteSpace(sourceToken) || string.IsNullOrWhiteSpace(targetToken))
        {
            return null;
        }

        var normalizedSource = NormalizeToken(sourceToken);
        var normalizedTarget = NormalizeToken(targetToken);

        if (string.IsNullOrWhiteSpace(normalizedSource) || string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return null;
        }

        // 1. Exact match (case-insensitive).
        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new TokenMatchResult
            {
                SourceToken = sourceToken,
                MatchedToken = targetToken,
                Score = 1.0,
                MatchType = TokenMatchType.Exact,
            };
        }

        // 2. Plural/singular normalization.
        var singularSource = Singularize(normalizedSource);
        var singularTarget = Singularize(normalizedTarget);

        if (string.Equals(singularSource, singularTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new TokenMatchResult
            {
                SourceToken = sourceToken,
                MatchedToken = targetToken,
                Score = 0.95,
                MatchType = TokenMatchType.PluralSingular,
            };
        }

        // 3. Abbreviation matching.
        if (IsAbbreviationMatch(normalizedSource, normalizedTarget))
        {
            return new TokenMatchResult
            {
                SourceToken = sourceToken,
                MatchedToken = targetToken,
                Score = 0.85,
                MatchType = TokenMatchType.Abbreviation,
            };
        }

        // 4. Stem matching.
        var stemSource = StripCommonSuffix(singularSource);
        var stemTarget = StripCommonSuffix(singularTarget);

        if (stemSource.Length >= 3 && stemTarget.Length >= 3
            && string.Equals(stemSource, stemTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new TokenMatchResult
            {
                SourceToken = sourceToken,
                MatchedToken = targetToken,
                Score = 0.80,
                MatchType = TokenMatchType.StemMatch,
            };
        }

        // 5. Substring containment (min 3 chars).
        if (IsSubstringMatch(normalizedSource, normalizedTarget))
        {
            return new TokenMatchResult
            {
                SourceToken = sourceToken,
                MatchedToken = targetToken,
                Score = 0.70,
                MatchType = TokenMatchType.Substring,
            };
        }

        return null;
    }

    /// <inheritdoc />
    public string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Convert a plural English word to singular.
    /// Handles common patterns: -ies, -ses, -zes, -xes, -ches, -shes, -s.
    /// </summary>
    internal static string Singularize(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 3)
        {
            return word;
        }

        var lower = word.ToLowerInvariant();

        // Check irregular plurals first.
        if (IrregularPlurals.TryGetValue(lower, out var irregular))
        {
            return irregular;
        }

        // -ies → -y (categories → category), but not "series".
        if (lower.EndsWith("ies", StringComparison.Ordinal) && lower.Length > 4
            && !lower.Equals("series", StringComparison.Ordinal))
        {
            return lower[..^3] + "y";
        }

        // -ses, -zes, -xes, -ches, -shes → remove -es.
        if (lower.EndsWith("ses", StringComparison.Ordinal)
            || lower.EndsWith("zes", StringComparison.Ordinal)
            || lower.EndsWith("xes", StringComparison.Ordinal)
            || lower.EndsWith("ches", StringComparison.Ordinal)
            || lower.EndsWith("shes", StringComparison.Ordinal))
        {
            return lower[..^2];
        }

        // -s → remove -s (but not "ss", "us", "is").
        if (lower.EndsWith('s')
            && !lower.EndsWith("ss", StringComparison.Ordinal)
            && !lower.EndsWith("us", StringComparison.Ordinal)
            && !lower.EndsWith("is", StringComparison.Ordinal))
        {
            return lower[..^1];
        }

        return lower;
    }

    /// <summary>
    /// Check if one token is a known abbreviation of the other (bidirectional).
    /// </summary>
    private static bool IsAbbreviationMatch(string a, string b)
    {
        // Check a is abbreviation of b.
        if (Abbreviations.TryGetValue(a, out var aExpansions)
            && aExpansions.Any(e => e.Equals(b, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check b is abbreviation of a.
        if (Abbreviations.TryGetValue(b, out var bExpansions)
            && bExpansions.Any(e => e.Equals(a, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check via reverse map: both resolve to same abbreviation.
        if (ReverseAbbreviations.TryGetValue(a, out var aAbbr)
            && ReverseAbbreviations.TryGetValue(b, out var bAbbr)
            && aAbbr.Equals(bAbbr, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Strip common English suffixes for stem comparison.
    /// Uses pre-sorted suffixes by length descending for greedy matching.
    /// </summary>
    private static string StripCommonSuffix(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 5)
        {
            return word;
        }

        foreach (var suffix in SortedSuffixesByLengthDesc)
        {
            if (word.Length > suffix.Length + 2
                && word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return word[..^suffix.Length];
            }
        }

        return word;
    }

    /// <summary>
    /// Check if one token contains the other as a substring (min 3 chars).
    /// </summary>
    private static bool IsSubstringMatch(string a, string b)
    {
        if (a.Length < 3 || b.Length < 3)
        {
            return false;
        }

        // The shorter one must be at least 3 chars and contained in the longer one.
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;

        return shorter.Length >= 3 && longer.Contains(shorter, StringComparison.OrdinalIgnoreCase);
    }
}
