using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

internal static class TestCaseVariableMaterializationHelper
{
    private const string FlowProducesTagPrefix = "flow-produces:";

    private static readonly HashSet<string> BuiltInRuntimeVariableNames = new (StringComparer.OrdinalIgnoreCase)
    {
        "tcUniqueId",
        "timestamp",
        "randomInt",
        "uuid",
        "runId",
        "runSuffix",
        "runIdSuffix",
        "runTimestamp",
    };

    public static void AddExplicitVariables(TestCase testCase, IEnumerable<N8nTestCaseVariable> variables)
    {
        if (testCase == null || variables == null)
        {
            return;
        }

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable?.VariableName))
            {
                continue;
            }

            if (testCase.Variables.Any(x =>
                string.Equals(x.VariableName, variable.VariableName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(variable.JsonPath) &&
                string.IsNullOrWhiteSpace(variable.HeaderName) &&
                string.IsNullOrWhiteSpace(variable.Regex) &&
                string.IsNullOrWhiteSpace(variable.DefaultValue))
            {
                continue;
            }

            testCase.Variables.Add(new TestCaseVariable
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCase.Id,
                VariableName = variable.VariableName.Trim(),
                ExtractFrom = ParseExtractFrom(variable.ExtractFrom),
                JsonPath = NormalizePath(variable.JsonPath),
                HeaderName = variable.HeaderName,
                Regex = variable.Regex,
                DefaultValue = variable.DefaultValue,
            });
        }
    }

    public static void AddRequestBodyProducerAliasVariables(TestCase testCase, IEnumerable<string> produces)
    {
        if (testCase?.Request == null || produces == null || string.IsNullOrWhiteSpace(testCase.Request.Body))
        {
            return;
        }

        if (!TryParseJsonObject(testCase.Request.Body, out var root))
        {
            return;
        }

        foreach (var variableName in produces.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (BuiltInRuntimeVariableNames.Contains(variableName) || IsTokenLikeVariableName(variableName))
            {
                continue;
            }

            if (testCase.Variables.Any(x =>
                string.Equals(x.VariableName, variableName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!TryFindRequestBodyProducerPath(root, variableName, "$", out var jsonPath))
            {
                continue;
            }

            testCase.Variables.Add(new TestCaseVariable
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCase.Id,
                VariableName = variableName,
                ExtractFrom = ExtractFrom.RequestBody,
                JsonPath = jsonPath,
            });
        }
    }

    public static void AddRequestBodyProducerAliasVariablesFromTags(TestCase testCase)
    {
        AddRequestBodyProducerAliasVariables(testCase, ExtractProducesFromTags(testCase?.Tags));
    }

    private static IEnumerable<string> ExtractProducesFromTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
            return tags
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith(FlowProducesTagPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(x => x[FlowProducesTagPrefix.Length..].Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static ExtractFrom ParseExtractFrom(string extractFrom)
    {
        if (string.IsNullOrWhiteSpace(extractFrom))
        {
            return ExtractFrom.ResponseBody;
        }

        return extractFrom.Trim().ToLowerInvariant() switch
        {
            "responsebody" or "response_body" or "body" => ExtractFrom.ResponseBody,
            "requestbody" or "request_body" => ExtractFrom.RequestBody,
            "responseheader" or "response_header" or "header" => ExtractFrom.ResponseHeader,
            "status" => ExtractFrom.Status,
            _ => ExtractFrom.ResponseBody,
        };
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }

    private static bool TryParseJsonObject(string body, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindRequestBodyProducerPath(JsonElement element, string variableName, string currentPath, out string jsonPath)
    {
        jsonPath = null;

        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var bestScore = 0;
        var bestPath = string.Empty;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{currentPath}.{property.Name}";
                var score = ScoreSemanticBodyFieldMatch(variableName, property.Name);
                if (score > bestScore && IsExtractableRequestBodyValue(property.Value))
                {
                    bestScore = score;
                    bestPath = propertyPath;
                }

                if ((property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array) &&
                    TryFindRequestBodyProducerPath(property.Value, variableName, propertyPath, out var nestedPath))
                {
                    var nestedLeaf = nestedPath.Split('.').LastOrDefault();
                    var nestedScore = ScoreSemanticBodyFieldMatch(variableName, nestedLeaf);
                    if (nestedScore > bestScore)
                    {
                        bestScore = nestedScore;
                        bestPath = nestedPath;
                    }
                }
            }
        }
        else
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindRequestBodyProducerPath(item, variableName, $"{currentPath}[{index}]", out var nestedPath))
                {
                    jsonPath = nestedPath;
                    return true;
                }

                index++;
            }
        }

        if (bestScore <= 0)
        {
            return false;
        }

        jsonPath = bestPath;
        return true;
    }

    private static bool IsExtractableRequestBodyValue(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null;
    }

    private static int ScoreSemanticBodyFieldMatch(string variableName, string fieldName)
    {
        var variableKey = NormalizeKey(variableName);
        var fieldKey = NormalizeKey(fieldName);
        if (string.IsNullOrWhiteSpace(variableKey) || string.IsNullOrWhiteSpace(fieldKey))
        {
            return 0;
        }

        if (string.Equals(variableKey, fieldKey, StringComparison.Ordinal))
        {
            return 100;
        }

        if (variableKey.EndsWith(fieldKey, StringComparison.Ordinal) && variableKey.Length > fieldKey.Length)
        {
            return 80 + Math.Min(fieldKey.Length, 10);
        }

        if (fieldKey.EndsWith(variableKey, StringComparison.Ordinal) && fieldKey.Length > variableKey.Length)
        {
            return 40 + Math.Min(variableKey.Length, 10);
        }

        return 0;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsTokenLikeVariableName(string variableName)
    {
        var key = NormalizeKey(variableName);
        return key.Contains("token", StringComparison.Ordinal)
            || key.Contains("jwt", StringComparison.Ordinal)
            || key.Contains("bearer", StringComparison.Ordinal)
            || key.Contains("session", StringComparison.Ordinal);
    }
}
