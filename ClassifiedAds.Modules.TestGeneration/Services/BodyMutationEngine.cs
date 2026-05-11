using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Rule-based body mutation engine for boundary/negative test case generation.
/// Generates mutation variants: missing required fields, type mismatches, overflow,
/// empty body, malformed JSON, invalid enum values.
/// </summary>
public class BodyMutationEngine : IBodyMutationEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public IReadOnlyList<BodyMutation> GenerateMutations(BodyMutationContext context)
    {
        // Only applies to POST/PUT/PATCH. For GET/DELETE/HEAD/OPTIONS → return empty list.
        var method = (context.HttpMethod ?? string.Empty).ToUpperInvariant();
        if (method is not ("POST" or "PUT" or "PATCH"))
        {
            return Array.Empty<BodyMutation>();
        }

        var mutations = new List<BodyMutation>();

        // Whole-body mutations
        AddEmptyBodyMutations(mutations, context);
        AddMalformedJsonMutations(mutations, context);

        // Per-field mutations from parameters
        if (context.BodyParameters != null && context.BodyParameters.Count > 0)
        {
            var baseBody = BuildBaseBody(context.BodyParameters);
            AddMissingRequiredFieldMutations(mutations, context, baseBody);
            AddTypeMismatchMutations(mutations, context, baseBody);
            AddOverflowMutations(mutations, context, baseBody);
            AddInvalidEnumMutations(mutations, context, baseBody);
        }

        // Schema-based mutations if raw schema is available
        if (!string.IsNullOrWhiteSpace(context.RequestBodySchema))
        {
            AddSchemaBasedMutations(mutations, context);
        }

        return mutations;
    }

    private static void AddEmptyBodyMutations(List<BodyMutation> mutations, BodyMutationContext context)
    {
        mutations.Add(new BodyMutation
        {
            MutationType = "emptyBody",
            Label = "empty body (null)",
            MutatedBody = null,
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("emptyBody"),
            Description = $"{context.HttpMethod} {context.Path} - gửi body null.",
            SuggestedTestType = TestType.Negative,
        });

        mutations.Add(new BodyMutation
        {
            MutationType = "emptyBody",
            Label = "empty body (empty string)",
            MutatedBody = string.Empty,
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("emptyBody"),
            Description = $"{context.HttpMethod} {context.Path} - gửi body rỗng.",
            SuggestedTestType = TestType.Negative,
        });

        mutations.Add(new BodyMutation
        {
            MutationType = "emptyBody",
            Label = "empty JSON object",
            MutatedBody = "{}",
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("emptyBody"),
            Description = $"{context.HttpMethod} {context.Path} - gửi JSON object rỗng.",
            SuggestedTestType = TestType.Negative,
        });
    }

    private static void AddMalformedJsonMutations(List<BodyMutation> mutations, BodyMutationContext context)
    {
        mutations.Add(new BodyMutation
        {
            MutationType = "malformedJson",
            Label = "malformed JSON (missing closing brace)",
            MutatedBody = "{\"field\": \"value\"",
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("malformedJson"),
            Description = $"{context.HttpMethod} {context.Path} - gửi JSON thiếu dấu đóng ngoặc.",
            SuggestedTestType = TestType.Negative,
        });

        mutations.Add(new BodyMutation
        {
            MutationType = "malformedJson",
            Label = "malformed JSON (truncated value)",
            MutatedBody = "{\"field\": }",
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("malformedJson"),
            Description = $"{context.HttpMethod} {context.Path} - gửi JSON bị cắt giá trị.",
            SuggestedTestType = TestType.Negative,
        });

        mutations.Add(new BodyMutation
        {
            MutationType = "malformedJson",
            Label = "malformed JSON (plain text)",
            MutatedBody = "this is not json",
            TargetFieldName = null,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("malformedJson"),
            Description = $"{context.HttpMethod} {context.Path} - gửi plain text thay vì JSON.",
            SuggestedTestType = TestType.Negative,
        });
    }

    private static void AddMissingRequiredFieldMutations(
        List<BodyMutation> mutations,
        BodyMutationContext context,
        Dictionary<string, object> baseBody)
    {
        var requiredFields = context.BodyParameters
            .Where(p => p.IsRequired && !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        foreach (var field in requiredFields)
        {
            var mutatedBody = new Dictionary<string, object>(baseBody);
            mutatedBody.Remove(field.Name);

            mutations.Add(new BodyMutation
            {
                MutationType = "missingRequired",
                Label = $"{field.Name} field omitted",
                MutatedBody = JsonSerializer.Serialize(mutatedBody, JsonOpts),
                TargetFieldName = field.Name,
                ExpectedStatusCode = 400,
                ExpectedStatusCodes = GetExpectedStatusesForMutation("missingRequired"),
                Description = $"{context.HttpMethod} {context.Path} - thiếu trường bắt buộc '{field.Name}'.",
                SuggestedTestType = TestType.Negative,
            });
        }
    }

    private static void AddTypeMismatchMutations(
        List<BodyMutation> mutations,
        BodyMutationContext context,
        Dictionary<string, object> baseBody)
    {
        foreach (var field in context.BodyParameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            var normalizedType = (field.DataType ?? "string").ToLowerInvariant();
            object wrongValue = normalizedType switch
            {
                "integer" or "int" or "long" or "number" or "float" or "double" or "decimal"
                    => "not_a_number",
                "boolean" or "bool"
                    => "not_a_boolean",
                "string"
                    => 12345,
                "array"
                    => "not_an_array",
                "object"
                    => "not_an_object",
                _ => 12345,
            };

            var mutatedBody = new Dictionary<string, object>(baseBody);
            mutatedBody[field.Name] = wrongValue;

            mutations.Add(new BodyMutation
            {
                MutationType = "typeMismatch",
                Label = $"{field.Name} - wrong type ({wrongValue.GetType().Name} instead of {normalizedType})",
                MutatedBody = JsonSerializer.Serialize(mutatedBody, JsonOpts),
                TargetFieldName = field.Name,
                ExpectedStatusCode = 400,
                ExpectedStatusCodes = GetExpectedStatusesForMutation("typeMismatch"),
                Description = $"{context.HttpMethod} {context.Path} - trường '{field.Name}' gửi kiểu dữ liệu sai (expected: {normalizedType}).",
                SuggestedTestType = TestType.Negative,
            });
        }
    }

    private static void AddOverflowMutations(
        List<BodyMutation> mutations,
        BodyMutationContext context,
        Dictionary<string, object> baseBody)
    {
        foreach (var field in context.BodyParameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            var normalizedType = (field.DataType ?? "string").ToLowerInvariant();
            var normalizedFormat = field.Format?.ToLowerInvariant();

            switch (normalizedType)
            {
                case "integer" or "int" when normalizedFormat == "int32":
                    AddOverflowMutation(mutations, context, baseBody, field.Name,
                        2147483648L, "Int32.MaxValue + 1 (overflow int32)");
                    break;

                case "integer" or "int" or "long":
                    AddOverflowMutation(mutations, context, baseBody, field.Name,
                        long.MaxValue, "Int64.MaxValue (boundary)");
                    break;

                case "number" or "float" or "double" or "decimal":
                    AddOverflowMutation(mutations, context, baseBody, field.Name,
                        999999999999.999, "very large number (overflow)");
                    break;

                case "string":
                    var longString = new string('a', 10000);
                    AddOverflowMutation(mutations, context, baseBody, field.Name,
                        longString, "10000-char string (overflow)");
                    break;
            }
        }
    }

    private static void AddOverflowMutation(
        List<BodyMutation> mutations,
        BodyMutationContext context,
        Dictionary<string, object> baseBody,
        string fieldName,
        object overflowValue,
        string valueDescription)
    {
        var mutatedBody = new Dictionary<string, object>(baseBody);
        mutatedBody[fieldName] = overflowValue;

        mutations.Add(new BodyMutation
        {
            MutationType = "overflow",
            Label = $"{fieldName} - {valueDescription}",
            MutatedBody = JsonSerializer.Serialize(mutatedBody, JsonOpts),
            TargetFieldName = fieldName,
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = GetExpectedStatusesForMutation("overflow"),
            Description = $"{context.HttpMethod} {context.Path} - trường '{fieldName}' vượt giới hạn: {valueDescription}.",
            SuggestedTestType = TestType.Boundary,
        });
    }

    private static void AddInvalidEnumMutations(
        List<BodyMutation> mutations,
        BodyMutationContext context,
        Dictionary<string, object> baseBody)
    {
        foreach (var field in context.BodyParameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            var enumValues = ExtractEnumValues(field.Schema);
            if (enumValues == null || enumValues.Count == 0)
            {
                continue;
            }

            var invalidValue = "INVALID_ENUM_VALUE_" + Guid.NewGuid().ToString("N")[..8];

            var mutatedBody = new Dictionary<string, object>(baseBody);
            mutatedBody[field.Name] = invalidValue;

            mutations.Add(new BodyMutation
            {
                MutationType = "invalidEnum",
                Label = $"{field.Name} - invalid enum value",
                MutatedBody = JsonSerializer.Serialize(mutatedBody, JsonOpts),
                TargetFieldName = field.Name,
                ExpectedStatusCode = 400,
                ExpectedStatusCodes = GetExpectedStatusesForMutation("invalidEnum"),
                Description = $"{context.HttpMethod} {context.Path} - trường '{field.Name}' gửi giá trị ngoài enum " +
                    $"(valid: [{string.Join(", ", enumValues)}]).",
                SuggestedTestType = TestType.Negative,
            });
        }
    }

    private static void AddSchemaBasedMutations(List<BodyMutation> mutations, BodyMutationContext context)
    {
        try
        {
            using var doc = JsonDocument.Parse(context.RequestBodySchema);
            var root = doc.RootElement;

            if (!root.TryGetProperty("properties", out var properties))
            {
                return;
            }

            var requiredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredArray.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        requiredFields.Add(item.GetString());
                    }
                }
            }

            // Check if there are schema-only fields not covered by BodyParameters
            var existingFieldNames = new HashSet<string>(
                (context.BodyParameters ?? Array.Empty<ParameterDetailDto>())
                    .Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var prop in properties.EnumerateObject())
            {
                if (existingFieldNames.Contains(prop.Name))
                {
                    continue; // Already handled by per-field mutations
                }

                if (requiredFields.Contains(prop.Name))
                {
                    // Generate missing required field mutation from schema
                    mutations.Add(new BodyMutation
                    {
                        MutationType = "missingRequired",
                        Label = $"{prop.Name} field omitted (from schema)",
                        MutatedBody = "{}",
                        TargetFieldName = prop.Name,
                        ExpectedStatusCode = 400,
                        ExpectedStatusCodes = GetExpectedStatusesForMutation("missingRequired"),
                        Description = $"{context.HttpMethod} {context.Path} - thiếu trường bắt buộc '{prop.Name}' (từ JSON schema).",
                        SuggestedTestType = TestType.Negative,
                    });
                }
            }
        }
        catch (JsonException)
        {
            // Schema is not valid JSON; skip schema-based mutations
        }
    }

    private static List<int> GetExpectedStatusesForMutation(string mutationType)
    {
        return mutationType switch
        {
            "emptyBody" => new List<int> { 400, 415, 422 },
            "malformedJson" => new List<int> { 400 },
            "missingRequired" or "typeMismatch" or "overflow" or "invalidEnum"
                => new List<int> { 400, 422 },
            _ => new List<int> { 400 },
        };
    }

    private static Dictionary<string, object> BuildBaseBody(IReadOnlyList<ParameterDetailDto> bodyParameters)
    {
        var body = new Dictionary<string, object>();

        foreach (var param in bodyParameters.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
        {
            body[param.Name] = GetDefaultValue(param);
        }

        return body;
    }

    private static object GetDefaultValue(ParameterDetailDto param)
    {
        var normalizedType = (param.DataType ?? "string").ToLowerInvariant();
        var normalizedFormat = param.Format?.ToLowerInvariant();
        var normalizedName = param.Name?.ToLowerInvariant() ?? string.Empty;

        if (RequiresUniqueValue(normalizedName, normalizedFormat))
        {
            return BuildUniquePlaceholderValue(normalizedName, normalizedFormat);
        }

        if (!string.IsNullOrWhiteSpace(param.DefaultValue))
        {
            return TryParseJsonValue(param.DefaultValue, param.DataType);
        }

        if (!string.IsNullOrWhiteSpace(param.Examples))
        {
            return TryParseJsonValue(param.Examples, param.DataType);
        }

        if (normalizedType is "integer" or "int" or "long")
        {
            return 1;
        }

        if (normalizedType is "number" or "float" or "double" or "decimal")
        {
            return 1.0;
        }

        if (normalizedType is "boolean" or "bool")
        {
            return true;
        }

        if (normalizedType == "array")
        {
            return Array.Empty<object>();
        }

        if (normalizedType == "object")
        {
            return new Dictionary<string, object>();
        }

        return "sample_value";
    }

    /// <summary>
    /// Returns true for field names/formats that must carry a unique value per test case
    /// (email, username, phone, code, slug, etc.). Mirrors the LLM prompt rule 5.
    /// </summary>
    private static bool RequiresUniqueValue(string normalizedName, string normalizedFormat)
    {
        if (normalizedFormat == "email")
        {
            return true;
        }

        // Heuristic: any field whose name strongly suggests a unique identifier
        return normalizedName switch
        {
            var n when n.Contains("email") => true,
            var n when n.Contains("username") => true,
            var n when n.Contains("phone") => true,
            var n when n.Contains("code") => true,
            var n when n.Contains("slug") => true,
            _ => false,
        };
    }

    /// <summary>
    /// Builds a {{tcUniqueId}}-based placeholder so the test execution runtime resolves
    /// it to a unique 8-char hex string per test case.
    /// </summary>
    private static string BuildUniquePlaceholderValue(string normalizedName, string normalizedFormat)
    {
        if (normalizedFormat == "email" || normalizedName.Contains("email"))
        {
            return $"testuser_{{{{tcUniqueId}}}}@example.com";
        }

        if (normalizedName.Contains("username"))
        {
            return $"user_{{{{tcUniqueId}}}}";
        }

        if (normalizedName.Contains("phone"))
        {
            return $"+1202555{{{{tcUniqueId}}}}";
        }

        // Generic unique placeholder for code/slug etc.
        return $"UNIQ_{{{{tcUniqueId}}}}";
    }

    private static object TryParseJsonValue(string value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            var element = doc.RootElement;

            return ConvertJsonElement(element);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

    private static List<string> ExtractEnumValues(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(schema);
            if (!doc.RootElement.TryGetProperty("enum", out var enumArray) ||
                enumArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return enumArray.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
