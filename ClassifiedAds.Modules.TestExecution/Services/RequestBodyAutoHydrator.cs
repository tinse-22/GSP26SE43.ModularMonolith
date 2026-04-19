using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClassifiedAds.Modules.TestExecution.Services;

internal static class RequestBodyAutoHydrator
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "PATCH",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static bool TryHydrate(ExecutionTestCaseDto testCase, ApiEndpointMetadataDto endpointMetadata)
    {
        if (testCase?.Request == null || endpointMetadata == null)
        {
            return false;
        }

        if (!WriteMethods.Contains(testCase.Request.HttpMethod ?? string.Empty) ||
            !endpointMetadata.HasRequiredRequestBody ||
            IsNegativeLikeCase(testCase) ||
            !IsMissingOrEmptyObjectBody(testCase.Request.Body))
        {
            return false;
        }

        var schema = ExtractRequestBodySchema(endpointMetadata);
        if (string.IsNullOrWhiteSpace(schema) || !TryParseSchema(schema, out var schemaRoot))
        {
            return false;
        }

        var bodyNode = BuildNodeFromSchema(schemaRoot, propertyName: null, depth: 0);
        if (bodyNode is not JsonObject bodyObject || bodyObject.Count == 0)
        {
            return false;
        }

        testCase.Request.Body = bodyObject.ToJsonString(JsonOptions);
        if (string.IsNullOrWhiteSpace(testCase.Request.BodyType) ||
            string.Equals(testCase.Request.BodyType, "None", StringComparison.OrdinalIgnoreCase))
        {
            testCase.Request.BodyType = "JSON";
        }

        return true;
    }

    private static bool IsMissingOrEmptyObjectBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return true;
        }

        try
        {
            var node = JsonNode.Parse(body);
            return node is JsonObject obj && obj.Count == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNegativeLikeCase(ExecutionTestCaseDto testCase)
    {
        if (ContainsAny(testCase.TestType, "negative", "boundary", "invalid") ||
            ContainsAny(testCase.Name, "negative", "boundary", "invalid"))
        {
            return true;
        }

        var statuses = ParseExpectedStatuses(testCase.Expectation?.ExpectedStatus);
        return statuses.Count > 0 && statuses.All(status => status >= 400 && status < 600);
    }

    private static string ExtractRequestBodySchema(ApiEndpointMetadataDto endpointMetadata)
    {
        var bodyParameterSchema = endpointMetadata.Parameters?
            .Where(parameter => parameter != null &&
                                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(parameter.Schema))
            .OrderByDescending(parameter => parameter.IsRequired)
            .Select(parameter => parameter.Schema)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(bodyParameterSchema))
        {
            return bodyParameterSchema;
        }

        return endpointMetadata.ParameterSchemaPayloads?
            .FirstOrDefault(payload => !string.IsNullOrWhiteSpace(payload));
    }

    private static bool TryParseSchema(string schemaJson, out JsonElement schemaRoot)
    {
        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            schemaRoot = document.RootElement.Clone();
            return true;
        }
        catch
        {
            schemaRoot = default;
            return false;
        }
    }

    private static JsonNode BuildNodeFromSchema(JsonElement schema, string propertyName, int depth)
    {
        if (depth > 10)
        {
            return JsonValue.Create(BuildHeuristicStringValue(propertyName, null, null, null, null));
        }

        if (TryGetSchemaExample(schema, out var exampleNode))
        {
            return exampleNode;
        }

        if (TryGetSchemaDefault(schema, out var defaultNode))
        {
            return defaultNode;
        }

        if (TryGetEnumValue(schema, out var enumNode))
        {
            return enumNode;
        }

        if (TryBuildCompositeNode(schema, propertyName, depth, out var compositeNode))
        {
            return compositeNode;
        }

        var type = GetSchemaType(schema);
        return type switch
        {
            "object" => BuildObjectNode(schema, depth),
            "array" => BuildArrayNode(schema, propertyName, depth),
            "boolean" => JsonValue.Create(true),
            "integer" => JsonValue.Create((int)BuildNumericValue(schema, integerOnly: true)),
            "number" => JsonValue.Create((double)BuildNumericValue(schema, integerOnly: false)),
            _ => JsonValue.Create(BuildStringValue(propertyName, schema)),
        };
    }

    private static bool TryBuildCompositeNode(
        JsonElement schema,
        string propertyName,
        int depth,
        out JsonNode compositeNode)
    {
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(keyword, out var variants) || variants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var variant in variants.EnumerateArray())
            {
                var candidate = BuildNodeFromSchema(variant, propertyName, depth + 1);
                if (candidate != null)
                {
                    compositeNode = candidate;
                    return true;
                }
            }
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            JsonObject merged = null;
            JsonNode first = null;
            foreach (var variant in allOf.EnumerateArray())
            {
                var candidate = BuildNodeFromSchema(variant, propertyName, depth + 1);
                first ??= candidate;
                if (candidate is JsonObject candidateObject)
                {
                    merged ??= new JsonObject();
                    foreach (var property in candidateObject)
                    {
                        merged[property.Key] = property.Value?.DeepClone();
                    }
                }
            }

            compositeNode = merged ?? first;
            return compositeNode != null;
        }

        compositeNode = null;
        return false;
    }

    private static JsonNode BuildObjectNode(JsonElement schema, int depth)
    {
        var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    requiredNames.Add(item.GetString());
                }
            }
        }

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return new JsonObject();
        }

        var result = new JsonObject();
        foreach (var property in properties.EnumerateObject())
        {
            if (requiredNames.Count > 0 &&
                !requiredNames.Contains(property.Name) &&
                !ShouldIncludeOptionalProperty(property.Name))
            {
                continue;
            }

            result[property.Name] = BuildNodeFromSchema(property.Value, property.Name, depth + 1);
        }

        if (result.Count == 0)
        {
            var firstProperty = properties.EnumerateObject().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstProperty.Name))
            {
                result[firstProperty.Name] = BuildNodeFromSchema(firstProperty.Value, firstProperty.Name, depth + 1);
            }
        }

        return result;
    }

    private static JsonNode BuildArrayNode(JsonElement schema, string propertyName, int depth)
    {
        var result = new JsonArray();
        if (!schema.TryGetProperty("items", out var items))
        {
            return result;
        }

        result.Add(BuildNodeFromSchema(items, propertyName, depth + 1));
        return result;
    }

    private static bool ShouldIncludeOptionalProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || propertyName.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("description", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("email", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryGetSchemaExample(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("example", out var example))
        {
            node = JsonNode.Parse(example.GetRawText());
            return true;
        }

        if (schema.TryGetProperty("examples", out var examples) && examples.ValueKind != JsonValueKind.Null)
        {
            var fromExamples = TryBuildFromExamples(examples.GetRawText());
            if (fromExamples != null)
            {
                node = fromExamples;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static bool TryGetSchemaDefault(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("default", out var defaultValue))
        {
            node = JsonNode.Parse(defaultValue.GetRawText());
            return true;
        }

        node = null;
        return false;
    }

    private static bool TryGetEnumValue(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("enum", out var enumValues) &&
            enumValues.ValueKind == JsonValueKind.Array &&
            enumValues.GetArrayLength() > 0)
        {
            node = JsonNode.Parse(enumValues[0].GetRawText());
            return true;
        }

        node = null;
        return false;
    }

    private static string GetSchemaType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString()?.Trim().ToLowerInvariant();
        }

        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            return "object";
        }

        if (schema.TryGetProperty("items", out _))
        {
            return "array";
        }

        return "string";
    }

    private static decimal BuildNumericValue(JsonElement schema, bool integerOnly)
    {
        decimal? minimum = null;
        decimal? maximum = null;

        if (schema.TryGetProperty("minimum", out var minimumElement) && minimumElement.TryGetDecimal(out var minimumValue))
        {
            minimum = minimumValue;
        }

        if (schema.TryGetProperty("maximum", out var maximumElement) && maximumElement.TryGetDecimal(out var maximumValue))
        {
            maximum = maximumValue;
        }

        var value = minimum.HasValue
            ? Math.Max(minimum.Value, integerOnly ? 1 : 0.5m)
            : integerOnly ? 1m : 1.5m;

        if (maximum.HasValue)
        {
            value = Math.Min(value, maximum.Value);
        }

        return integerOnly ? decimal.Truncate(value) : value;
    }

    private static string BuildStringValue(string propertyName, JsonElement schema)
    {
        var format = schema.TryGetProperty("format", out var formatElement) && formatElement.ValueKind == JsonValueKind.String
            ? formatElement.GetString()
            : null;

        int? minLength = null;
        if (schema.TryGetProperty("minLength", out var minLengthElement) && minLengthElement.TryGetInt32(out var parsedMinLength))
        {
            minLength = parsedMinLength;
        }

        int? maxLength = null;
        if (schema.TryGetProperty("maxLength", out var maxLengthElement) && maxLengthElement.TryGetInt32(out var parsedMaxLength))
        {
            maxLength = parsedMaxLength;
        }

        return BuildHeuristicStringValue(propertyName, "string", format, minLength, maxLength);
    }

    private static string BuildHeuristicStringValue(
        string propertyName,
        string dataType,
        string format,
        int? minLength,
        int? maxLength)
    {
        var name = propertyName ?? string.Empty;
        var normalizedFormat = format?.Trim().ToLowerInvariant();

        string value = normalizedFormat switch
        {
            "email" => "testuser@example.com",
            "uuid" => "00000000-0000-0000-0000-000000000001",
            "date" => "2024-01-01",
            "date-time" => "2024-01-01T00:00:00Z",
            "uri" => "https://example.com/resource",
            _ => null,
        };

        value ??= name.ToLowerInvariant() switch
        {
            var candidate when candidate.Contains("password") => "Test123!",
            var candidate when candidate.Contains("email") => "testuser@example.com",
            var candidate when candidate.Contains("username") => "testuser",
            var candidate when candidate.Contains("phone") => "+12025550123",
            var candidate when candidate.Contains("price") || candidate.Contains("amount") || candidate.Contains("cost") => "9.99",
            var candidate when candidate.Contains("quantity") || candidate.Contains("stock") || candidate.Contains("count") => "1",
            var candidate when candidate.EndsWith("id") => "1",
            var candidate when candidate.Contains("name") => "Sample Name",
            var candidate when candidate.Contains("title") => "Sample Title",
            var candidate when candidate.Contains("description") => "Sample description",
            _ => "sample-value",
        };

        if (minLength.HasValue && value.Length < minLength.Value)
        {
            value = value.PadRight(minLength.Value, 'x');
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            value = value[..maxLength.Value];
        }

        return value;
    }

    private static JsonNode TryBuildFromExamples(string examplesJson)
    {
        if (string.IsNullOrWhiteSpace(examplesJson))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(examplesJson);
            if (node == null)
            {
                return null;
            }

            if (node is JsonObject obj && TryExtractNamedExampleValue(obj, out var exampleValue))
            {
                return exampleValue;
            }

            return node;
        }
        catch
        {
            return JsonValue.Create(examplesJson.Trim('"'));
        }
    }

    private static bool TryExtractNamedExampleValue(JsonObject examplesObject, out JsonNode exampleValue)
    {
        foreach (var property in examplesObject)
        {
            if (property.Value is JsonObject exampleObject &&
                exampleObject.TryGetPropertyValue("value", out var nestedValue) &&
                nestedValue != null)
            {
                exampleValue = nestedValue.DeepClone();
                return true;
            }

            if (property.Value != null)
            {
                exampleValue = property.Value.DeepClone();
                return true;
            }
        }

        exampleValue = null;
        return false;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static List<int> ParseExpectedStatuses(string expectedStatus)
    {
        if (string.IsNullOrWhiteSpace(expectedStatus))
        {
            return new List<int>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<int>>(expectedStatus);
            if (parsed != null && parsed.Count > 0)
            {
                return parsed;
            }
        }
        catch
        {
            // ignore malformed expectation format; caller falls back to single-value parsing.
        }

        var trimmed = expectedStatus.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
            trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('[', ']', ' ');
        }

        return int.TryParse(trimmed, out var single)
            ? new List<int> { single }
            : new List<int>();
    }
}
