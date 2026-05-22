using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Contracts.TestGeneration.Validation;

public static class RequestSchemaPayloadValidator
{
    private static readonly Regex PlaceholderRegex = new(
        @"^\s*\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<RequestSchemaValidationIssue> Validate(
        string body,
        string bodyType,
        ApiEndpointMetadataDto endpointMetadata,
        IReadOnlyDictionary<string, string> variables = null,
        IReadOnlyCollection<int> expectedStatuses = null,
        string testType = null,
        string testName = null)
    {
        if (string.IsNullOrWhiteSpace(body) || !IsJsonBody(bodyType))
        {
            return Array.Empty<RequestSchemaValidationIssue>();
        }

        var issues = new List<RequestSchemaValidationIssue>();
        var trimmedBody = body.Trim();
        if (ContainsJavascriptExpression(trimmedBody))
        {
            issues.Add(new RequestSchemaValidationIssue
            {
                Code = "REQUEST_BODY_INVALID_JSON",
                Message = "Request body contains a JavaScript expression. JSON request bodies must contain serialized values only.",
                Target = "Request.Body",
                Expected = "Valid JSON literal values",
                Actual = body,
            });
            return issues;
        }

        JsonDocument bodyDocument;
        try
        {
            bodyDocument = JsonDocument.Parse(trimmedBody);
        }
        catch (JsonException ex)
        {
            issues.Add(new RequestSchemaValidationIssue
            {
                Code = "REQUEST_BODY_INVALID_JSON",
                Message = $"Request bodyType is JSON but body is not valid JSON: {ex.Message}",
                Target = "Request.Body",
                Expected = "Valid JSON body",
                Actual = body,
            });
            return issues;
        }

        using (bodyDocument)
        {
            var schema = ResolveRequestBodySchema(endpointMetadata);
            if (string.IsNullOrWhiteSpace(schema))
            {
                return issues;
            }

            JsonDocument schemaDocument;
            try
            {
                schemaDocument = JsonDocument.Parse(schema);
            }
            catch (JsonException)
            {
                return issues;
            }

            using (schemaDocument)
            {
                if (bodyDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return issues;
                }

                var properties = ResolveSchemaProperties(schemaDocument.RootElement);
                if (properties.Count == 0)
                {
                    return issues;
                }

                var allowWrongType = IsExplicitWrongTypeNegativeCase(expectedStatuses, testType, testName);
                foreach (var property in bodyDocument.RootElement.EnumerateObject())
                {
                    if (!properties.TryGetValue(property.Name, out var propertySchema))
                    {
                        continue;
                    }

                    ValidateProperty(property.Name, property.Value, propertySchema, variables, allowWrongType, issues);
                }
            }
        }

        return issues;
    }

    private static void ValidateProperty(
        string propertyName,
        JsonElement value,
        JsonElement propertySchema,
        IReadOnlyDictionary<string, string> variables,
        bool allowWrongType,
        List<RequestSchemaValidationIssue> issues)
    {
        var schemaType = GetSchemaType(propertySchema);
        if (string.Equals(schemaType, "number", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, "integer", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNumericProperty(propertyName, value, schemaType, variables, allowWrongType, issues);
            return;
        }

        if (IsIdentifierSemanticName(propertyName) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (TryExtractExactPlaceholder(text, out var placeholderName) &&
                IsIdentifierSemanticName(placeholderName) &&
                !IsResourceCompatiblePlaceholder(propertyName, placeholderName))
            {
                issues.Add(new RequestSchemaValidationIssue
                {
                    Code = "REQUEST_SCHEMA_TYPE_MISMATCH",
                    Message = $"Identifier field '{propertyName}' uses incompatible placeholder '{{{{{placeholderName}}}}}'.",
                    Target = $"Request.Body.{propertyName}",
                    Expected = $"Placeholder matching resource '{StripIdSuffix(propertyName)}'",
                    Actual = $"{{{{{placeholderName}}}}}",
                });
            }
        }
    }

    private static void ValidateNumericProperty(
        string propertyName,
        JsonElement value,
        string schemaType,
        IReadOnlyDictionary<string, string> variables,
        bool allowWrongType,
        List<RequestSchemaValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (string.Equals(schemaType, "integer", StringComparison.OrdinalIgnoreCase) &&
                !value.TryGetInt64(out _))
            {
                issues.Add(CreateNumericMismatch(propertyName, "JSON integer", value.GetRawText()));
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            if (!allowWrongType)
            {
                issues.Add(CreateNumericMismatch(propertyName, $"JSON {schemaType}", value.GetRawText()));
            }

            return;
        }

        var text = value.GetString();
        if (TryExtractExactPlaceholder(text, out var placeholderName))
        {
            if (IsIdentifierSemanticName(placeholderName))
            {
                issues.Add(CreateNumericMismatch(
                    propertyName,
                    $"JSON {schemaType} or numeric placeholder",
                    $"{{{{{placeholderName}}}}}"));
                return;
            }

            if (!IsNumericSemanticName(placeholderName))
            {
                if (!allowWrongType)
                {
                    issues.Add(CreateNumericMismatch(
                        propertyName,
                        $"JSON {schemaType} or numeric placeholder",
                        $"{{{{{placeholderName}}}}}"));
                }

                return;
            }

            if (variables != null &&
                variables.TryGetValue(placeholderName, out var resolvedValue) &&
                !CanParseSchemaNumber(resolvedValue, schemaType))
            {
                issues.Add(CreateNumericMismatch(
                    propertyName,
                    $"Resolved numeric value for '{{{{{placeholderName}}}}}'",
                    resolvedValue));
            }

            return;
        }

        if (!allowWrongType)
        {
            issues.Add(CreateNumericMismatch(propertyName, $"JSON {schemaType}", text));
        }
    }

    private static RequestSchemaValidationIssue CreateNumericMismatch(string propertyName, string expected, string actual)
    {
        return new RequestSchemaValidationIssue
        {
            Code = "REQUEST_SCHEMA_TYPE_MISMATCH",
            Message = $"Request body field '{propertyName}' must match the OpenAPI numeric schema.",
            Target = $"Request.Body.{propertyName}",
            Expected = expected,
            Actual = actual,
        };
    }

    private static bool IsJsonBody(string bodyType)
    {
        return string.Equals(bodyType?.Trim(), "JSON", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsJavascriptExpression(string value)
    {
        return value?.Contains(".repeat(", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolveRequestBodySchema(ApiEndpointMetadataDto endpointMetadata)
    {
        return endpointMetadata?.Parameters?
            .FirstOrDefault(parameter =>
                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Schema))
            ?.Schema
            ?? endpointMetadata?.ParameterSchemaPayloads?.FirstOrDefault();
    }

    private static Dictionary<string, JsonElement> ResolveSchemaProperties(JsonElement schema)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        AddSchemaProperties(schema, properties);
        return properties;
    }

    private static void AddSchemaProperties(JsonElement schema, Dictionary<string, JsonElement> properties)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("properties", out var directProperties) &&
            directProperties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in directProperties.EnumerateObject())
            {
                properties[property.Name] = property.Value;
            }
        }

        foreach (var compositeName in new[] { "allOf", "anyOf", "oneOf" })
        {
            if (!schema.TryGetProperty(compositeName, out var variants) ||
                variants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var variant in variants.EnumerateArray())
            {
                AddSchemaProperties(variant, properties);
            }
        }
    }

    private static string GetSchemaType(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type))
        {
            return null;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString();
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in type.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    !string.Equals(item.GetString(), "null", StringComparison.OrdinalIgnoreCase))
                {
                    return item.GetString();
                }
            }
        }

        return null;
    }

    private static bool IsExplicitWrongTypeNegativeCase(
        IReadOnlyCollection<int> expectedStatuses,
        string testType,
        string testName)
    {
        var expectsBadRequest = expectedStatuses?.Contains(400) == true;
        if (!expectsBadRequest)
        {
            return false;
        }

        return ContainsAny(testType, "negative", "boundary", "invalid", "wrong type", "type") ||
               ContainsAny(testName, "negative", "invalid type", "wrong type", "schema", "validation");
    }

    private static bool TryExtractExactPlaceholder(string value, out string placeholderName)
    {
        placeholderName = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = PlaceholderRegex.Match(value);
        if (!match.Success)
        {
            return false;
        }

        placeholderName = match.Groups[1].Value;
        return true;
    }

    private static bool CanParseSchemaNumber(string value, string schemaType)
    {
        if (string.Equals(schemaType, "integer", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsResourceCompatiblePlaceholder(string propertyName, string placeholderName)
    {
        var propertyResource = StripIdSuffix(propertyName);
        var placeholderResource = StripIdSuffix(placeholderName);

        if (string.IsNullOrWhiteSpace(propertyResource) || string.IsNullOrWhiteSpace(placeholderResource))
        {
            return true;
        }

        if (string.Equals(propertyResource, "id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(placeholderResource, "id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(propertyResource, placeholderResource, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripIdSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = value.Trim();
        if (result.EndsWith("Ids", StringComparison.OrdinalIgnoreCase))
        {
            return result[..^3];
        }

        return result.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            ? result[..^2]
            : result;
    }

    private static bool IsIdentifierSemanticName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value, "id", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumericSemanticName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        foreach (var keyword in new[]
                 {
                     "price",
                     "amount",
                     "cost",
                     "stock",
                     "quantity",
                     "qty",
                     "count",
                     "total",
                     "number",
                     "num",
                     "rate",
                     "percent",
                     "percentage",
                     "size",
                     "limit",
                     "offset",
                 })
        {
            if (normalized.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token =>
            !string.IsNullOrWhiteSpace(token) &&
            value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class RequestSchemaValidationIssue
{
    public string Code { get; set; }

    public string Message { get; set; }

    public string Target { get; set; }

    public string Expected { get; set; }

    public string Actual { get; set; }
}
