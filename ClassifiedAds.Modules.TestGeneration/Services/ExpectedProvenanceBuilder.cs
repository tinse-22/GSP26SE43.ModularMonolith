using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public static class ExpectedProvenanceBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "srs",
        "openapi",
        "n8n",
        "business_rule",
        "ai_inferred",
        "unknown",
    };

    private static readonly HashSet<string> AllowedConfidence = new(StringComparer.OrdinalIgnoreCase)
    {
        "high",
        "medium",
        "low",
    };

    public static string Normalize(string expectedProvenance)
    {
        var items = Deserialize(expectedProvenance);
        return items.Count == 0 ? null : Serialize(items);
    }

    public static string Build(
        N8nTestCaseExpectation expectation,
        SrsRequirement requirement = null,
        string fallbackSource = null)
    {
        if (expectation == null)
        {
            return null;
        }

        var provided = Deserialize(expectation.ExpectedProvenance);
        if (provided.Count > 0)
        {
            return Serialize(provided);
        }

        var requirementCode = expectation.RequirementCode ?? requirement?.RequirementCode;
        var evidence = BuildRequirementEvidence(requirement);
        var source = NormalizeSourceForEvidence(fallbackSource ?? expectation.ExpectationSource, evidence);
        var confidence = source is "srs" or "openapi" or "business_rule" or "n8n" ? "high" : "low";
        var items = new List<ExpectedProvenanceItem>();

        AddStatuses(items, expectation.ExpectedStatus, source, requirementCode, evidence, confidence);
        AddList(items, "bodyContains", "bodyContains", expectation.BodyContains, source, requirementCode, evidence, confidence);
        AddList(items, "bodyNotContains", "bodyNotContains", expectation.BodyNotContains, source, requirementCode, evidence, confidence);
        AddMap(items, "headerCheck", expectation.HeaderChecks, source, requirementCode, evidence, confidence);
        AddMap(items, "jsonPathCheck", expectation.JsonPathChecks, source, requirementCode, evidence, confidence);

        if (expectation.MaxResponseTime.HasValue)
        {
            items.Add(Create("maxResponseTime", "responseTime", expectation.MaxResponseTime.Value.ToString(), source, requirementCode, evidence, confidence));
        }

        return items.Count == 0 ? null : Serialize(items);
    }

    public static string BuildFromSerializedExpectation(
        string expectedStatus,
        string bodyContains,
        string bodyNotContains,
        string headerChecks,
        string jsonPathChecks,
        int? maxResponseTime,
        string expectedProvenance,
        string expectationSource,
        string requirementCode,
        SrsRequirement requirement = null)
    {
        var provided = Deserialize(expectedProvenance);
        if (provided.Count > 0)
        {
            return Serialize(provided);
        }

        var code = requirementCode ?? requirement?.RequirementCode;
        var evidence = BuildRequirementEvidence(requirement);
        var source = NormalizeSourceForEvidence(expectationSource, evidence);
        var confidence = source is "srs" or "openapi" or "business_rule" or "n8n" ? "high" : "low";
        var items = new List<ExpectedProvenanceItem>();

        AddStatuses(items, ParseList<int>(expectedStatus), source, code, evidence, confidence);
        AddList(items, "bodyContains", "bodyContains", ParseList<string>(bodyContains), source, code, evidence, confidence);
        AddList(items, "bodyNotContains", "bodyNotContains", ParseList<string>(bodyNotContains), source, code, evidence, confidence);
        AddMap(items, "headerCheck", ParseMap(headerChecks), source, code, evidence, confidence);
        AddMap(items, "jsonPathCheck", ParseMap(jsonPathChecks), source, code, evidence, confidence);

        if (maxResponseTime.HasValue)
        {
            items.Add(Create("maxResponseTime", "responseTime", maxResponseTime.Value.ToString(), source, code, evidence, confidence));
        }

        return items.Count == 0 ? null : Serialize(items);
    }

    public static string BuildFromEvidence(
        N8nTestCaseExpectation expectation,
        string requirementCode,
        string evidence,
        string source = "srs")
    {
        if (expectation == null || string.IsNullOrWhiteSpace(evidence))
        {
            return null;
        }

        var normalizedSource = NormalizeSourceForEvidence(source, evidence);
        var confidence = normalizedSource is "srs" or "openapi" or "business_rule" or "n8n" ? "high" : "low";
        var items = new List<ExpectedProvenanceItem>();

        AddStatuses(items, expectation.ExpectedStatus, normalizedSource, requirementCode, evidence, confidence);
        AddList(items, "bodyContains", "bodyContains", expectation.BodyContains, normalizedSource, requirementCode, evidence, confidence);
        AddList(items, "bodyNotContains", "bodyNotContains", expectation.BodyNotContains, normalizedSource, requirementCode, evidence, confidence);
        AddMap(items, "headerCheck", expectation.HeaderChecks, normalizedSource, requirementCode, evidence, confidence);
        AddMap(items, "jsonPathCheck", expectation.JsonPathChecks, normalizedSource, requirementCode, evidence, confidence);

        if (expectation.MaxResponseTime.HasValue)
        {
            items.Add(Create("maxResponseTime", "responseTime", expectation.MaxResponseTime.Value.ToString(), normalizedSource, requirementCode, evidence, confidence));
        }

        return items.Count == 0 ? null : Serialize(items);
    }

    public static string BuildRequirementEvidence(SrsRequirement requirement)
    {
        if (requirement == null)
        {
            return null;
        }

        var parts = new[]
        {
            requirement.Title,
            requirement.Description,
            requirement.RefinedConstraints,
            requirement.TestableConstraints,
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim());

        var evidence = string.Join(" | ", parts);
        return string.IsNullOrWhiteSpace(evidence) ? null : evidence;
    }

    public static string NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "unknown";
        }

        return source.Trim().ToLowerInvariant() switch
        {
            "srs" => "srs",
            "swagger" => "openapi",
            "openapi" => "openapi",
            "n8n" => "n8n",
            "businessrule" => "business_rule",
            "business_rule" => "business_rule",
            "llm" => "ai_inferred",
            "ai" => "ai_inferred",
            "ai_inferred" => "ai_inferred",
            "default" => "unknown",
            "unknown" => "unknown",
            _ => "unknown",
        };
    }

    private static string NormalizeSourceForEvidence(string source, string evidence)
    {
        var normalized = NormalizeSource(source);
        return !string.IsNullOrWhiteSpace(evidence) && normalized is "ai_inferred" or "unknown"
            ? "srs"
            : normalized;
    }

    private static void AddStatuses(
        ICollection<ExpectedProvenanceItem> items,
        IEnumerable<int> statuses,
        string source,
        string requirementCode,
        string evidence,
        string confidence)
    {
        foreach (var status in statuses ?? Enumerable.Empty<int>())
        {
            items.Add(Create("expectedStatus", "status", status.ToString(), source, requirementCode, evidence, confidence));
        }
    }

    private static void AddList(
        ICollection<ExpectedProvenanceItem> items,
        string field,
        string type,
        IEnumerable<string> values,
        string source,
        string requirementCode,
        string evidence,
        string confidence)
    {
        foreach (var value in values?.Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>())
        {
            items.Add(Create(field, type, value, source, requirementCode, evidence, confidence));
        }
    }

    private static void AddMap(
        ICollection<ExpectedProvenanceItem> items,
        string type,
        IDictionary<string, string> values,
        string source,
        string requirementCode,
        string evidence,
        string confidence)
    {
        foreach (var pair in values ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            items.Add(Create(pair.Key, type, pair.Value, source, requirementCode, evidence, confidence));
        }
    }

    private static ExpectedProvenanceItem Create(
        string field,
        string type,
        string expected,
        string source,
        string requirementCode,
        string evidence,
        string confidence)
    {
        return new ExpectedProvenanceItem
        {
            Field = field,
            Type = type,
            Expected = expected,
            Source = NormalizeSource(source),
            RequirementCode = string.IsNullOrWhiteSpace(requirementCode) ? null : requirementCode.Trim(),
            Evidence = string.IsNullOrWhiteSpace(evidence) ? null : evidence.Trim(),
            Confidence = NormalizeConfidence(confidence),
        };
    }

    private static string NormalizeConfidence(string confidence)
        => AllowedConfidence.Contains(confidence ?? string.Empty) ? confidence.Trim().ToLowerInvariant() : "low";

    private static List<ExpectedProvenanceItem> Deserialize(string expectedProvenance)
    {
        if (string.IsNullOrWhiteSpace(expectedProvenance))
        {
            return new List<ExpectedProvenanceItem>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<ExpectedProvenanceItem>>(expectedProvenance, JsonOpts)
                ?? new List<ExpectedProvenanceItem>();
            return items
                .Where(x => !string.IsNullOrWhiteSpace(x?.Type))
                .Select(NormalizeItem)
                .Where(x => x != null)
                .ToList();
        }
        catch
        {
            return new List<ExpectedProvenanceItem>();
        }
    }

    private static ExpectedProvenanceItem NormalizeItem(ExpectedProvenanceItem item)
    {
        var source = NormalizeSource(item.Source);
        if (!AllowedSources.Contains(source))
        {
            source = "unknown";
        }

        return new ExpectedProvenanceItem
        {
            Field = string.IsNullOrWhiteSpace(item.Field) ? null : item.Field.Trim(),
            Expected = item.Expected,
            Type = item.Type.Trim(),
            Source = source,
            RequirementCode = string.IsNullOrWhiteSpace(item.RequirementCode) ? null : item.RequirementCode.Trim(),
            Evidence = string.IsNullOrWhiteSpace(item.Evidence) ? null : item.Evidence.Trim(),
            Confidence = NormalizeConfidence(item.Confidence),
        };
    }

    private static string Serialize(List<ExpectedProvenanceItem> items)
        => JsonSerializer.Serialize(items, JsonOpts);

    private static List<T> ParseList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? new List<T>();
        }
        catch
        {
            if (typeof(T) == typeof(int) && int.TryParse(json.Trim('[', ']', ' '), out var single))
            {
                return new List<T> { (T)(object)single };
            }

            return new List<T>();
        }
    }

    private static Dictionary<string, string> ParseMap(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
