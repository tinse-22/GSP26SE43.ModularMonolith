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
    private const int TotalValidationChecks = 7;
    private const string InvalidExpectationFormatCode = "INVALID_EXPECTATION_FORMAT";

    private readonly ILogger<RuleBasedValidator> _logger;

    public RuleBasedValidator(ILogger<RuleBasedValidator> logger)
    {
        _logger = logger;
    }

    public TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null,
        bool strictMode = false)
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
            result.ChecksSkipped = TotalValidationChecks;

            if (strictMode)
            {
                result.IsPassed = false;
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "NO_EXPECTATION",
                    Message = "Strict mode: Test case phải có expectation được định nghĩa.",
                    Target = "Expectation",
                });

                _logger.LogWarning(
                    "Strict validation failed for test case {TestCaseId} because expectation is missing.",
                    testCase.TestCaseId);

                return result;
            }

            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "NO_EXPECTATION_DEFINED",
                Message = "Test case không có expectation được định nghĩa. Kết quả có thể thiếu độ tin cậy.",
            });

            _logger.LogWarning(
                "Test case {TestCaseId} has no expectation defined. Validation checks were skipped.",
                testCase.TestCaseId);

            return result;
        }

        var checksPerformed = 0;
        var checksSkipped = 0;

        // 1. Status code check
        TrackCheck(ValidateStatusCode(response, testCase, expectation, result), ref checksPerformed, ref checksSkipped);

        // 2. Response schema validation
        TrackCheck(ValidateResponseSchema(response, expectation, endpointMetadata, result), ref checksPerformed, ref checksSkipped);

        // 3. Header exact-match validation
        TrackCheck(ValidateHeaders(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 4. Body contains
        TrackCheck(ValidateBodyContains(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 5. Body not contains
        TrackCheck(ValidateBodyNotContains(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 6. JSONPath equality checks
        TrackCheck(ValidateJsonPathChecks(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 7. Max response time
        TrackCheck(ValidateResponseTime(response, expectation, result), ref checksPerformed, ref checksSkipped);

        result.ChecksPerformed = checksPerformed;
        result.ChecksSkipped = checksSkipped;

        if (checksPerformed == 0 && checksSkipped > 0)
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "ALL_CHECKS_SKIPPED",
                Message = $"Tất cả {checksSkipped} validation checks bị bỏ qua do expectation trống hoặc không hợp lệ.",
            });

            _logger.LogWarning(
                "Test case {TestCaseId}: all validation checks were skipped.",
                testCase.TestCaseId);
        }

        result.IsPassed = result.Failures.Count == 0;
        return result;
    }

    private static bool ValidateStatusCode(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(expectation.ExpectedStatus))
        {
            return false;
        }

        try
        {
            var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
            if (expectedStatuses == null || expectedStatuses.Count == 0)
            {
                result.StatusCodeMatched = false;
                AddInvalidExpectationFailure(
                    result,
                    "ExpectedStatus",
                    expectation.ExpectedStatus,
                    "Mong đợi JSON array số nguyên, ví dụ [200, 201].");
                return true;
            }

            if (!response.StatusCode.HasValue || !expectedStatuses.Contains(response.StatusCode.Value))
            {
                if (TryApplyAdaptiveStatusMatch(response, expectedStatuses, testCase, result))
                {
                    result.StatusCodeMatched = true;
                }
                else
                {
                    result.StatusCodeMatched = false;
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "STATUS_CODE_MISMATCH",
                        Message = $"Mã trạng thái không khớp. Mong đợi: [{string.Join(", ", expectedStatuses)}], thực tế: {response.StatusCode?.ToString() ?? "(null)"}.",
                        Expected = string.Join(", ", expectedStatuses),
                        Actual = response.StatusCode?.ToString(),
                    });
                }
            }

            return true;
        }
        catch (JsonException)
        {
            // Try single integer
            if (int.TryParse(expectation.ExpectedStatus.Trim('[', ']', ' '), out var singleStatus))
            {
                if (response.StatusCode != singleStatus)
                {
                    var singleExpectedList = new List<int> { singleStatus };
                    if (TryApplyAdaptiveStatusMatch(response, singleExpectedList, testCase, result))
                    {
                        result.StatusCodeMatched = true;
                    }
                    else
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

                return true;
            }

            result.StatusCodeMatched = false;
            AddInvalidExpectationFailure(
                result,
                "ExpectedStatus",
                expectation.ExpectedStatus,
                "Mong đợi JSON array số nguyên, ví dụ [200, 201].");
            return true;
        }
    }

    private static bool ValidateResponseSchema(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ApiEndpointMetadataDto endpointMetadata,
        TestCaseValidationResult result)
    {
        // Determine which schema to use
        var schemaJson = expectation.ResponseSchema;
        var isFromFallback = false;

        // If expectation has no explicit schema, check if we should skip schema validation
        // for non-2xx expected statuses (error responses don't match success schemas)
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            if (IsExpectingNon2xxStatus(expectation))
            {
                // Skip schema validation for error responses when no explicit schema provided
                // Success schemas (ResponseSchemaPayloads) are not appropriate for error responses
                return false;
            }

            if (IsExpectingMixed2xxAnd4xxStatus(expectation))
            {
                // Mixed expected statuses: don't apply fallback schema
                return false;
            }

            // Fallback to endpoint metadata schema only for pure 2xx expected statuses
            if (endpointMetadata?.ResponseSchemaPayloads != null)
            {
                schemaJson = endpointMetadata.ResponseSchemaPayloads.FirstOrDefault();
                isFromFallback = true;
            }
        }

        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return false;
        }

        Json.Schema.JsonSchema schema;
        try
        {
            var normalizedSchemaJson = NormalizeSchemaForEvaluation(schemaJson);
            schema = Json.Schema.JsonSchema.FromText(normalizedSchemaJson);
        }
        catch (Exception)
        {
            result.SchemaMatched = false;
            AddInvalidExpectationFailure(
                result,
                "ResponseSchema",
                schemaJson,
                "ResponseSchema không phải JSON schema hợp lệ.");
            return true;
        }

        if (string.IsNullOrEmpty(response.Body))
        {
            result.SchemaMatched = false;
            if (isFromFallback)
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "RESPONSE_NOT_JSON",
                    Message = "Response body trống nhưng có schema validation (fallback từ OpenAPI spec).",
                    Target = "ResponseSchema",
                });
            }
            else
            {
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "RESPONSE_NOT_JSON",
                    Message = "Response body trống nhưng có schema validation.",
                });
            }

            return true;
        }

        try
        {
            using var responseDoc = JsonDocument.Parse(response.Body);
            var evaluation = schema.Evaluate(responseDoc.RootElement);
            var isValid = evaluation.IsValid;
            result.SchemaMatched = isValid;

            if (!isValid)
            {
                if (isFromFallback)
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "RESPONSE_SCHEMA_MISMATCH",
                        Message = "Response body không phù hợp với JSON schema (fallback từ OpenAPI spec).",
                        Target = "ResponseSchema",
                    });
                }
                else
                {
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "RESPONSE_SCHEMA_MISMATCH",
                        Message = "Response body không phù hợp với JSON schema mong đợi.",
                    });
                }
            }

            return true;
        }
        catch (JsonException)
        {
            result.SchemaMatched = false;
            if (isFromFallback)
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "RESPONSE_NOT_JSON",
                    Message = "Response body không phải JSON hợp lệ (fallback schema từ OpenAPI spec).",
                    Target = "ResponseSchema",
                });
            }
            else
            {
                result.Failures.Add(new ValidationFailureModel
                {
                    Code = "RESPONSE_NOT_JSON",
                    Message = "Response body không phải JSON hợp lệ.",
                });
            }

            return true;
        }
        catch (Json.Schema.RefResolutionException ex)
        {
            if (isFromFallback)
            {
                // Fallback schema from OpenAPI spec has unresolvable $ref — treat as warning, not failure
                result.SchemaMatched = null;
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "SCHEMA_REF_UNRESOLVABLE",
                    Message = $"Không thể resolve $ref trong JSON schema (fallback từ OpenAPI spec): {ex.Message}",
                    Target = "ResponseSchema",
                });
                return true;
            }

            result.SchemaMatched = null;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "SCHEMA_REF_UNRESOLVABLE",
                Message = $"Không thể resolve $ref trong JSON schema: {ex.Message}",
            });
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (isFromFallback)
            {
                result.SchemaMatched = null;
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "SCHEMA_VALIDATION_ERROR",
                    Message = $"Lỗi khi validate response schema (fallback từ OpenAPI spec): {ex.Message}",
                    Target = "ResponseSchema",
                });
                return true;
            }

            result.SchemaMatched = null;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "SCHEMA_VALIDATION_ERROR",
                Message = $"Lỗi khi validate response schema: {ex.Message}",
            });
            return true;
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

    private static bool ValidateHeaders(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(expectation.HeaderChecks))
        {
            return false;
        }

        Dictionary<string, string> headerChecks;
        try
        {
            headerChecks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.HeaderChecks);
        }
        catch (JsonException)
        {
            result.HeaderChecksPassed = false;
            AddInvalidExpectationFailure(
                result,
                "HeaderChecks",
                expectation.HeaderChecks,
                "Mong đợi JSON object dạng {\"Header-Name\": \"expected-value\"}.");
            return true;
        }

        if (headerChecks == null || headerChecks.Count == 0)
        {
            return false;
        }

        var responseHeaders = response.Headers ?? new Dictionary<string, string>();
        var allPassed = true;
        foreach (var check in headerChecks)
        {
            var found = false;
            foreach (var h in responseHeaders)
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
        return true;
    }

    private static bool ValidateBodyContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(expectation.BodyContains))
        {
            return false;
        }

        List<string> patterns;
        try
        {
            patterns = JsonSerializer.Deserialize<List<string>>(expectation.BodyContains);
        }
        catch (JsonException)
        {
            result.BodyContainsPassed = false;
            AddInvalidExpectationFailure(
                result,
                "BodyContains",
                expectation.BodyContains,
                "Mong đợi JSON array chuỗi, ví dụ [\"success\", \"completed\"].");
            return true;
        }

        var normalizedPatterns = patterns?
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToList();

        if (normalizedPatterns == null || normalizedPatterns.Count == 0)
        {
            return false;
        }

        var body = response.Body ?? string.Empty;
        var allPassed = true;

        foreach (var pattern in normalizedPatterns)
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
        return true;
    }

    private static bool ValidateBodyNotContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(expectation.BodyNotContains))
        {
            return false;
        }

        List<string> patterns;
        try
        {
            patterns = JsonSerializer.Deserialize<List<string>>(expectation.BodyNotContains);
        }
        catch (JsonException)
        {
            result.BodyNotContainsPassed = false;
            AddInvalidExpectationFailure(
                result,
                "BodyNotContains",
                expectation.BodyNotContains,
                "Mong đợi JSON array chuỗi, ví dụ [\"error\", \"forbidden\"].");
            return true;
        }

        var normalizedPatterns = patterns?
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToList();

        if (normalizedPatterns == null || normalizedPatterns.Count == 0)
        {
            return false;
        }

        var body = response.Body ?? string.Empty;
        var allPassed = true;

        foreach (var pattern in normalizedPatterns)
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
        return true;
    }

    private static bool ValidateJsonPathChecks(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(expectation.JsonPathChecks))
        {
            return false;
        }

        Dictionary<string, string> checks;
        try
        {
            checks = JsonSerializer.Deserialize<Dictionary<string, string>>(expectation.JsonPathChecks);
        }
        catch (JsonException)
        {
            result.JsonPathChecksPassed = false;
            AddInvalidExpectationFailure(
                result,
                "JsonPathChecks",
                expectation.JsonPathChecks,
                "Mong đợi JSON object dạng {\"$.path\": \"expectedValue\"}.");
            return true;
        }

        if (checks == null || checks.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(response.Body))
        {
            result.JsonPathChecksPassed = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body trống nhưng có JSONPath checks.",
            });
            return true;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(response.Body);
        }
        catch (JsonException)
        {
            result.JsonPathChecksPassed = false;
            result.Failures.Add(new ValidationFailureModel
            {
                Code = "RESPONSE_NOT_JSON",
                Message = "Response body không phải JSON hợp lệ cho JSONPath checks.",
            });
            return true;
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
        return true;
    }

    private static bool ValidateResponseTime(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        TestCaseValidationResult result)
    {
        if (!expectation.MaxResponseTime.HasValue)
        {
            return false;
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

        return true;
    }

    private static void TrackCheck(bool checkPerformed, ref int checksPerformed, ref int checksSkipped)
    {
        if (checkPerformed)
        {
            checksPerformed++;
            return;
        }

        checksSkipped++;
    }

    private static void AddInvalidExpectationFailure(
        TestCaseValidationResult result,
        string target,
        string actualValue,
        string details)
    {
        result.Failures.Add(new ValidationFailureModel
        {
            Code = InvalidExpectationFormatCode,
            Message = $"{target} không đúng định dạng mong đợi. {details}",
            Target = target,
            Actual = Truncate(actualValue, 500),
        });
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

    /// <summary>
    /// Determines if the expectation is for non-2xx (error) status codes.
    /// Returns true if ALL expected statuses are outside the 200-299 range.
    /// </summary>
    private static bool IsExpectingNon2xxStatus(ExecutionTestCaseExpectationDto expectation)
    {
        if (string.IsNullOrWhiteSpace(expectation?.ExpectedStatus))
        {
            return false;
        }

        try
        {
            var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
            if (expectedStatuses == null || expectedStatuses.Count == 0)
            {
                return false;
            }

            // All statuses must be non-2xx for this to return true
            return expectedStatuses.All(status => status < 200 || status >= 300);
        }
        catch (JsonException)
        {
            // Try single integer
            if (int.TryParse(expectation.ExpectedStatus.Trim('[', ']', ' '), out var singleStatus))
            {
                return singleStatus < 200 || singleStatus >= 300;
            }

            return false;
        }
    }

    private static bool IsExpectingMixed2xxAnd4xxStatus(ExecutionTestCaseExpectationDto expectation)
    {
        if (string.IsNullOrWhiteSpace(expectation?.ExpectedStatus))
        {
            return false;
        }

        try
        {
            var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
            if (expectedStatuses == null || expectedStatuses.Count < 2)
            {
                return false;
            }

            // Check if list contains both 2xx and 4xx
            bool has2xx = expectedStatuses.Any(status => status >= 200 && status < 300);
            bool has4xx = expectedStatuses.Any(status => status >= 400 && status < 500);
            return has2xx && has4xx;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ShouldTreatAsAdaptiveErrorMatch(HttpTestResponse response, IReadOnlyCollection<int> expectedStatuses)
    {
        if (!response.StatusCode.HasValue || response.StatusCode.Value != 200)
        {
            return false;
        }

        if (expectedStatuses == null || expectedStatuses.Count == 0)
        {
            return false;
        }

        // Adaptive mode only for scenarios that are explicitly expecting non-2xx error outcomes.
        if (!expectedStatuses.All(status => status < 200 || status >= 300))
        {
            return false;
        }

        return LooksLikeErrorPayload(response.Body);
    }

    private static bool TryApplyAdaptiveStatusMatch(
        HttpTestResponse response,
        IReadOnlyCollection<int> expectedStatuses,
        ExecutionTestCaseDto testCase,
        TestCaseValidationResult result)
    {
        if (!response.StatusCode.HasValue || expectedStatuses == null || expectedStatuses.Count == 0)
        {
            return false;
        }

        var actualStatus = response.StatusCode.Value;

        if (ShouldTreatAsAdaptiveErrorMatch(response, expectedStatuses))
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "ADAPTIVE_ERROR_PAYLOAD_MATCH",
                Message = $"Status thực tế {actualStatus} không nằm trong kỳ vọng [{string.Join(", ", expectedStatuses)}], nhưng response body thể hiện lỗi hợp lệ nên được chấp nhận theo chế độ adaptive.",
                Target = "ExpectedStatus",
            });

            return true;
        }

        if (IsAdaptiveSuccessStatusMatch(actualStatus, expectedStatuses, testCase))
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "ADAPTIVE_SUCCESS_STATUS_MATCH",
                Message = $"Status thực tế {actualStatus} không nằm trong kỳ vọng [{string.Join(", ", expectedStatuses)}], nhưng được chấp nhận theo tương thích success-status của HTTP method {NormalizeHttpMethod(testCase?.Request?.HttpMethod)}.",
                Target = "ExpectedStatus",
            });

            return true;
        }

        if (IsAdaptiveClientErrorMatch(actualStatus, expectedStatuses, testCase))
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "ADAPTIVE_CLIENT_ERROR_STATUS_MATCH",
                Message = $"Status thực tế {actualStatus} không nằm trong kỳ vọng [{string.Join(", ", expectedStatuses)}], nhưng được chấp nhận vì test case {(testCase?.TestType ?? "(unknown)")} cho phép nhóm client-error 4xx.",
                Target = "ExpectedStatus",
            });

            return true;
        }

        return false;
    }

    private static bool IsAdaptiveSuccessStatusMatch(
        int actualStatus,
        IReadOnlyCollection<int> expectedStatuses,
        ExecutionTestCaseDto testCase)
    {
        if (actualStatus < 200 || actualStatus >= 300)
        {
            return false;
        }

        if (!expectedStatuses.Any(status => status >= 200 && status < 300))
        {
            return false;
        }

        var candidateStatuses = GetSuccessStatusCandidates(NormalizeHttpMethod(testCase?.Request?.HttpMethod));
        if (!candidateStatuses.Contains(actualStatus))
        {
            return false;
        }

        return expectedStatuses.Any(candidateStatuses.Contains);
    }

    private static bool IsAdaptiveClientErrorMatch(
        int actualStatus,
        IReadOnlyCollection<int> expectedStatuses,
        ExecutionTestCaseDto testCase)
    {
        if (actualStatus < 400 || actualStatus >= 500)
        {
            return false;
        }

        // Adaptive client error match only applies when expected list is pure 4xx
        // (no 2xx success statuses mixed in).
        // E.g., expected [401, 403] with actual 400 can match; expected [200, 400] with actual 400 cannot.
        if (expectedStatuses.Any(status => status >= 200 && status < 300))
        {
            return false; // Has 2xx, not pure client error list
        }

        if (!expectedStatuses.Any(status => status >= 400 && status < 500))
        {
            return false;
        }

        if (IsBoundaryOrNegative(testCase?.TestType))
        {
            return true;
        }

        // Some boundary/negative suites encode error expectations narrowly (e.g. [401]) while APIs return adjacent 4xx (e.g. 400).
        // Only apply if there is no 2xx in expected and case is boundary/negative.
        return false;
    }

    private static string NormalizeHttpMethod(string httpMethod)
    {
        return string.IsNullOrWhiteSpace(httpMethod)
            ? "GET"
            : httpMethod.Trim().ToUpperInvariant();
    }

    private static HashSet<int> GetSuccessStatusCandidates(string method)
    {
        return method switch
        {
            "POST" => new HashSet<int> { 200, 201, 202 },
            "PUT" => new HashSet<int> { 200, 202, 204 },
            "PATCH" => new HashSet<int> { 200, 202, 204 },
            "DELETE" => new HashSet<int> { 200, 202, 204 },
            _ => new HashSet<int> { 200 },
        };
    }

    private static bool IsBoundaryOrNegative(string testType)
    {
        if (string.IsNullOrWhiteSpace(testType))
        {
            return false;
        }

        var normalized = testType.Trim().ToLowerInvariant();
        return normalized is "boundary" or "negative";
    }

    private static bool LooksLikeErrorPayload(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("success", out var successElement) &&
                successElement.ValueKind == JsonValueKind.False)
            {
                return true;
            }

            if (root.TryGetProperty("errors", out var errorsElement) &&
                (errorsElement.ValueKind == JsonValueKind.Array || errorsElement.ValueKind == JsonValueKind.Object))
            {
                return true;
            }

            var errorLikeFields = new[] { "error", "message", "detail", "title", "code", "status" };
            foreach (var field in errorLikeFields)
            {
                if (!root.TryGetProperty(field, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return true;
                    }
                }
                else if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Fallback to lightweight text heuristics for non-JSON bodies.
            var text = body.ToLowerInvariant();
            return text.Contains("error") || text.Contains("invalid") || text.Contains("failed") || text.Contains("exception");
        }

        return false;
    }
}
