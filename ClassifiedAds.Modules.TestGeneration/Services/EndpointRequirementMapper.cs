using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public enum RequirementRelevance
{
    None = 0,
    Direct = 1,
    Partial = 2,
    Dependency = 3,
}

public enum RequirementMatchConfidence
{
    Low = 0,
    Medium = 1,
    High = 2,
}

public sealed class RequirementMatch
{
    public SrsRequirement Requirement { get; init; }

    public string RequirementCode => Requirement?.RequirementCode;

    public RequirementRelevance Relevance { get; init; }

    public RequirementMatchConfidence Confidence { get; init; }

    public List<string> MatchedSignals { get; init; } = new();

    public bool IsCoverable =>
        Relevance is RequirementRelevance.Direct or RequirementRelevance.Partial &&
        Confidence is RequirementMatchConfidence.High or RequirementMatchConfidence.Medium;
}

public interface IEndpointRequirementMapper
{
    IReadOnlyList<RequirementMatch> MapRequirementsToEndpoint(
        ApiEndpointMetadataDto endpoint,
        IReadOnlyList<SrsRequirement> requirements);
}

public sealed class EndpointRequirementMapper : IEndpointRequirementMapper
{
    private static readonly Regex TokenRegex = new(@"[A-Za-z][A-Za-z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "the", "and", "for", "with", "from", "into", "this", "that", "must", "shall",
        "should", "when", "then", "user", "users", "data", "system", "request", "response",
        "success", "error", "valid", "invalid", "create", "created", "update", "delete", "get",
        "post", "put", "patch", "http", "https"
    };

    private static readonly string[] SecurityTokens = { "security", "secure", "auth", "authentication", "authorization", "password", "credential", "token", "jwt" };
    private static readonly string[] ValidationTokens = { "required", "format", "min", "max", "minimum", "maximum", "length", "enum", "pattern", "unique", "duplicate", "validation" };

    public IReadOnlyList<RequirementMatch> MapRequirementsToEndpoint(
        ApiEndpointMetadataDto endpoint,
        IReadOnlyList<SrsRequirement> requirements)
    {
        if (endpoint == null || requirements == null || requirements.Count == 0)
        {
            return Array.Empty<RequirementMatch>();
        }

        var endpointSignals = BuildEndpointSignals(endpoint);
        return requirements
            .Where(r => r != null)
            .Select(r => Match(endpoint, endpointSignals, r))
            .OrderByDescending(m => m.IsCoverable)
            .ThenByDescending(m => m.Confidence)
            .ThenBy(m => m.Requirement?.DisplayOrder)
            .ThenBy(m => m.RequirementCode)
            .ToList();
    }

    private static RequirementMatch Match(
        ApiEndpointMetadataDto endpoint,
        EndpointSignals endpointSignals,
        SrsRequirement requirement)
    {
        var signals = new List<string>();
        if (requirement.EndpointId == endpoint.EndpointId)
        {
            signals.Add("endpointId");
            return Build(requirement, RequirementRelevance.Direct, RequirementMatchConfidence.High, signals);
        }

        if (MatchesMappedEndpointPath(endpoint, requirement.MappedEndpointPath))
        {
            signals.Add("mappedEndpointPath");
            return Build(requirement, RequirementRelevance.Direct, RequirementMatchConfidence.High, signals);
        }

        var requirementTokens = ExtractRequirementTokens(requirement);
        var overlap = endpointSignals.SemanticTokens.Intersect(requirementTokens, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlap.Count > 0)
        {
            signals.AddRange(overlap.Select(x => $"token:{x}"));
        }

        var endpointIntent = ResolveIntent(endpointSignals.SemanticTokens);
        var requirementIntent = ResolveIntent(requirementTokens);
        if (!string.IsNullOrWhiteSpace(endpointIntent) &&
            string.Equals(endpointIntent, requirementIntent, StringComparison.OrdinalIgnoreCase))
        {
            signals.Add($"intent:{endpointIntent}");
            return Build(requirement, RequirementRelevance.Direct, RequirementMatchConfidence.High, signals);
        }

        if (IsDependencyIntent(endpointIntent, requirementIntent))
        {
            signals.Add($"dependency:{requirementIntent}->{endpointIntent}");
            return Build(requirement, RequirementRelevance.Dependency, RequirementMatchConfidence.Medium, signals);
        }

        if (!string.IsNullOrWhiteSpace(endpointIntent) &&
            !string.IsNullOrWhiteSpace(requirementIntent) &&
            !string.Equals(endpointIntent, requirementIntent, StringComparison.OrdinalIgnoreCase) &&
            requirement.RequirementType is not SrsRequirementType.Security and not SrsRequirementType.Constraint)
        {
            return Build(requirement, RequirementRelevance.None, RequirementMatchConfidence.Low, signals);
        }

        var fieldOverlap = endpointSignals.FieldTokens.Intersect(requirementTokens, StringComparer.OrdinalIgnoreCase).ToList();
        if (fieldOverlap.Count > 0)
        {
            signals.AddRange(fieldOverlap.Select(x => $"field:{x}"));
        }

        var isCrossCutting = IsCrossCuttingRequirement(requirement, requirementTokens);
        if (isCrossCutting && fieldOverlap.Count > 0)
        {
            var confidence = fieldOverlap.Count >= 2 ? RequirementMatchConfidence.High : RequirementMatchConfidence.Medium;
            return Build(requirement, RequirementRelevance.Partial, confidence, signals);
        }

        if (overlap.Count >= 2)
        {
            return Build(requirement, RequirementRelevance.Partial, RequirementMatchConfidence.Medium, signals);
        }

        if (overlap.Count == 1 && IsStrongEndpointToken(overlap[0]))
        {
            return Build(requirement, RequirementRelevance.Partial, RequirementMatchConfidence.Medium, signals);
        }

        return Build(requirement, RequirementRelevance.None, RequirementMatchConfidence.Low, signals);
    }

    private static RequirementMatch Build(
        SrsRequirement requirement,
        RequirementRelevance relevance,
        RequirementMatchConfidence confidence,
        List<string> signals) =>
        new()
        {
            Requirement = requirement,
            Relevance = relevance,
            Confidence = confidence,
            MatchedSignals = signals
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList(),
        };

    private static EndpointSignals BuildEndpointSignals(ApiEndpointMetadataDto endpoint)
    {
        var semanticText = string.Join(" ", new[]
        {
            endpoint.HttpMethod,
            endpoint.Path,
            endpoint.OperationId,
            string.Join(" ", endpoint.Responses?.Select(r => r.Description) ?? Array.Empty<string>())
        });

        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in endpoint.Parameters ?? Array.Empty<ApiEndpointParameterDescriptorDto>())
        {
            AddToken(fields, parameter.Name);
            foreach (var field in ExtractSchemaPropertyNames(parameter.Schema))
            {
                AddToken(fields, field);
            }
        }

        foreach (var schema in endpoint.ResponseSchemaPayloads ?? Array.Empty<string>())
        {
            foreach (var field in ExtractSchemaPropertyNames(schema))
            {
                AddToken(fields, field);
            }
        }

        return new EndpointSignals
        {
            SemanticTokens = ExtractTokens(semanticText).Concat(fields).ToHashSet(StringComparer.OrdinalIgnoreCase),
            FieldTokens = fields,
        };
    }

    private static HashSet<string> ExtractRequirementTokens(SrsRequirement requirement)
    {
        var text = string.Join(" ", new[]
        {
            requirement.RequirementCode,
            requirement.Title,
            requirement.Description,
            requirement.RequirementType.ToString(),
            requirement.TestableConstraints,
            requirement.RefinedConstraints,
            requirement.Assumptions,
            requirement.Ambiguities,
            requirement.MappedEndpointPath
        });

        return ExtractTokens(text);
    }

    private static HashSet<string> ExtractTokens(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (Match match in TokenRegex.Matches(text))
        {
            var token = NormalizeToken(match.Value);
            AddToken(result, token);
        }

        return result;
    }

    private static void AddToken(HashSet<string> result, string token)
    {
        token = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(token) || token.Length < 3 || StopWords.Contains(token))
        {
            return;
        }

        result.Add(token);
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token.Trim().Trim('{', '}', '[', ']', '"', '\'').ToLowerInvariant();
        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 4)
        {
            return normalized[..^3] + "y";
        }

        if (normalized.EndsWith("s", StringComparison.Ordinal) && normalized.Length > 3 && !normalized.EndsWith("ss", StringComparison.Ordinal))
        {
            return normalized[..^1];
        }

        return normalized;
    }

    private static string ResolveIntent(HashSet<string> tokens)
    {
        if (tokens.Contains("register") || tokens.Contains("registration") || tokens.Contains("signup"))
        {
            return "register";
        }

        if (tokens.Contains("login") || tokens.Contains("signin") || tokens.Contains("authenticate") || tokens.Contains("token"))
        {
            return "login";
        }

        if (tokens.Contains("health") || tokens.Contains("heartbeat"))
        {
            return "health";
        }

        if (tokens.Contains("category"))
        {
            return "category";
        }

        if (tokens.Contains("product"))
        {
            return "product";
        }

        return null;
    }

    private static bool IsDependencyIntent(string endpointIntent, string requirementIntent)
    {
        return string.Equals(endpointIntent, "login", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(requirementIntent, "register", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCrossCuttingRequirement(SrsRequirement requirement, HashSet<string> requirementTokens)
    {
        if (requirement.RequirementType is SrsRequirementType.Security or SrsRequirementType.Constraint)
        {
            return true;
        }

        return SecurityTokens.Any(requirementTokens.Contains) || ValidationTokens.Any(requirementTokens.Contains);
    }

    private static bool IsStrongEndpointToken(string token)
        => token is "register" or "registration" or "login" or "health" or "category" or "product";

    private static bool MatchesMappedEndpointPath(ApiEndpointMetadataDto endpoint, string mappedEndpointPath)
    {
        if (endpoint == null || string.IsNullOrWhiteSpace(mappedEndpointPath))
        {
            return false;
        }

        var mapped = mappedEndpointPath.Trim();
        var method = endpoint.HttpMethod?.Trim();
        var path = endpoint.Path?.Trim();
        return (!string.IsNullOrWhiteSpace(method) && mapped.Contains(method, StringComparison.OrdinalIgnoreCase)) &&
               (!string.IsNullOrWhiteSpace(path) && mapped.Contains(path, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ExtractSchemaPropertyNames(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            yield break;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(schemaJson);
        }
        catch
        {
            yield break;
        }

        using (document)
        {
            foreach (var name in ExtractSchemaPropertyNames(document.RootElement))
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<string> ExtractSchemaPropertyNames(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (element.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in ExtractSchemaPropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }

        foreach (var keyword in new[] { "items", "allOf", "oneOf", "anyOf" })
        {
            if (!element.TryGetProperty(keyword, out var child))
            {
                continue;
            }

            if (child.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in child.EnumerateArray())
                {
                    foreach (var nested in ExtractSchemaPropertyNames(item))
                    {
                        yield return nested;
                    }
                }
            }
            else
            {
                foreach (var nested in ExtractSchemaPropertyNames(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private sealed class EndpointSignals
    {
        public HashSet<string> SemanticTokens { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> FieldTokens { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
