using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class VariableExtractor : IVariableExtractor
{
    private readonly ILogger<VariableExtractor> _logger;

    public VariableExtractor(ILogger<VariableExtractor> logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, string> Extract(
        HttpTestResponse response,
        IReadOnlyList<ExecutionVariableRuleDto> variables,
        string requestBody = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (variables == null || variables.Count == 0)
        {
            return result;
        }

        foreach (var variable in variables)
        {
            string extracted = null;
            var effectiveJsonPath = ResolveJsonPath(variable);

            try
            {
                extracted = variable.ExtractFrom switch
                {
                    "ResponseBody" => ExtractFromResponseBody(response.Body, effectiveJsonPath),
                    "ResponseHeader" => ExtractFromHeader(response.Headers, variable.HeaderName),
                    "RequestBody" => ExtractFromResponseBody(requestBody, effectiveJsonPath),
                    "Status" => response.StatusCode?.ToString(),
                    _ => null,
                };

                extracted = ApplyRegex(extracted, variable.Regex);

                // Guardrail: some historical/generated suites may attach ID extraction rules
                // (e.g. $.data.id or Location header tail) to non-ID variable names such as
                // price/stock/name. That pollutes downstream request bodies with identifiers.
                if (!string.IsNullOrEmpty(extracted) &&
                    ShouldSkipIdentifierExtractionForVariable(variable, effectiveJsonPath))
                {
                    _logger.LogDebug(
                        "Skipping identifier extraction for non-identifier variable. Variable={VariableName}, ExtractFrom={ExtractFrom}, JsonPath={JsonPath}, HeaderName={HeaderName}",
                        variable.VariableName,
                        variable.ExtractFrom,
                        effectiveJsonPath,
                        variable.HeaderName);
                    extracted = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Variable extraction failed. Variable={VariableName}, ExtractFrom={ExtractFrom}",
                    variable.VariableName, variable.ExtractFrom);
            }

            if (!string.IsNullOrEmpty(extracted))
            {
                result[variable.VariableName] = extracted;
            }
            else if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                result[variable.VariableName] = variable.DefaultValue;
            }
        }

        return result;
    }

    private static bool ShouldSkipIdentifierExtractionForVariable(ExecutionVariableRuleDto variable, string effectiveJsonPath)
    {
        if (variable == null || IsIdentifierVariableName(variable.VariableName))
        {
            return false;
        }

        return IsIdentifierSource(variable, effectiveJsonPath);
    }

    private static bool IsIdentifierVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        return string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdentifierSource(ExecutionVariableRuleDto variable, string effectiveJsonPath)
    {
        if (variable == null)
        {
            return false;
        }

        if (string.Equals(variable.ExtractFrom, "ResponseBody", StringComparison.OrdinalIgnoreCase))
        {
            return IsIdentifierJsonPath(effectiveJsonPath);
        }

        if (string.Equals(variable.ExtractFrom, "ResponseHeader", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(variable.HeaderName, "Location", StringComparison.OrdinalIgnoreCase)
                && string.Equals(variable.Regex, "([^/?#]+)$", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsIdentifierJsonPath(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        var normalized = jsonPath.Trim().ToLowerInvariant();
        return normalized == "$.id"
            || normalized == "$._id"
            || normalized.EndsWith(".id", StringComparison.Ordinal)
            || normalized.EndsWith("._id", StringComparison.Ordinal)
            || normalized.Contains("].id", StringComparison.Ordinal)
            || normalized.Contains("]._id", StringComparison.Ordinal);
    }

    private static string ResolveJsonPath(ExecutionVariableRuleDto variable)
    {
        if (variable == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(variable.JsonPath))
        {
            return variable.JsonPath;
        }

        if (!string.Equals(variable.ExtractFrom, "RequestBody", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return InferRequestBodyJsonPath(variable.VariableName);
    }

    private static string InferRequestBodyJsonPath(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        var normalized = variableName.Trim();
        foreach (var prefix in new[] { "registered", "created", "generated", "new", "saved" })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && normalized.Length > prefix.Length)
            {
                normalized = normalized[prefix.Length..];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.IndexOf("email", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "$.email";
        }

        if (normalized.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "$.password";
        }

        var fieldName = char.ToLowerInvariant(normalized[0]) + normalized[1..];
        return "$." + fieldName;
    }

    private static string ExtractFromResponseBody(string body, string jsonPath)
    {
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(jsonPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var element = NavigateJsonPath(doc.RootElement, jsonPath);
            return element?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractFromHeader(Dictionary<string, string> headers, string headerName)
    {
        if (headers == null || string.IsNullOrEmpty(headerName))
        {
            return null;
        }

        foreach (var kvp in headers)
        {
            if (kvp.Key.Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    private static string ApplyRegex(string input, string regex)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(regex))
        {
            return input;
        }

        var match = Regex.Match(input, regex, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        if (match.Groups["value"] is { Success: true, Value.Length: > 0 } namedGroup)
        {
            return namedGroup.Value;
        }

        return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
    }

    internal static JsonElement? NavigateJsonPath(JsonElement root, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return root;
        }

        // Remove leading "$." if present
        var normalized = path.StartsWith("$.") ? path[2..] : path.TrimStart('$').TrimStart('.');

        if (string.IsNullOrEmpty(normalized))
        {
            return root;
        }

        var current = root;
        var segments = ParsePathSegments(normalized);

        foreach (var segment in segments)
        {
            if (segment.ArrayIndex.HasValue)
            {
                // Property + array access like "items[0]"
                if (!string.IsNullOrEmpty(segment.PropertyName))
                {
                    if (!TryGetPropertyCaseInsensitive(current, segment.PropertyName, out var propElement))
                    {
                        return null;
                    }

                    current = propElement;
                }

                if (current.ValueKind != JsonValueKind.Array || segment.ArrayIndex.Value >= current.GetArrayLength())
                {
                    return null;
                }

                current = current[segment.ArrayIndex.Value];
            }
            else
            {
                if (!TryGetPropertyCaseInsensitive(current, segment.PropertyName, out var propElement))
                {
                    return null;
                }

                current = propElement;
            }
        }

        return current;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
        {
            propertyValue = default;
            return false;
        }

        if (element.TryGetProperty(propertyName, out propertyValue))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propertyValue = property.Value;
                return true;
            }
        }

        // id ↔ _id alias: MongoDB returns "_id" but LLM-generated paths use "id" (and vice-versa).
        var altName = propertyName.Equals("id", StringComparison.OrdinalIgnoreCase)
            ? "_id"
            : propertyName.Equals("_id", StringComparison.OrdinalIgnoreCase) ? "id" : null;

        if (altName != null)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(altName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }
        }

        propertyValue = default;
        return false;
    }

    private static List<PathSegment> ParsePathSegments(string path)
    {
        var segments = new List<PathSegment>();
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            var bracketIndex = part.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var propName = bracketIndex > 0 ? part[..bracketIndex] : null;
                var indexStr = part[(bracketIndex + 1)..part.IndexOf(']')];
                if (int.TryParse(indexStr, out var index))
                {
                    segments.Add(new PathSegment { PropertyName = propName, ArrayIndex = index });
                }
            }
            else
            {
                segments.Add(new PathSegment { PropertyName = part });
            }
        }

        return segments;
    }

    private class PathSegment
    {
        public string PropertyName { get; set; }
        public int? ArrayIndex { get; set; }
    }
}
