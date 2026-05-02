using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Extracts bodyContains and jsonPathChecks hints from Swagger error response schemas.
/// Prevents LLM from guessing assertion field names that may not exist in the API.
///
/// IMPORTANT: JSONPath keys preserve original casing from the Swagger schema.
/// If schema uses "Success" (PascalCase), key will be "$.Success" — NOT normalized.
/// This is correct as long as the API response and schema are in sync.
/// If the API returns a different casing than the schema, assertions will fail.
/// </summary>
internal static class ErrorResponseSchemaAnalyzer
{
    private static readonly string[] PriorityFields =
        { "success", "message", "error", "errors", "code", "status", "detail" };

    /// <summary>
    /// Extracts top-level property names from a JSON Schema object.
    /// Returns empty list if schema is null/invalid/not an object schema.
    /// </summary>
    public static List<string> ExtractFieldNames(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return ExtractFromElement(doc.RootElement)
                .Take(5)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Builds JSONPath assertions from error response schema.
    /// For "success" field on error scenario: asserts "false".
    /// For other fields: asserts "*" (field exists).
    /// Returns at most 2 assertions.
    /// </summary>
    public static Dictionary<string, string> BuildJsonPathAssertions(
        string schemaJson,
        TestType testType)
    {
        var fields = ExtractFieldNames(schemaJson);
        if (fields.Count == 0) return new Dictionary<string, string>();

        var ordered = fields
            .OrderBy(f =>
            {
                var idx = Array.IndexOf(PriorityFields, f.ToLowerInvariant());
                return idx >= 0 ? idx : 999;
            })
            .Take(2)
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in ordered)
        {
            // DESIGN: preserve original casing from Swagger schema (camelCase or PascalCase).
            // JSONPath key MUST match the actual response field casing. Schema is the source of truth.
            // Do NOT normalize to camelCase — if schema uses "Success", key should be "$.Success".
            var isSuccessField = string.Equals(field, "success", StringComparison.OrdinalIgnoreCase);
            var isErrorTest = testType == TestType.Boundary || testType == TestType.Negative;
            result[$"$.{field}"] = isSuccessField && isErrorTest ? "false" : "*";
        }
        return result;
    }

    private static IEnumerable<string> ExtractFromElement(JsonElement element)
    {
        // allOf: merge all sub-schemas
        if (element.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var sub in allOf.EnumerateArray())
            {
                foreach (var name in ExtractFromElement(sub))
                    yield return name;
            }
            yield break;
        }

        // oneOf / anyOf: use first variant
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (element.TryGetProperty(keyword, out var variants) && variants.ValueKind == JsonValueKind.Array)
            {
                var first = variants.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    foreach (var name in ExtractFromElement(first))
                        yield return name;
                }
                yield break;
            }
        }

        // Standard object with properties
        if (element.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                if (!string.IsNullOrWhiteSpace(prop.Name))
                    yield return prop.Name;
            }
        }
    }
}
