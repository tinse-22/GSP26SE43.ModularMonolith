using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;

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
        IReadOnlyList<ExecutionVariableRuleDto> variables)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (variables == null || variables.Count == 0)
        {
            return result;
        }

        foreach (var variable in variables)
        {
            string extracted = null;

            try
            {
                extracted = variable.ExtractFrom switch
                {
                    "ResponseBody" => ExtractFromResponseBody(response.Body, variable.JsonPath),
                    "ResponseHeader" => ExtractFromHeader(response.Headers, variable.HeaderName),
                    "Status" => response.StatusCode?.ToString(),
                    _ => null,
                };
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
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment.PropertyName, out var propElement))
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
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment.PropertyName, out var propElement))
                {
                    return null;
                }

                current = propElement;
            }
        }

        return current;
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
