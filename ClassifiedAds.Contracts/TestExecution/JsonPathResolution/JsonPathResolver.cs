using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

public interface IJsonPathResolver
{
    JsonPathResolutionResult Resolve(JsonPathResolutionRequest request);
}

public sealed class JsonPathResolver : IJsonPathResolver
{
    private readonly HashSet<string> _wrapperNames;
    private readonly Dictionary<string, HashSet<string>> _fieldAliases;
    private readonly Dictionary<string, string> _canonicalFieldNames;

    public JsonPathResolver(JsonPathResolutionOptions options)
    {
        options ??= new JsonPathResolutionOptions();
        _wrapperNames = new HashSet<string>(
            options.WrapperNames?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        _fieldAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        _canonicalFieldNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var aliasGroup in options.FieldAliases ?? new Dictionary<string, List<string>>())
        {
            if (string.IsNullOrWhiteSpace(aliasGroup.Key))
            {
                continue;
            }

            var canonical = aliasGroup.Key.Trim();
            var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonical };
            foreach (var alias in aliasGroup.Value ?? [])
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    members.Add(alias.Trim());
                }
            }

            foreach (var member in members)
            {
                if (!_fieldAliases.TryGetValue(member, out var aliases))
                {
                    aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _fieldAliases[member] = aliases;
                }

                foreach (var alias in members.Where(x => !string.Equals(x, member, StringComparison.OrdinalIgnoreCase)))
                {
                    aliases.Add(alias);
                }

                _canonicalFieldNames.TryAdd(member, canonical.ToLowerInvariant());
            }
        }
    }

    public JsonPathResolutionResult Resolve(JsonPathResolutionRequest request)
    {
        var originalPath = NormalizePath(request?.OriginalPath);
        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return Unresolved(request?.OriginalPath, "invalid_jsonpath", []);
        }

        var candidates = new List<PathCandidate>();
        candidates.AddRange(FlattenActualResponse(request?.ActualResponseJson));
        candidates.AddRange(FlattenSwaggerSchemas(request?.SwaggerResponseSchemas));

        if (candidates.Count == 0)
        {
            return new JsonPathResolutionResult
            {
                OriginalPath = originalPath,
                ResolvedPath = originalPath,
                Confidence = 1m,
                ResolutionStrategy = "exact_no_candidates",
                Source = "original",
                IsResolved = true,
                Diagnostics = ["No actual response or Swagger schema candidates were available; using original JSONPath."],
                CandidatePaths = [],
            };
        }

        var exact = candidates
            .FirstOrDefault(x => PathsEquivalent(x.Path, originalPath));
        if (exact != null)
        {
            return Resolved(originalPath, exact.Path, 100, "exact", exact.Source, ["exact"]);
        }

        var scored = candidates
            .Select(candidate => Score(originalPath, candidate))
            .Where(candidate => candidate.Score >= 70)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path.Length)
            .ToList();

        if (scored.Count == 0)
        {
            var near = FindNearCandidates(originalPath, candidates);
            return new JsonPathResolutionResult
            {
                OriginalPath = originalPath,
                ResolvedPath = null,
                Confidence = 0m,
                ResolutionStrategy = "unresolved",
                Source = "resolver",
                IsResolved = false,
                Diagnostics = BuildUnresolvedDiagnostics(originalPath, near),
                CandidatePaths = near.Select(ToPublicCandidate).ToList(),
            };
        }

        var topScore = scored[0].Score;
        var top = scored.Where(x => x.Score == topScore).ToList();
        if (top.Count > 1)
        {
            return new JsonPathResolutionResult
            {
                OriginalPath = originalPath,
                ResolvedPath = null,
                Confidence = topScore / 100m,
                ResolutionStrategy = "ambiguous",
                Source = "resolver",
                IsResolved = false,
                IsAmbiguous = true,
                Diagnostics =
                [
                    $"Ambiguous JSONPath resolution for '{originalPath}'. Multiple candidates have the same score {topScore}.",
                    "No path was selected because resolving it would be unsafe.",
                ],
                CandidatePaths = top.Take(5).Select(ToPublicCandidate).ToList(),
            };
        }

        var best = scored[0];
        return new JsonPathResolutionResult
        {
            OriginalPath = originalPath,
            ResolvedPath = best.Path,
            Confidence = best.Score / 100m,
            ResolutionStrategy = string.Join("+", best.Reasons.Distinct(StringComparer.OrdinalIgnoreCase)),
            Source = best.Source,
            IsResolved = true,
            Diagnostics =
            [
                best.Path.Equals(originalPath, StringComparison.Ordinal)
                    ? $"JSONPath '{originalPath}' resolved without path rewrite."
                    : $"JSONPath '{originalPath}' normalized to '{best.Path}' via {string.Join(", ", best.Reasons)}.",
            ],
            CandidatePaths = [ToPublicCandidate(best)],
        };
    }

    private ScoredCandidate Score(string originalPath, PathCandidate candidate)
    {
        var expected = ParseParts(originalPath);
        var actual = ParseParts(candidate.Path);
        var reasons = new List<string>();

        if (expected.Leaf == null || actual.Leaf == null || !FieldsEquivalent(expected.Leaf, actual.Leaf))
        {
            return new ScoredCandidate(candidate.Path, candidate.Source, 0, []);
        }

        var score = 0;
        if (expected.StructuralSignature.Equals(actual.StructuralSignature, StringComparison.OrdinalIgnoreCase))
        {
            score = 96;
            reasons.Add("structural_match");
        }
        else if (expected.NonWrapperSignature.Equals(actual.NonWrapperSignature, StringComparison.OrdinalIgnoreCase))
        {
            score = 88;
            reasons.Add(expected.WrapperCount > actual.WrapperCount ? "wrapper_collapsed" : "wrapper_expanded");
        }
        else if (expected.NonWrapperLeafSignature.Equals(actual.NonWrapperLeafSignature, StringComparison.OrdinalIgnoreCase))
        {
            score = 74;
            reasons.Add("wrapper_adjusted");
        }
        else
        {
            return new ScoredCandidate(candidate.Path, candidate.Source, 0, []);
        }

        if (!expected.Leaf.Equals(actual.Leaf, StringComparison.OrdinalIgnoreCase))
        {
            score -= 4;
            reasons.Add("id_alias");
        }

        if (expected.ArraySignature.Equals(actual.ArraySignature, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(expected.ArraySignature))
            {
                reasons.Add("array_item_resolved");
            }
        }
        else if (!string.IsNullOrWhiteSpace(expected.ArraySignature) || !string.IsNullOrWhiteSpace(actual.ArraySignature))
        {
            score -= 12;
            reasons.Add("array_mismatch");
        }

        score -= Math.Min(Math.Abs(expected.Depth - actual.Depth) * 2, 10);

        return new ScoredCandidate(candidate.Path, candidate.Source, score, reasons);
    }

    private IReadOnlyList<ScoredCandidate> FindNearCandidates(string originalPath, IReadOnlyList<PathCandidate> candidates)
    {
        var expected = ParseParts(originalPath);
        return candidates
            .Select(candidate =>
            {
                var actual = ParseParts(candidate.Path);
                var score = 0;
                var reasons = new List<string>();
                if (expected.Leaf != null && actual.Leaf != null && FieldsEquivalent(expected.Leaf, actual.Leaf))
                {
                    score += 55;
                    reasons.Add(expected.Leaf.Equals(actual.Leaf, StringComparison.OrdinalIgnoreCase) ? "same_field" : "id_alias");
                }

                if (expected.ArraySignature != actual.ArraySignature)
                {
                    reasons.Add("array_mismatch");
                }

                if (expected.WrapperCount != actual.WrapperCount)
                {
                    reasons.Add(expected.WrapperCount > actual.WrapperCount ? "extra_wrapper" : "missing_wrapper");
                }

                score -= Math.Min(Math.Abs(expected.Depth - actual.Depth) * 3, 20);
                return new ScoredCandidate(candidate.Path, candidate.Source, score, reasons);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path.Length)
            .Take(5)
            .ToList();
    }

    private static IReadOnlyList<string> BuildUnresolvedDiagnostics(string originalPath, IReadOnlyList<ScoredCandidate> near)
    {
        if (near.Count == 0)
        {
            return
            [
                $"JSONPath '{originalPath}' was not found in actual response or Swagger schema candidates.",
                "Reason: field_not_found.",
            ];
        }

        var reasons = near
            .SelectMany(x => x.Reasons)
            .DefaultIfEmpty("field_not_found")
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return
        [
            $"JSONPath '{originalPath}' could not be mapped safely.",
            $"Reason(s): {string.Join(", ", reasons)}.",
            $"Nearest candidate path(s): {string.Join(", ", near.Select(x => x.Path))}.",
        ];
    }

    private static List<PathCandidate> FlattenActualResponse(string json)
    {
        var result = new List<PathCandidate>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            FlattenElement(doc.RootElement, "$", "actual_response", result);
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static List<PathCandidate> FlattenSwaggerSchemas(IReadOnlyCollection<string> schemas)
    {
        var result = new List<PathCandidate>();
        foreach (var schemaJson in schemas ?? [])
        {
            if (string.IsNullOrWhiteSpace(schemaJson))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(schemaJson);
                FlattenSchema(doc.RootElement, "$", result, depth: 0);
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return result;
    }

    private static void FlattenElement(JsonElement element, string path, string source, ICollection<PathCandidate> result)
    {
        if (path != "$")
        {
            result.Add(new PathCandidate(path, source));
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    FlattenElement(prop.Value, $"{path}.{prop.Name}", source, result);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{path}[{index++}]", source, result);
                }
                break;
        }
    }

    private static void FlattenSchema(JsonElement schema, string path, ICollection<PathCandidate> result, int depth)
    {
        if (depth > 16 || schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (path != "$")
        {
            result.Add(new PathCandidate(path, "swagger"));
        }

        if (TryGetProperty(schema, "properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                FlattenSchema(prop.Value, $"{path}.{prop.Name}", result, depth + 1);
            }
        }

        if (TryGetProperty(schema, "items", out var items))
        {
            FlattenSchema(items, $"{path}[0]", result, depth + 1);
        }

        foreach (var keyword in new[] { "allOf", "anyOf", "oneOf" })
        {
            if (!TryGetProperty(schema, keyword, out var union) || union.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var child in union.EnumerateArray())
            {
                FlattenSchema(child, path, result, depth + 1);
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool PathsEquivalent(string left, string right)
        => string.Equals(NormalizeArrayIndexes(left), NormalizeArrayIndexes(right), StringComparison.OrdinalIgnoreCase);

    private bool FieldsEquivalent(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _fieldAliases.TryGetValue(left, out var aliases)
            && aliases.Contains(right);
    }

    private JsonPathParts ParseParts(string path)
        => JsonPathParts.Parse(path, _wrapperNames, CanonicalField);

    private string CanonicalField(string field)
        => !string.IsNullOrWhiteSpace(field) && _canonicalFieldNames.TryGetValue(field, out var canonical)
            ? canonical
            : field?.ToLowerInvariant();

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var trimmed = path.Trim();
        if (trimmed == "$" || trimmed.StartsWith("$.", StringComparison.Ordinal) || trimmed.StartsWith("$[", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return "$." + trimmed.TrimStart('.');
    }

    private static string NormalizeArrayIndexes(string path)
        => Regex.Replace(path ?? string.Empty, @"\[\d+\]", "[*]");

    private static JsonPathResolutionResult Resolved(
        string originalPath,
        string resolvedPath,
        int score,
        string strategy,
        string source,
        IReadOnlyList<string> reasons)
        => new()
        {
            OriginalPath = originalPath,
            ResolvedPath = resolvedPath,
            Confidence = score / 100m,
            ResolutionStrategy = strategy,
            Source = source,
            IsResolved = true,
            Diagnostics = [$"JSONPath '{originalPath}' resolved to '{resolvedPath}' via {strategy}."],
            CandidatePaths = [new JsonPathCandidate { Path = resolvedPath, Source = source, Score = score, Reasons = reasons }],
        };

    private static JsonPathResolutionResult Unresolved(string originalPath, string reason, IReadOnlyList<ScoredCandidate> candidates)
        => new()
        {
            OriginalPath = originalPath,
            ResolvedPath = null,
            Confidence = 0m,
            ResolutionStrategy = "unresolved",
            Source = "resolver",
            IsResolved = false,
            Diagnostics = [$"JSONPath '{originalPath}' could not be resolved. Reason: {reason}."],
            CandidatePaths = candidates.Select(ToPublicCandidate).ToList(),
        };

    private static JsonPathCandidate ToPublicCandidate(ScoredCandidate candidate)
        => new()
        {
            Path = candidate.Path,
            Source = candidate.Source,
            Score = candidate.Score,
            Reasons = candidate.Reasons,
        };

    private sealed record PathCandidate(string Path, string Source);

    private sealed record ScoredCandidate(string Path, string Source, int Score, IReadOnlyList<string> Reasons);

    private sealed class JsonPathParts
    {
        private JsonPathParts(IReadOnlyList<PathPart> parts)
        {
            Parts = parts;
            Depth = parts.Count;
            WrapperCount = parts.Count(x => x.IsWrapper);
            Leaf = parts.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Property))?.Property;
            StructuralSignature = BuildSignature(parts, includeWrappers: true, leafOnly: false);
            NonWrapperSignature = BuildSignature(parts, includeWrappers: false, leafOnly: false);
            NonWrapperLeafSignature = BuildSignature(parts, includeWrappers: false, leafOnly: true);
            ArraySignature = string.Join("/", parts.Where(x => x.HasArray).Select((_, index) => index.ToString()));
        }

        public IReadOnlyList<PathPart> Parts { get; }

        public string Leaf { get; }

        public int Depth { get; }

        public int WrapperCount { get; }

        public string StructuralSignature { get; }

        public string NonWrapperSignature { get; }

        public string NonWrapperLeafSignature { get; }

        public string ArraySignature { get; }

        public static JsonPathParts Parse(
            string path,
            IReadOnlySet<string> wrapperNames,
            Func<string, string> canonicalField)
        {
            var normalized = NormalizePath(path);
            var body = normalized.StartsWith("$.", StringComparison.Ordinal)
                ? normalized[2..]
                : normalized.TrimStart('$').TrimStart('.');
            if (string.IsNullOrWhiteSpace(body))
            {
                return new JsonPathParts([]);
            }

            var parts = body.Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => ParsePart(part, wrapperNames, canonicalField))
                .ToList();
            return new JsonPathParts(parts);
        }

        private static PathPart ParsePart(
            string raw,
            IReadOnlySet<string> wrapperNames,
            Func<string, string> canonicalField)
        {
            var match = Regex.Match(raw, @"^(?<name>[^\[]*)(?<array>(?:\[\d+\]|\[\*\])*)$");
            var name = match.Success ? match.Groups["name"].Value : raw;
            var hasArray = match.Success && match.Groups["array"].Value.Length > 0;
            var canonical = canonicalField(name);
            return new PathPart(
                name,
                canonical,
                hasArray,
                wrapperNames.Contains(name));
        }

        private static string BuildSignature(IReadOnlyList<PathPart> parts, bool includeWrappers, bool leafOnly)
        {
            var selected = includeWrappers
                ? parts
                : parts.Where(x => !x.IsWrapper).ToList();

            if (leafOnly)
            {
                selected = selected.TakeLast(1).ToList();
            }

            return string.Join(
                ".",
                selected.Select(x => x.HasArray ? $"{x.CanonicalProperty}[]" : x.CanonicalProperty));
        }

    }

    private sealed record PathPart(string Property, string CanonicalProperty, bool HasArray, bool IsWrapper);
}
