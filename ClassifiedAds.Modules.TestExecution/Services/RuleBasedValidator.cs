using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class RuleBasedValidator : IRuleBasedValidator
{
    private const int TotalValidationChecks = 7;
    private const string InvalidExpectationFormatCode = "INVALID_EXPECTATION_FORMAT";
    private static readonly Regex TcUniqueIdPlaceholderRegex = new(
        Regex.Escape("{{tcUniqueId}}"),
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Detects JWT-like values: three non-trivial base64url segments separated by dots.
    // JWTs are session-specific and always differ across runs; treat as existence checks.
    private static readonly System.Text.RegularExpressions.Regex JwtLikeRegex = new(
        @"^[A-Za-z0-9\-_]{10,}\.[A-Za-z0-9\-_]{10,}\.[A-Za-z0-9\-_]{10,}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly ILogger<RuleBasedValidator> _logger;

    public RuleBasedValidator(ILogger<RuleBasedValidator> logger)
    {
        _logger = logger;
    }

    public TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null,
        ValidationProfile profile = ValidationProfile.Default,
        IReadOnlyDictionary<string, string> variableBag = null)
    {
        var strictMode = profile != ValidationProfile.Default;
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
        TrackCheck(ValidateStatusCode(response, testCase, expectation, result, profile != ValidationProfile.SrsStrict), ref checksPerformed, ref checksSkipped);

        // 2. Response schema validation
        TrackCheck(ValidateResponseSchema(response, expectation, endpointMetadata, result, strictMode), ref checksPerformed, ref checksSkipped);

        // 3. Header exact-match validation
        TrackCheck(ValidateHeaders(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 4. Body contains
        TrackCheck(ValidateBodyContains(response, expectation, testCase, result), ref checksPerformed, ref checksSkipped);

        // 5. Body not contains
        TrackCheck(ValidateBodyNotContains(response, expectation, testCase, result), ref checksPerformed, ref checksSkipped);

        // 6. JSONPath equality checks
        TrackCheck(ValidateJsonPathChecks(response, expectation, testCase, result, variableBag), ref checksPerformed, ref checksSkipped);

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
        TestCaseValidationResult result,
        bool allowAdaptive = true)
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
                if (allowAdaptive && TryApplyAdaptiveStatusMatch(response, expectedStatuses, testCase, result))
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
                    if (allowAdaptive && TryApplyAdaptiveStatusMatch(response, singleExpectedList, testCase, result))
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
        TestCaseValidationResult result,
        bool strictMode)
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
            if (strictMode)
            {
                result.SchemaMatched = false;
                AddInvalidExpectationFailure(
                    result,
                    "ResponseSchema",
                    schemaJson,
                    "ResponseSchema không phải JSON schema hợp lệ.");
            }
            else
            {
                result.SchemaMatched = null;
                AddInvalidExpectationWarning(
                    result,
                    "ResponseSchema",
                    "ResponseSchema không phải JSON schema hợp lệ.");
            }

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
            var expectedHeaderValue = check.Value?.Trim();
            foreach (var h in responseHeaders)
            {
                if (h.Key.Equals(check.Key, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    var actualHeaderValue = h.Value?.Trim();
                    if (!HeaderValuesEqual(check.Key, expectedHeaderValue, actualHeaderValue))
                    {
                        allPassed = false;
                        result.Failures.Add(new ValidationFailureModel
                        {
                            Code = "HEADER_MISMATCH",
                            Message = $"Header '{check.Key}' không khớp. Mong đợi: '{expectedHeaderValue}', thực tế: '{actualHeaderValue}'.",
                            Target = check.Key,
                            Expected = expectedHeaderValue,
                            Actual = actualHeaderValue,
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

    private static bool HeaderValuesEqual(string headerName, string expected, string actual)
    {
        if (string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            var expectedMediaType = ExtractMediaType(expected);
            var actualMediaType = ExtractMediaType(actual);
            if (!string.IsNullOrWhiteSpace(expectedMediaType) &&
                !string.IsNullOrWhiteSpace(actualMediaType) &&
                string.Equals(expectedMediaType, actualMediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string ExtractMediaType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var semicolonIndex = contentType.IndexOf(';');
        return semicolonIndex >= 0
            ? contentType[..semicolonIndex].Trim()
            : contentType.Trim();
    }

    private static bool IsBodyContainsSoftMode(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ExecutionTestCaseDto testCase,
        TestCaseValidationResult result)
    {
        // Soft mode 1: Adaptive permissive match was applied (2xx returned for a Negative/Boundary test expecting 4xx)
        if (result.Warnings.Any(w => w.Code == "ADAPTIVE_PERMISSIVE_STATUS_MATCH"))
        {
            return true;
        }

        // Soft mode 2: Test is Negative/Boundary AND actual non-2xx status exactly matched expected statuses
        if (IsBoundaryOrNegative(testCase?.TestType))
        {
            if (response.StatusCode.HasValue && response.StatusCode.Value >= 400 &&
                !string.IsNullOrWhiteSpace(expectation.ExpectedStatus))
            {
                try
                {
                    var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
                    if (expectedStatuses != null && expectedStatuses.Contains(response.StatusCode.Value))
                    {
                        return true;
                    }
                }
                catch
                {
                    if (int.TryParse(expectation.ExpectedStatus.Trim('[', ']', ' '), out var single) &&
                        single == response.StatusCode.Value)
                    {
                        return true;
                    }
                }
            }

            // Soft mode 3: Only when the test explicitly expects an error outcome (all 4xx/5xx).
            // Success-boundary tests (e.g. expectedStatus=[201]) must still enforce body checks.
            if (!string.IsNullOrWhiteSpace(expectation.ExpectedStatus))
            {
                try
                {
                    var expectedStatuses = JsonSerializer.Deserialize<List<int>>(expectation.ExpectedStatus);
                    if (expectedStatuses != null && expectedStatuses.Count > 0 &&
                        expectedStatuses.All(s => s >= 400))
                    {
                        return true;
                    }
                }
                catch
                {
                    // ignore parse errors; fall through to hard mode
                }
            }
        }

        return false;
    }

    private static bool ValidateBodyContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ExecutionTestCaseDto testCase,
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

        // Build normalized variants of the body once so each pattern check is fast.
        // Strategy 1: raw body as-is.
        // Strategy 2: pretty-printed — normalises compact `"key":value` → `"key": value`.
        // Strategy 3: structurally compact — strips all whitespace around JSON syntax
        //              characters so `"key" : value` and `"key":value` both reduce
        //              to `"key":value` regardless of original whitespace style.
        var prettyBody = body;
        var compactBody = body;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var node = JsonNode.Parse(body);
                prettyBody = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
                compactBody = NormalizeJsonWhitespace(body);
            }
            catch
            {
                // Not valid JSON — all three variants remain identical to raw body.
            }
        }

        var allPassed = true;
        var softMode = IsBodyContainsSoftMode(response, expectation, testCase, result);

        foreach (var pattern in normalizedPatterns)
        {
            var compactPattern = NormalizeJsonWhitespace(pattern);
            var matched =
                body.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                prettyBody.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                compactBody.Contains(compactPattern, StringComparison.OrdinalIgnoreCase);

            if (!matched)
            {
                if (softMode)
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "BODY_CONTAINS_PARTIAL_MISMATCH",
                        Message = $"Response body không chứa '{Truncate(pattern, 100)}', nhưng được bỏ qua vì test Negative/Boundary đã khớp status code đúng.",
                        Target = "BodyContains",
                    });
                }
                else
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
        }

        result.BodyContainsPassed = allPassed || softMode;
        return true;
    }

    private static bool ValidateBodyNotContains(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ExecutionTestCaseDto testCase,
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
        var softMode = IsBodyContainsSoftMode(response, expectation, testCase, result);

        foreach (var pattern in normalizedPatterns)
        {
            if (body.Contains(pattern, StringComparison.Ordinal))
            {
                if (softMode)
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "BODY_NOT_CONTAINS_PARTIAL_MISMATCH",
                        Message = $"Response body chứa '{Truncate(pattern, 100)}', nhưng được bỏ qua vì test Negative/Boundary đã khớp status code đúng.",
                        Target = "BodyNotContains",
                    });
                }
                else
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
        }

        result.BodyNotContainsPassed = allPassed || softMode;
        return true;
    }

    private static string ResolveExpectedValue(string expected, IReadOnlyDictionary<string, string> variableBag)
    {
        if (variableBag == null || string.IsNullOrEmpty(expected) || !expected.Contains("{{", StringComparison.Ordinal))
            return expected;
        return System.Text.RegularExpressions.Regex.Replace(expected, @"\{\{(\w+)\}\}", m =>
            variableBag.TryGetValue(m.Groups[1].Value, out var v) && !string.IsNullOrEmpty(v) ? v : m.Value);
    }

    private static bool ValidateJsonPathChecks(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ExecutionTestCaseDto testCase,
        TestCaseValidationResult result,
        IReadOnlyDictionary<string, string> variableBag = null)
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
        var jsonPathSoftMode = IsBodyContainsSoftMode(response, expectation, testCase, result);
        using (doc)
        {
            foreach (var check in checks)
            {
                var resolvedExpected = ResolveExpectedValue(check.Value, variableBag);
                var element = VariableExtractor.NavigateJsonPath(doc.RootElement, check.Key);
                var actualValue = element?.ToString();

                if (actualValue == null || !ValuesEqual(actualValue, resolvedExpected, element))
                {
                    // Treat "notnull", "*", "*.+", ".+", empty, and JWT-like values as existence checks.
                    var isExistenceCheck = string.IsNullOrWhiteSpace(resolvedExpected)
                        || resolvedExpected == "*"
                        || resolvedExpected == "*.+"
                        || resolvedExpected == "*+"
                        || resolvedExpected == ".+"
                        || resolvedExpected == "+"
                        || string.Equals(resolvedExpected, "notnull", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(resolvedExpected, "any", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(resolvedExpected, "present", StringComparison.OrdinalIgnoreCase)
                        || LooksLikeExistenceExpectation(resolvedExpected)
                        || JwtLikeRegex.IsMatch(resolvedExpected);

                    // Soft-forgive ONLY when the path does not exist in the response (null).
                    // Error responses legitimately lack success-path fields — that is expected.
                    // When the field EXISTS but has the wrong value, it is a genuine assertion
                    // failure regardless of soft mode.  Existence checks ("*"/"notnull") require
                    // presence, so null is never soft-forgiven for them either.
                    var canSoftForgive = jsonPathSoftMode && actualValue == null && !isExistenceCheck;

                    if (canSoftForgive)
                    {
                        result.Warnings.Add(new ValidationWarningModel
                        {
                            Code = "JSONPATH_PARTIAL_MISMATCH",
                            Message = $"JSONPath '{check.Key}' không tìm thấy, nhưng được bỏ qua vì test Negative/Boundary đã khớp status code đúng.",
                            Target = check.Key,
                        });
                    }
                    else
                    {
                        allPassed = false;
                        result.Failures.Add(new ValidationFailureModel
                        {
                            Code = "JSONPATH_ASSERTION_FAILED",
                            Message = isExistenceCheck
                                ? $"JSONPath '{check.Key}' phải tồn tại nhưng không tìm thấy trong response."
                                : $"JSONPath '{check.Key}' không khớp. Mong đợi: '{resolvedExpected}', thực tế: '{actualValue ?? "(null)"}'.",
                            Target = check.Key,
                            Expected = resolvedExpected,
                            Actual = actualValue,
                        });
                    }
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

    private static void AddInvalidExpectationWarning(
        TestCaseValidationResult result,
        string target,
        string details)
    {
        result.Warnings.Add(new ValidationWarningModel
        {
            Code = InvalidExpectationFormatCode,
            Message = $"{target} không đúng định dạng mong đợi. {details}",
            Target = target,
        });
    }

    private static bool ValuesEqual(string actual, string expected, JsonElement? actualElement = null)
    {
        // Wildcards: "*", "notnull", and empty/null all mean "field must exist with any non-null value".
        // Empty expected value is treated as an existence check (not a literal empty-string match)
        // because the DB sometimes stores "" when the LLM generated a token field assertion
        // without knowing the actual value.
        // Also treat common LLM-generated non-empty patterns as existence checks:
        //   "*.+" / "*+" / ".+" / "+" / "any" / "present" — all mean "must be non-empty".
        if (string.IsNullOrWhiteSpace(expected)
            || expected == "*"
            || expected == "*.+"
            || expected == "*+"
            || expected == ".+"
            || expected == "+"
            || string.Equals(expected, "notnull", StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, "non-null", StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, "non null", StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, "nonnull", StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, "any", StringComparison.OrdinalIgnoreCase)
            || string.Equals(expected, "present", StringComparison.OrdinalIgnoreCase))
        {
            return actual != null && actual.Length > 0;
        }

        if (LooksLikeExistenceExpectation(expected))
        {
            return actualElement.HasValue;
        }

        if (LooksLikeNonEmptyExpectation(expected))
        {
            if (!actualElement.HasValue)
            {
                return false;
            }

            return actualElement.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => false,
                JsonValueKind.String => !string.IsNullOrWhiteSpace(actualElement.Value.GetString()),
                JsonValueKind.Array => actualElement.Value.GetArrayLength() > 0,
                JsonValueKind.Object => actualElement.Value.EnumerateObject().Any(),
                _ => true,
            };
        }

        if (LooksLikeStringExpectation(expected))
        {
            return actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.String;
        }

        if (LooksLikeBooleanExpectation(expected))
        {
            if (!actualElement.HasValue)
            {
                return false;
            }

            return actualElement.Value.ValueKind == JsonValueKind.True
                || actualElement.Value.ValueKind == JsonValueKind.False;
        }

        if (LooksLikeNumberExpectation(expected))
        {
            return actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Number;
        }

        if (LooksLikeArrayExpectation(expected))
        {
            return actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Array;
        }

        if (LooksLikeObjectExpectation(expected))
        {
            return actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Object;
        }

        if (LooksLikeDateTimeExpectation(expected))
        {
            return actualElement.HasValue
                && actualElement.Value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(actualElement.Value.GetString(), out _);
        }

        if (LooksLikeGuidExpectation(expected))
        {
            return actualElement.HasValue
                && actualElement.Value.ValueKind == JsonValueKind.String
                && Guid.TryParse(actualElement.Value.GetString(), out _);
        }

        // JWT guard: a stored JWT token will never match a newly-issued one (different
        // iat/exp/jti claims per session). Treat a JWT-like expected value as an
        // existence check — the useful assertion is "token is present", not exact match.
        if (JwtLikeRegex.IsMatch(expected))
        {
            return actual != null && actual.Length > 0;
        }

        if (actual == expected)
        {
            return true;
        }

        if (MatchesTcUniqueIdTemplate(actual, expected))
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

        // Truthiness check: LLMs often generate `$.someField = true` to mean "the field
        // must be present and non-empty / truthy", not a literal boolean equality.
        // For example, `$.errors.fieldErrors.password = true` is intended as
        // "password validation errors exist", but the actual value is an array of messages.
        // When expected is literally "true", treat non-null/non-empty/non-false/non-zero
        // actual values as passing — the intent is a truthy existence check.
        if (string.Equals(expected, "true", StringComparison.OrdinalIgnoreCase))
        {
            if (actual == null || actual.Length == 0)
            {
                return false;
            }

            // Explicit falsy literals → not truthy
            if (string.Equals(actual, "false", StringComparison.OrdinalIgnoreCase)
                || actual == "0"
                || actual == "null"
                || actual == "[]"
                || actual == "{}")
            {
                return false;
            }

            // Non-empty array or object, any string, positive number → truthy
            return true;
        }

        // When expected is "false", only the literal boolean false or 0 values satisfy it.
        if (string.Equals(expected, "false", StringComparison.OrdinalIgnoreCase))
        {
            return actual != null
                && (string.Equals(actual, "false", StringComparison.OrdinalIgnoreCase)
                    || actual == "0"
                    || actual == "null"
                    || actual == "[]"
                    || actual == "{}");
        }

        // Email fuzzy match: LLMs sometimes generate an email expected value with a different
        // prefix than what VariableResolver generates (e.g. "upper_abc12345@example.com" vs
        // "testuser_abc12345@example.com"). If both look like emails, share the same @domain,
        // and share the same unique suffix after the last '_' (≥6 chars), treat as equal.
        // This covers boundary test cases that test email format/case normalisation where
        // the prefix is semantic but the uniqueness is encoded in the suffix.
        if (actual != null
            && actual.Contains('@', StringComparison.Ordinal)
            && expected.Contains('@', StringComparison.Ordinal))
        {
            var actualAt = actual.IndexOf('@');
            var expectedAt = expected.IndexOf('@');
            var actualDomain = actual[(actualAt + 1)..].ToLowerInvariant();
            var expectedDomain = expected[(expectedAt + 1)..].ToLowerInvariant();
            if (actualDomain == expectedDomain)
            {
                var actualLocal = actual[..actualAt].ToLowerInvariant();
                var expectedLocal = expected[..expectedAt].ToLowerInvariant();
                var actualUnder = actualLocal.LastIndexOf('_');
                var expectedUnder = expectedLocal.LastIndexOf('_');
                if (actualUnder >= 0 && expectedUnder >= 0)
                {
                    var actualSuffix = actualLocal[(actualUnder + 1)..];
                    var expectedSuffix = expectedLocal[(expectedUnder + 1)..];
                    if (actualSuffix.Length >= 6 && actualSuffix == expectedSuffix)
                        return true;
                }
            }
        }

        // Regex check: LLMs sometimes generate `{"regex":"^pattern$"}` as the expected value
        // to describe a structural assertion (e.g. JWT format, UUID, email).
        // Extract the pattern and test it against actual.
        if (expected.StartsWith("{", StringComparison.Ordinal) && expected.Contains("\"regex\"", StringComparison.Ordinal))
        {
            try
            {
                var regexDoc = JsonDocument.Parse(expected);
                if (regexDoc.RootElement.TryGetProperty("regex", out var patternEl))
                {
                    var pattern = patternEl.GetString();
                    if (!string.IsNullOrEmpty(pattern) && actual != null)
                    {
                        return System.Text.RegularExpressions.Regex.IsMatch(
                            actual,
                            pattern,
                            System.Text.RegularExpressions.RegexOptions.None,
                            TimeSpan.FromMilliseconds(500));
                    }
                }
            }
            catch
            {
                // Not a valid regex object — fall through to literal comparison
            }
        }

        // Inline regex support: many prompts serialize regex checks as "regex:<pattern>".
        if (actual != null && TryExtractInlineRegexPattern(expected, out var inlinePattern))
        {
            try
            {
                return Regex.IsMatch(actual, inlinePattern, RegexOptions.None, TimeSpan.FromMilliseconds(500));
            }
            catch
            {
                // Invalid inline regex — fall through to other strategies.
            }
        }

        // Plain regex support: many LLM outputs provide regex directly as a string,
        // e.g. "^.+\\..+\\..+$" for JWT-like token checks.
        if (actual != null && LooksLikeRegexPattern(expected))
        {
            try
            {
                return Regex.IsMatch(actual, expected, RegexOptions.None, TimeSpan.FromMilliseconds(500));
            }
            catch
            {
                // Invalid regex — fall through to literal comparison.
            }
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static bool LooksLikeExistenceExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = expected.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(text, @"[\s_-]+", string.Empty);
        return text == "exists"
            || text == "exist"
            || text == "must exist"
            || text == "should exist"
            || text == "is present"
            || text == "must be present"
            || text == "be present"
            || text == "available"
            || text == "is available"
            || text == "defined"
            || text == "is defined"
            || normalized == "notnull"
            || normalized == "nonnull";
    }

    private static bool LooksLikeNonEmptyExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = expected.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(text, @"[\s_-]+", string.Empty);
        return text.Contains("non-empty", StringComparison.Ordinal)
            || text.Contains("not empty", StringComparison.Ordinal)
            || text.Contains("not blank", StringComparison.Ordinal)
            || text.Contains("non empty", StringComparison.Ordinal)
            || text.Contains("must be present", StringComparison.Ordinal)
            || normalized.Contains("nonempty", StringComparison.Ordinal)
            || normalized.Contains("notempty", StringComparison.Ordinal)
            || normalized.Contains("nonblank", StringComparison.Ordinal)
            || normalized.Contains("notblank", StringComparison.Ordinal);
    }

    private static bool LooksLikeStringExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text == "string"
            || text == "must be string"
            || text == "should be string"
            || text == "must be a string"
            || text == "must be a non-empty string"
            || text == "must be non-empty string";
    }

    private static bool LooksLikeBooleanExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text == "boolean"
            || text == "bool"
            || text == "must be boolean"
            || text == "must be bool"
            || text == "should be boolean";
    }

    private static bool LooksLikeNumberExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text == "number"
            || text == "numeric"
            || text == "must be number"
            || text == "must be numeric"
            || text == "should be number"
            || text == "integer"
            || text == "must be integer";
    }

    private static bool LooksLikeArrayExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text == "array"
            || text == "list"
            || text == "must be array"
            || text == "should be array";
    }

    private static bool LooksLikeObjectExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text == "object"
            || text == "json object"
            || text == "must be object"
            || text == "should be object";
    }

    private static bool LooksLikeDateTimeExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text.Contains("datetime", StringComparison.Ordinal)
            || text.Contains("date time", StringComparison.Ordinal)
            || text.Contains("date-time", StringComparison.Ordinal)
            || text.Contains("timestamp", StringComparison.Ordinal)
            || text.Contains("iso", StringComparison.Ordinal) && text.Contains("date", StringComparison.Ordinal)
            || text.Contains("createdat", StringComparison.Ordinal) && LooksLikeNonEmptyExpectation(text);
    }

    private static bool LooksLikeGuidExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text.Contains("uuid", StringComparison.Ordinal)
            || text.Contains("guid", StringComparison.Ordinal)
            || text.Contains("objectid", StringComparison.Ordinal)
            || text.Contains("object id", StringComparison.Ordinal)
            || text.Contains("mongo id", StringComparison.Ordinal);
    }

    private static string NormalizeTypeToken(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return string.Empty;
        }

        var text = expected.Trim().ToLowerInvariant();
        if (text.StartsWith("type:", StringComparison.Ordinal) || text.StartsWith("type=", StringComparison.Ordinal))
        {
            return text[5..].Trim();
        }

        if (text.StartsWith("format:", StringComparison.Ordinal) || text.StartsWith("format=", StringComparison.Ordinal))
        {
            return text[7..].Trim();
        }

        return text;
    }

    private static bool TryExtractInlineRegexPattern(string expected, out string pattern)
    {
        pattern = null;

        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var candidate = expected.Trim();
        if (!candidate.StartsWith("regex", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = candidate[5..].TrimStart(); // after "regex"
        if (remainder.StartsWith("pattern", StringComparison.OrdinalIgnoreCase))
        {
            remainder = remainder[7..].TrimStart();
        }

        if (remainder.StartsWith(":", StringComparison.Ordinal) ||
            remainder.StartsWith("=", StringComparison.Ordinal))
        {
            remainder = remainder[1..].TrimStart();
        }

        remainder = remainder.Trim().Trim('\"', '\'');
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        // Support slash-delimited forms: /pattern/ or /pattern/i
        if (remainder.StartsWith("/", StringComparison.Ordinal))
        {
            var lastSlash = remainder.LastIndexOf('/');
            if (lastSlash > 0)
            {
                remainder = remainder[1..lastSlash];
            }
        }

        pattern = remainder;
        return !string.IsNullOrWhiteSpace(pattern);
    }

    private static bool LooksLikeRegexPattern(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var candidate = expected.Trim();

        // "regex:<pattern>" is handled by TryExtractInlineRegexPattern.
        if (candidate.StartsWith("regex", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Skip canonical existence-check tokens handled above.
        if (candidate == "*.+" || candidate == "*+" || candidate == ".+" || candidate == "+")
        {
            return false;
        }

        if (candidate.StartsWith("^", StringComparison.Ordinal) || candidate.EndsWith("$", StringComparison.Ordinal))
        {
            return true;
        }

        return candidate.Contains(@"\d", StringComparison.Ordinal)
            || candidate.Contains(@"\w", StringComparison.Ordinal)
            || candidate.Contains(@"\S", StringComparison.Ordinal)
            || candidate.Contains(@"\s", StringComparison.Ordinal)
            || candidate.Contains(".*", StringComparison.Ordinal)
            || candidate.Contains(".+", StringComparison.Ordinal)
            || candidate.Contains("(?:", StringComparison.Ordinal)
            || (candidate.Contains("[", StringComparison.Ordinal) && candidate.Contains("]", StringComparison.Ordinal));
    }

    private static bool MatchesTcUniqueIdTemplate(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual)
            || string.IsNullOrWhiteSpace(expected)
            || !expected.Contains("{{tcUniqueId}}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // tcUniqueId is an 8-char hex token. Some responses append an additional run suffix,
        // e.g. "<prefix>_<tcUniqueId>-<runSuffix>", so allow an optional "-xxxxxxxx" tail.
        var expectedPattern = "^" + Regex.Escape(expected) + "$";
        expectedPattern = TcUniqueIdPlaceholderRegex.Replace(expectedPattern, "[a-f0-9]{8}(?:-[a-z0-9]{8})?");

        return Regex.IsMatch(actual, expectedPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
    /// Strips all whitespace that surrounds JSON structural characters ({ } [ ] : ,).
    /// This normalises any whitespace style (compact, pretty, hybrid) to a single
    /// canonical compact form so pattern matching is whitespace-agnostic.
    /// Note: whitespace INSIDE string literals that also contains structural characters
    /// is also affected, but in practice patterns from LLM do not rely on exact
    /// whitespace inside string values, so the trade-off is acceptable.
    /// </summary>
    private static string NormalizeJsonWhitespace(string s) =>
        string.IsNullOrEmpty(s) ? s : Regex.Replace(s, @"\s*([{}\[\]:,])\s*", "$1");

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

        if (IsAdaptivePermissiveStatusMatch(actualStatus, expectedStatuses, testCase, response))
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "ADAPTIVE_PERMISSIVE_STATUS_MATCH",
                Message = $"Status thực tế {actualStatus} không nằm trong kỳ vọng [{string.Join(", ", expectedStatuses)}], nhưng được chấp nhận vì endpoint thể hiện hành vi permissive cho test case {testCase?.TestType ?? "(unknown)"}.",
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

    private static bool IsAdaptivePermissiveStatusMatch(
        int actualStatus,
        IReadOnlyCollection<int> expectedStatuses,
        ExecutionTestCaseDto testCase,
        HttpTestResponse response)
    {
        if (actualStatus < 200 || actualStatus >= 300)
        {
            return false;
        }

        if (expectedStatuses == null || expectedStatuses.Count == 0)
        {
            return false;
        }

        if (expectedStatuses.Any(status => status >= 200 && status < 300) ||
            !expectedStatuses.Any(status => status >= 400 && status < 500))
        {
            return false;
        }

        if (!IsBoundaryOrNegativeCase(testCase))
        {
            return false;
        }

        // Keep explicit error-shaped payload handling in ADAPTIVE_ERROR_PAYLOAD_MATCH.
        return !LooksLikeErrorPayload(response?.Body);
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

    private static bool IsBoundaryOrNegativeCase(ExecutionTestCaseDto testCase)
    {
        if (IsBoundaryOrNegative(testCase?.TestType))
        {
            return true;
        }

        var name = testCase?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.IndexOf("boundary", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("negative", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0;
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
