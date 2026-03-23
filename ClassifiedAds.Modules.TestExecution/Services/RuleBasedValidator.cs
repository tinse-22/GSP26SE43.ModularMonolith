using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class RuleBasedValidator : IRuleBasedValidator
{
    private readonly ILogger<RuleBasedValidator> _logger;

    public RuleBasedValidator(ILogger<RuleBasedValidator> logger)
    {
        _logger = logger;
    }

    public TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null)
    {
        var result = new TestCaseValidationResult
        {
            IsPassed = true,
            StatusCodeMatched = true,
        };

        // Handle transport errors
        if (!string.IsNullOrEmpty(response.TransportError))
        {
            result.IsPassed = false;
            result.StatusCodeMatched = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "HTTP_REQUEST_ERROR",
                Message = response.TransportError,
            });
            return result;
        }

        var expectation = testCase.Expectation;
        if (expectation == null)
        {
            return result;
        }

        // 1. Status code check
        ValidateStatusCode(response, expectation, result);

        // 2. Response schema validation
        ValidateResponseSchema(response, expectation, endpointMetadata, result);

        // 3. Header exact-match validation
        ValidateHeaders(response, expectation, result);

        // 4. Body contains
        ValidateBodyContains(response, expectation, result);

        // 5. Body not contains
        ValidateBodyNotContains(response, expectation, result);

        // 6. JSONPath equality checks
        ValidateJsonPathChecks(response, expectation, result);

        // 7. Max response time
        ValidateResponseTime(response, expectation, result);

        result.IsPassed = result.Failures.Count == 0;
        return result;
    }

    private static void ValidateStatusCode(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrEmpty(expectation.ExpectedStatus))
        {
            return;
        }

        try
        {
            var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
            if (expectedStatuses != null && expectedStatuses.Count > 0 && response.StatusCode.HasValue)
            {
                if (!expectedStatuses.Contains(response.StatusCode.Value))
                {
                    result.StatusCodeMatched = false;
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "STATUS_CODE_MISMATCH",
                        Message = $"Mã trạng thái không khớp. Mong đợi: [{string.Join(", ", expectedStatuses)}], thực tế: {response.StatusCode}.",
                        Expected = string.Join(", ", expectedStatuses),
                        Actual = response.StatusCode.Value.ToString(),
                    });
                }
            }
        }
        catch (JsonException)
        {
            // Try single integer
            if (int.TryParse(expectation.ExpectedStatus.Trim('[', ']', ' '), out var singleStatus))
            {
                if (response.StatusCode != singleStatus)
                {
                    result.StatusCodeMatched = false;
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "STATUS_CODE_MISMATCH",
                        Message = $"Mã trạng thái không khớp. Mong đợi: {singleStatus}, thực tế: {response.StatusCode}.",
                        Expected = singleStatus.ToString(),
                        Actual = response.StatusCode?.ToString(),
                    });
                }
            }
        }
    }

    private void ValidateResponseSchema(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ApiEndpointMetadataDto endpointMetadata,
        TestCaseValidationResult result)
    {
        // Determine which schema to use
        var schemaJson = expectation.ResponseSchema;

        // Fallback to endpoint metadata schema if expectation schema is empty
        if (string.IsNullOrWhiteSpace(schemaJson) && endpointMetadata?.ResponseSchemaPayloads != null)
        {
            schemaJson = endpointMetadata.ResponseSchemaPayloads.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return;
        }

        if (string.IsNullOrEmpty(response.Body))
        {
            result.SchemaMatched = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body trống nhưng có schema validation.",
            });
            return;
        }

        try
        {
            using var responseDoc = JsonDocument.Parse(response.Body);
            var normalizedSchemaJson = NormalizeSchemaForEvaluation(schemaJson);
            var schema = Json.Schema.JsonSchema.FromText(normalizedSchemaJson);
            var evaluation = schema.Evaluate(responseDoc.RootElement);
            var isValid = evaluation.IsValid;
            result.SchemaMatched = isValid;

            if (!isValid)
            {
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "RESPONSE_SCHEMA_MISMATCH",
                    Message = "Response body không phù hợp với JSON schema mong đợi.",
                });
            }
        }
        catch (JsonException)
        {
            result.SchemaMatched = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body không phải JSON hợp lệ.",
            });
        }
    }

    private static string NormalizeSchemaForEvaluation(string schemaJson)
    {
        var schemaNode = JsonNode.Parse(schemaJson);
        if (schemaNode == null)
        {
            return schemaJson;
        }

        NormalizeNullableKeywords(schemaNode);
        return schemaNode.ToJsonString();
    }

    private static void NormalizeNullableKeywords(JsonNode node)
    {
        switch (node)
        {
            case JsonObject schemaObject:
                foreach (var property in schemaObject.ToList())
                {
                    if (property.Value != null)
                    {
                        NormalizeNullableKeywords(property.Value);
                    }
                }

                ApplyNullableKeyword(schemaObject);
                break;

            case JsonArray schemaArray:
                foreach (var item in schemaArray)
                {
                    if (item != null)
                    {
                        NormalizeNullableKeywords(item);
                    }
                }

                break;
        }
    }

    private static void ApplyNullableKeyword(JsonObject schemaObject)
    {
        if (!schemaObject.TryGetPropertyValue("nullable", out var nullableNode)
            || nullableNode is not JsonValue nullableValue
            || !nullableValue.TryGetValue<bool>(out var isNullable)
            || !isNullable)
        {
            return;
        }

        schemaObject.Remove("nullable");
        var enumUpdated = TryAddNullToEnum(schemaObject);

        if (TryAddNullToType(schemaObject) || enumUpdated)
        {
            return;
        }

        // OpenAPI 3.0 uses nullable=true; convert unknown shapes to JSON Schema union form.
        var currentSchema = schemaObject.DeepClone();
        schemaObject.Clear();

        var anyOf = new JsonArray
        {
            currentSchema,
            new JsonObject
            {
                ["type"] = "null",
            },
        };

        schemaObject["anyOf"] = anyOf;
    }

    private static bool TryAddNullToType(JsonObject schemaObject)
    {
        if (!schemaObject.TryGetPropertyValue("type", out var typeNode) || typeNode == null)
        {
            return false;
        }

        switch (typeNode)
        {
            case JsonValue typeValue when typeValue.TryGetValue<string>(out var typeName):
                if (string.Equals(typeName, "null", StringComparison.Ordinal))
                {
                    return true;
                }

                schemaObject["type"] = new JsonArray
                {
                    typeName,
                    "null",
                };
                return true;

            case JsonArray typeArray:
                if (!typeArray.Any(item => string.Equals(item?.GetValue<string>(), "null", StringComparison.Ordinal)))
                {
                    typeArray.Add("null");
                }

                return true;

            default:
                return false;
        }
    }

    private static bool TryAddNullToEnum(JsonObject schemaObject)
    {
        if (!schemaObject.TryGetPropertyValue("enum", out var enumNode) || enumNode is not JsonArray enumArray)
        {
            return false;
        }

        if (!enumArray.Any(item => item == null))
        {
            enumArray.Add((JsonNode)null);
        }

        return true;
    }

    private static void ValidateHeaders(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrEmpty(expectation.HeaderChecks))
        {
            return;
        }

        Dictionary<string, string> headerChecks;
        try
        {
            headerChecks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.HeaderChecks);
        }
        catch
        {
            return;
        }

        if (headerChecks == null || headerChecks.Count == 0)
        {
            return;
        }

        var allPassed = true;
        foreach (var check in headerChecks)
        {
            var found = false;
            foreach (var h in response.Headers)
            {
                if (h.Key.Equals(check.Key, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    if (h.Value?.Trim() != check.Value?.Trim())
                    {
                        allPassed = false;
                        result.Failures.Add(new ValidationFailureModel
                        {
                            Code = "HEADER_MISMATCH",
                            Message = $"Header '{check.Key}' không khớp. Mong đợi: '{check.Value?.Trim()}', thực tế: '{h.Value?.Trim()}'.",
                            Target = check.Key,
                            Expected = check.Value?.Trim(),
                            Actual = h.Value?.Trim(),
                        });
                    }

                    break;
                }
            }

            if (!found)
            {
                allPassed = false;
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "HEADER_MISMATCH",
                    Message = $"Header '{check.Key}' không tồn tại trong response.",
                    Target = check.Key,
                    Expected = check.Value,
                });
            }
        }

        result.HeaderChecksPassed = allPassed;
    }

    private static void ValidateBodyContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrEmpty(expectation.BodyContains))
        {
            return;
        }

        List<string> patterns;
        try
        {
            patterns = JsonSerializer.Deserialize<List<string>>(expectation.BodyContains);
        }
        catch
        {
            return;
        }

        if (patterns == null || patterns.Count == 0)
        {
            return;
        }

        var body = response.Body ?? string.Empty;
        var allPassed = true;

        foreach (var pattern in patterns)
        {
            if (!body.Contains(pattern, StringComparison.Ordinal))
            {
                allPassed = false;
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "BODY_CONTAINS_MISSING",
                    Message = $"Response body không chứa chuỗi mong đợi: '{Truncate(pattern, 100)}'.",
                    Expected = Truncate(pattern, 200),
                });
            }
        }

        result.BodyContainsPassed = allPassed;
    }

    private static void ValidateBodyNotContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrEmpty(expectation.BodyNotContains))
        {
            return;
        }

        List<string> patterns;
        try
        {
            patterns = JsonSerializer.Deserialize<List<string>>(expectation.BodyNotContains);
        }
        catch
        {
            return;
        }

        if (patterns == null || patterns.Count == 0)
        {
            return;
        }

        var body = response.Body ?? string.Empty;
        var allPassed = true;

        foreach (var pattern in patterns)
        {
            if (body.Contains(pattern, StringComparison.Ordinal))
            {
                allPassed = false;
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "BODY_NOT_CONTAINS_PRESENT",
                    Message = $"Response body chứa chuỗi không mong đợi: '{Truncate(pattern, 100)}'.",
                    Actual = Truncate(pattern, 200),
                });
            }
        }

        result.BodyNotContainsPassed = allPassed;
    }

    private static void ValidateJsonPathChecks(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrEmpty(expectation.JsonPathChecks))
        {
            return;
        }

        Dictionary<string, string> checks;
        try
        {
            checks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.JsonPathChecks);
        }
        catch
        {
            return;
        }

        if (checks == null || checks.Count == 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(response.Body))
        {
            result.JsonPathChecksPassed = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body trống nhưng có JSONPath checks.",
            });
            return;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(response.Body);
        }
        catch
        {
            result.JsonPathChecksPassed = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body không phải JSON hợp lệ cho JSONPath checks.",
            });
            return;
        }

        var allPassed = true;
        using (doc)
        {
            foreach (var check in checks)
            {
                var element = VariableExtractor.NavigateJsonPath(doc.RootElement, check.Key);
                var actualValue = element?.ToString();

                if (actualValue == null || !ValuesEqual(actualValue, check.Value))
                {
                    allPassed = false;
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "JSONPATH_ASSERTION_FAILED",
                        Message = $"JSONPath '{check.Key}' không khớp. Mong đợi: '{check.Value}', thực tế: '{actualValue ?? "(null)"}'.",
                        Target = check.Key,
                        Expected = check.Value,
                        Actual = actualValue,
                    });
                }
            }
        }

        result.JsonPathChecksPassed = allPassed;
    }

    private static void ValidateResponseTime(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (!expectation.MaxResponseTime.HasValue)
        {
            return;
        }

        if (response.LatencyMs > expectation.MaxResponseTime.Value)
        {
            result.ResponseTimePassed = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_TIME_EXCEEDED",
                Message = $"Thời gian phản hồi vượt ngưỡng. Tối đa: {expectation.MaxResponseTime}ms, thực tế: {response.LatencyMs}ms.",
                Expected = $"{expectation.MaxResponseTime}ms",
                Actual = $"{response.LatencyMs}ms",
            });
        }
        else
        {
            result.ResponseTimePassed = true;
        }
    }

    private static bool ValuesEqual(string actual, string expected)
    {
        if (actual == expected)
        {
            return true;
        }

        // Try numeric comparison
        if (decimal.TryParse(actual, out var actualNum) && decimal.TryParse(expected, out var expectedNum))
        {
            return actualNum == expectedNum;
        }

        // Try boolean comparison
        if (bool.TryParse(actual, out var actualBool) && bool.TryParse(expected, out var expectedBool))
        {
            return actualBool == expectedBool;
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
