using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;
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

    // Detects JWT-like values: three non-trivial base64url segments separated by dots.
    // JWTs are session-specific and always differ across runs; treat as existence checks.
    private static readonly System.Text.RegularExpressions.Regex JwtLikeRegex = new(
        @"^[A-Za-z0-9\-_]{10,}\.[A-Za-z0-9\-_]{10,}\.[A-Za-z0-9\-_]{10,}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex ObjectIdRegex = new(
        @"^[0-9a-fA-F]{24}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex LengthFunctionRegex = new(
        @"^(?:len|length)\s*\(\s*(\d+)\s*\)$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex NumericComparatorRegex = new(
        @"^(>=|<=|>|<|==|=)\s*(-?\d+(?:\.\d+)?)$",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly TimeSpan SemanticDateTimeTolerance = TimeSpan.FromSeconds(5);

    private readonly ILogger<RuleBasedValidator> _logger;
    private readonly IJsonPathResolver _jsonPathResolver;

    public RuleBasedValidator(
        ILogger<RuleBasedValidator> logger,
        IJsonPathResolver jsonPathResolver)
    {
        _logger = logger;
        _jsonPathResolver = jsonPathResolver ?? throw new ArgumentNullException(nameof(jsonPathResolver));
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
        TrackCheck(
            ValidateStatusCode(
                response,
                testCase,
                expectation,
                result,
                ShouldAllowAdaptiveStatusMatching(testCase, profile)),
            ref checksPerformed,
            ref checksSkipped);

        // 2. Response schema validation
        TrackCheck(ValidateResponseSchema(response, expectation, endpointMetadata, result, strictMode), ref checksPerformed, ref checksSkipped);

        // 3. Header exact-match validation
        TrackCheck(ValidateHeaders(response, expectation, result), ref checksPerformed, ref checksSkipped);

        // 4. Body contains
        TrackCheck(ValidateBodyContains(response, expectation, testCase, result, variableBag), ref checksPerformed, ref checksSkipped);

        // 5. Body not contains
        TrackCheck(ValidateBodyNotContains(response, expectation, testCase, result), ref checksPerformed, ref checksSkipped);

        // 6. JSONPath equality checks
        TrackCheck(ValidateJsonPathChecks(response, expectation, testCase, endpointMetadata, result, profile, _jsonPathResolver, variableBag), ref checksPerformed, ref checksSkipped);

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

        var scoreThreshold = ResolveValidationScoreThreshold(profile);
        var hardChecksPassed = EvaluateHardChecksPassed(result);
        var validationScore = ComputeValidationScore(result);

        result.ValidationScoreThreshold = scoreThreshold;
        result.HardChecksPassed = hardChecksPassed;
        result.ValidationScore = validationScore;

        if (ShouldDowngradeSoftJsonPathFailures(profile, result, hardChecksPassed, validationScore, scoreThreshold))
        {
            DowngradeSoftJsonPathFailures(result);
            validationScore = ComputeValidationScore(result);
            result.ValidationScore = validationScore;
        }

        result.IsPassed = result.Failures.Count == 0
            && hardChecksPassed
            && validationScore >= scoreThreshold;
        return result;
    }

    private static decimal ResolveValidationScoreThreshold(ValidationProfile profile)
    {
        return profile switch
        {
            ValidationProfile.SrsStrict => 0.95m,
            ValidationProfile.DemoAdaptive => 0.70m,
            _ => 0.80m,
        };
    }

    private static bool EvaluateHardChecksPassed(TestCaseValidationResult result)
    {
        if (result == null)
        {
            return false;
        }

        // Hard gates: status, schema, and explicit body contract checks must pass.
        if (!result.StatusCodeMatched)
        {
            return false;
        }

        if (result.SchemaMatched == false)
        {
            return false;
        }

        if (result.BodyContainsPassed == false)
        {
            return false;
        }

        if (result.BodyNotContainsPassed == false)
        {
            return false;
        }

        return true;
    }

    private static decimal ComputeValidationScore(TestCaseValidationResult result)
    {
        if (result == null)
        {
            return 0m;
        }

        decimal weightedTotal = 0m;
        decimal weightsApplied = 0m;

        AddWeightedCheck(result.StatusCodeMatched, 0.35m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.SchemaMatched, 0.20m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.HeaderChecksPassed, 0.10m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.BodyContainsPassed, 0.10m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.BodyNotContainsPassed, 0.10m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.JsonPathChecksPassed, 0.10m, ref weightedTotal, ref weightsApplied);
        AddWeightedNullableCheck(result.ResponseTimePassed, 0.05m, ref weightedTotal, ref weightsApplied);

        var baseScore = weightsApplied > 0m ? weightedTotal / weightsApplied : 1m;

        // Warning penalty keeps semantics strict but avoids brittle hard-fail behavior.
        var warningPenalty = Math.Min(0.10m, (result.Warnings?.Count ?? 0) * 0.02m);
        var penalized = baseScore - warningPenalty;
        if (penalized < 0m)
        {
            penalized = 0m;
        }

        return Math.Round(penalized, 4);
    }

    private static bool ShouldDowngradeSoftJsonPathFailures(
        ValidationProfile profile,
        TestCaseValidationResult result,
        bool hardChecksPassed,
        decimal validationScore,
        decimal scoreThreshold)
    {
        if (profile == ValidationProfile.SrsStrict
            || result?.Failures == null
            || result.Failures.Count == 0
            || !hardChecksPassed
            || validationScore < scoreThreshold)
        {
            return false;
        }

        return result.Failures.All(f => string.Equals(f.Code, "JSONPATH_ASSERTION_FAILED", StringComparison.Ordinal));
    }

    private static void DowngradeSoftJsonPathFailures(TestCaseValidationResult result)
    {
        foreach (var failure in result.Failures.ToArray())
        {
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "JSONPATH_ASSERTION_WARNING",
                Message = $"{failure.Message} Đã hạ xuống warning vì hard checks pass và validation score đạt ngưỡng.",
                Target = failure.Target,
            });
        }

        result.Failures.Clear();
        result.JsonPathChecksPassed = true;
    }

    private static void AddWeightedCheck(bool passed, decimal weight, ref decimal weightedTotal, ref decimal weightsApplied)
    {
        weightsApplied += weight;
        if (passed)
        {
            weightedTotal += weight;
        }
    }

    private static void AddWeightedNullableCheck(bool? passed, decimal weight, ref decimal weightedTotal, ref decimal weightsApplied)
    {
        if (!passed.HasValue)
        {
            return;
        }

        weightsApplied += weight;
        if (passed.Value)
        {
            weightedTotal += weight;
        }
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
        TestCaseValidationResult result,
        IReadOnlyDictionary<string, string> variableBag)
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
            .Select(pattern => ResolveExpectationPlaceholders(pattern, variableBag, testCase))
            .Select(NormalizeBodyContainsPattern)
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
            var isAuthoritative = IsAuthoritativeExpectationCheck(
                expectation,
                "bodyContains",
                "bodyContains",
                pattern);

            if (ContainsUnresolvedPlaceholder(pattern))
            {
                if (isAuthoritative)
                {
                    allPassed = false;
                    result.Failures.Add(new ValidationFailureModel
                    {
                        Code = "BODY_CONTAINS_UNRESOLVED_PLACEHOLDER",
                        Message = $"BodyContains contains an unresolved placeholder: '{Truncate(pattern, 100)}'.",
                        Target = "BodyContains",
                        Expected = Truncate(pattern, 200),
                    });
                }
                else
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "BODY_CONTAINS_UNRESOLVED_PLACEHOLDER_WARNING",
                        Message = $"BodyContains '{Truncate(pattern, 100)}' was unresolved but is not authoritative, so it was treated as a warning.",
                        Target = "BodyContains",
                    });
                }

                continue;
            }

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
                else if (!isAuthoritative)
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "BODY_CONTAINS_INFERRED_MISSING_WARNING",
                        Message = $"Response body did not contain '{Truncate(pattern, 100)}', but this expectation is AI-inferred/unverified and was not used as a hard gate.",
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

    private static string ResolveExpectationPlaceholders(
        string value,
        IReadOnlyDictionary<string, string> variableBag,
        ExecutionTestCaseDto testCase = null)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            variableBag == null ||
            variableBag.Count == 0 ||
            !value.Contains("{{", StringComparison.Ordinal))
        {
            return value;
        }

        return Regex.Replace(
            value,
            @"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}",
            match =>
            {
                var variableName = match.Groups[1].Value;
                if (TryResolveDependencyScopedExpectationVariable(variableName, variableBag, testCase, out var scopedValue))
                {
                    return scopedValue ?? string.Empty;
                }

                return variableBag.TryGetValue(variableName, out var resolved)
                    ? resolved ?? string.Empty
                    : match.Value;
            });
    }

    private static bool TryResolveDependencyScopedExpectationVariable(
        string variableName,
        IReadOnlyDictionary<string, string> variableBag,
        ExecutionTestCaseDto testCase,
        out string value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(variableName)
            || variableBag == null
            || testCase?.DependencyIds == null
            || testCase.DependencyIds.Count == 0)
        {
            return false;
        }

        for (var i = testCase.DependencyIds.Count - 1; i >= 0; i--)
        {
            var scopedKey = $"case.{testCase.DependencyIds[i]:N}.{variableName}";
            if (variableBag.TryGetValue(scopedKey, out var scopedValue)
                && !string.IsNullOrWhiteSpace(scopedValue)
                && !scopedValue.Contains("{{", StringComparison.Ordinal))
            {
                value = scopedValue;
                return true;
            }
        }

        return false;
    }

    private static bool IsAuthoritativeExpectationCheck(
        ExecutionTestCaseExpectationDto expectation,
        string type,
        string field,
        string expected)
    {
        if (expectation == null)
        {
            return false;
        }

        var items = ParseExpectedProvenance(expectation.ExpectedProvenance);
        if (items.Count > 0)
        {
            return items.Any(item =>
                IsSameProvenanceType(item.Type, type)
                && IsSameProvenanceField(item.Field, field)
                && IsAuthoritativeSource(item.Source, item.Confidence));
        }

        // Without item-level provenance, only explicit non-LLM sources are hard gates.
        return IsAuthoritativeSource(expectation.ExpectationSource, "medium");
    }

    private static List<ExpectedProvenanceRuntimeItem> ParseExpectedProvenance(string expectedProvenance)
    {
        if (string.IsNullOrWhiteSpace(expectedProvenance))
        {
            return new List<ExpectedProvenanceRuntimeItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ExpectedProvenanceRuntimeItem>>(
                    expectedProvenance,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?.Where(x => !string.IsNullOrWhiteSpace(x?.Type))
                .ToList()
                ?? new List<ExpectedProvenanceRuntimeItem>();
        }
        catch
        {
            return new List<ExpectedProvenanceRuntimeItem>();
        }
    }

    private static bool IsSameProvenanceType(string actual, string expected)
        => string.Equals(NormalizeProvenanceToken(actual), NormalizeProvenanceToken(expected), StringComparison.Ordinal);

    private static bool IsSameProvenanceField(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        return string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeProvenanceToken(actual), NormalizeProvenanceToken(expected), StringComparison.Ordinal);
    }

    private static bool IsAuthoritativeSource(string source, string confidence)
    {
        var normalizedSource = NormalizeProvenanceToken(source);
        var normalizedConfidence = NormalizeProvenanceToken(confidence);

        if (normalizedSource is "srs" or "openapi" or "swagger" or "businessrule")
        {
            return normalizedConfidence is not "low";
        }

        return false;
    }

    private static string NormalizeProvenanceToken(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);

    private static bool TryRedirectRootArrayCheckToDataArray(
        JsonElement root,
        string jsonPath,
        string expected,
        out string redirectedJsonPath)
    {
        redirectedJsonPath = null;

        if (!string.Equals(jsonPath?.Trim(), "$", StringComparison.Ordinal)
            || !LooksLikeArrayTypeExpectation(expected)
            || root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        redirectedJsonPath = "$.data";
        return true;
    }

    private static bool LooksLikeArrayTypeExpectation(string expected)
    {
        if (LooksLikeArrayExpectation(expected))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(expected);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return doc.RootElement.TryGetProperty("op", out var op)
                && string.Equals(op.GetString(), "type", StringComparison.OrdinalIgnoreCase)
                && doc.RootElement.TryGetProperty("value", out var value)
                && string.Equals(value.GetString(), "array", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool ShouldAllowAdaptiveStatusMatching(
        ExecutionTestCaseDto testCase,
        ValidationProfile profile)
    {
        if (profile == ValidationProfile.SrsStrict)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(testCase?.Tags))
        {
            return false;
        }

        try
        {
            var tags = JsonSerializer.Deserialize<string[]>(testCase.Tags) ?? Array.Empty<string>();
            return tags.Any(t =>
                string.Equals(t?.Trim(), "status-adaptive:allow", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeBodyContainsPattern(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Trim();

        // Support markdown-wrapped expected tokens from LLM/n8n, e.g. "*{{name}}*" or "`{{name}}`".
        while (normalized.Length >= 2)
        {
            var startsEndsWithAsterisk = normalized.StartsWith('*') && normalized.EndsWith('*');
            var startsEndsWithBacktick = normalized.StartsWith('`') && normalized.EndsWith('`');
            var startsEndsWithQuote = normalized.StartsWith('"') && normalized.EndsWith('"');
            if (!startsEndsWithAsterisk && !startsEndsWithBacktick && !startsEndsWithQuote)
            {
                break;
            }

            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }

    private static bool ContainsUnresolvedPlaceholder(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("{{", StringComparison.Ordinal)
            && value.Contains("}}", StringComparison.Ordinal);
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
        {
            var key = m.Groups[1].Value;
            return TryResolveVariableValue(variableBag, key, out var resolved) && !string.IsNullOrEmpty(resolved)
                ? resolved
                : m.Value;
        });
    }

    private static string ResolveExpectedJsonPathCheck(JsonElement expectedElement, IReadOnlyDictionary<string, string> variableBag)
    {
        if (expectedElement.ValueKind == JsonValueKind.String)
        {
            return ResolveExpectedValue(expectedElement.GetString(), variableBag);
        }

        if (expectedElement.ValueKind == JsonValueKind.Object || expectedElement.ValueKind == JsonValueKind.Array)
        {
            var normalized = ResolvePlaceholdersInJsonElement(expectedElement, variableBag);
            return normalized?.ToJsonString() ?? expectedElement.GetRawText();
        }

        return expectedElement.GetRawText();
    }

    private static JsonNode ResolvePlaceholdersInJsonElement(JsonElement element, IReadOnlyDictionary<string, string> variableBag)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = new JsonObject();
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = ResolvePlaceholdersInJsonElement(prop.Value, variableBag);
                }
                return obj;
            }
            case JsonValueKind.Array:
            {
                var arr = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(ResolvePlaceholdersInJsonElement(item, variableBag));
                }
                return arr;
            }
            case JsonValueKind.String:
                return JsonValue.Create(ResolveExpectedValue(element.GetString(), variableBag));
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return JsonValue.Create(l);
                }
                if (element.TryGetDouble(out var d))
                {
                    return JsonValue.Create(d);
                }
                return JsonValue.Create(element.GetRawText());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return JsonValue.Create(element.GetRawText());
        }
    }

    private static bool TryResolveIdentifierAliasExpectedValue(
        string jsonPath,
        string rawExpected,
        IReadOnlyDictionary<string, string> variableBag,
        string actualValue,
        out string matchedExpected)
    {
        matchedExpected = null;
        if (string.IsNullOrWhiteSpace(jsonPath)
            || string.IsNullOrWhiteSpace(rawExpected)
            || variableBag == null
            || string.IsNullOrWhiteSpace(actualValue))
        {
            return false;
        }

        var placeholderMatch = Regex.Match(rawExpected, @"^\s*\{\{\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}\s*$");
        if (!placeholderMatch.Success)
        {
            return false;
        }

        var leaf = ExtractJsonPathLeaf(jsonPath);
        if (string.IsNullOrWhiteSpace(leaf) || !leaf.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidates = BuildIdentifierAliasKeys(leaf, placeholderMatch.Groups["name"].Value, variableBag.Keys);
        foreach (var key in candidates)
        {
            if (!TryResolveVariableValue(variableBag, key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(value, actualValue, StringComparison.OrdinalIgnoreCase))
            {
                matchedExpected = value;
                return true;
            }
        }

        return false;
    }

    private static string ExtractJsonPathLeaf(string jsonPath)
    {
        var path = jsonPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var lastDot = path.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < path.Length - 1)
        {
            return path[(lastDot + 1)..].Trim(' ', '"', '\'', '[', ']');
        }

        return path.Trim(' ', '$', '.', '"', '\'', '[', ']');
    }

    private static IEnumerable<string> BuildIdentifierAliasKeys(
        string leafKey,
        string placeholderKey,
        IEnumerable<string> allKeys)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(placeholderKey))
        {
            keys.Add(placeholderKey);
        }

        if (!string.IsNullOrWhiteSpace(leafKey))
        {
            keys.Add(leafKey);
            var pascal = char.ToUpperInvariant(leafKey[0]) + leafKey[1..];
            keys.Add($"first{pascal}");
            keys.Add($"created{pascal}");
            keys.Add($"new{pascal}");
            keys.Add($"last{pascal}");
        }

        if (allKeys != null)
        {
            foreach (var key in allKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(leafKey)
                    && key.EndsWith(leafKey, StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    private static bool ValidateJsonPathChecks(
        HttpTestResponse response,
        ExecutionTestCaseExpectationDto expectation,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata,
        TestCaseValidationResult result,
        ValidationProfile profile,
        IJsonPathResolver jsonPathResolver,
        IReadOnlyDictionary<string, string> variableBag = null)
    {
        if (string.IsNullOrWhiteSpace(expectation.JsonPathChecks))
        {
            return false;
        }

        Dictionary<string, JsonElement> checks;
        try
        {
            checks = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(expectation.JsonPathChecks);
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
                if (!string.IsNullOrWhiteSpace(check.Key)
                    && check.Key.IndexOf("expectationSource", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var pathResolution = jsonPathResolver.Resolve(new JsonPathResolutionRequest
                {
                    OriginalPath = check.Key,
                    ActualResponseJson = response.Body,
                    SwaggerResponseSchemas = BuildJsonPathResolutionSchemas(expectation, endpointMetadata),
                    EndpointPath = testCase?.Request?.Url,
                    HttpMethod = testCase?.Request?.HttpMethod,
                });
                var effectiveJsonPath = pathResolution.IsResolved && !string.IsNullOrWhiteSpace(pathResolution.ResolvedPath)
                    ? pathResolution.ResolvedPath
                    : check.Key;

                var resolvedExpected = ResolveExpectedJsonPathCheck(check.Value, variableBag);
                resolvedExpected = ResolveExpectedFromRequestIfNeeded(
                    resolvedExpected,
                    testCase?.Request);
                var isAuthoritative = IsAuthoritativeExpectationCheck(
                    expectation,
                    "jsonPathCheck",
                    check.Key,
                    resolvedExpected);
                if (TryRedirectRootArrayCheckToDataArray(doc.RootElement, effectiveJsonPath, resolvedExpected, out var redirectedJsonPath))
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "JSONPATH_ROOT_ARRAY_WRAPPER_NORMALIZED",
                        Message = $"JSONPath '{effectiveJsonPath}' expected an array but response root is an object with data array. Normalized to '{redirectedJsonPath}'.",
                        Target = redirectedJsonPath,
                    });
                    effectiveJsonPath = redirectedJsonPath;
                }
                var element = pathResolution.IsResolved
                    ? VariableExtractor.NavigateJsonPath(doc.RootElement, effectiveJsonPath)
                    : null;
                if (!element.HasValue)
                {
                    element = TryNavigateJsonPathWithAliases(doc.RootElement, effectiveJsonPath, out var aliasMatchedPath);
                    if (element.HasValue)
                    {
                        effectiveJsonPath = aliasMatchedPath;
                    }
                }
                var matchedJsonPath = element.HasValue ? effectiveJsonPath : null;
                var actualValue = element?.ToString();
                var matchedByIdentifierAlias = false;
                var wildcardMatched = false;
                var valuesMatch = ValuesEqual(actualValue, resolvedExpected, element);

                if (valuesMatch
                    && pathResolution.IsResolved
                    && !string.Equals(pathResolution.OriginalPath, pathResolution.ResolvedPath, StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "JSONPATH_PATH_NORMALIZED",
                        Message = $"JSONPath expected '{pathResolution.OriginalPath}' normalized to actual '{pathResolution.ResolvedPath}'. Source={pathResolution.Source}; strategy={pathResolution.ResolutionStrategy}.",
                        Target = pathResolution.ResolvedPath,
                    });
                }

                if (!valuesMatch
                    && check.Value.ValueKind == JsonValueKind.String
                    && TryResolveIdentifierAliasExpectedValue(effectiveJsonPath, check.Value.GetString(), variableBag, actualValue, out var aliasMatchedExpected))
                {
                    resolvedExpected = aliasMatchedExpected;
                    matchedByIdentifierAlias = true;
                    valuesMatch = ValuesEqual(actualValue, resolvedExpected, element);
                }

                if (!valuesMatch
                    && TryEvaluateWildcardJsonPathCheck(doc.RootElement, effectiveJsonPath, resolvedExpected, out var wildcardActualPreview))
                {
                    wildcardMatched = true;
                    actualValue = wildcardActualPreview;
                    valuesMatch = true;
                }

                if (!wildcardMatched && !valuesMatch)
                {
                    var hasUnresolvedPlaceholder =
                        !string.IsNullOrWhiteSpace(resolvedExpected)
                        && resolvedExpected.Contains("{{", StringComparison.Ordinal)
                        && resolvedExpected.Contains("}}", StringComparison.Ordinal);

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

                    var shouldDowngradeMessageMismatch =
                        ShouldDowngradeJsonPathMismatchToWarning(
                            effectiveJsonPath,
                            resolvedExpected,
                            actualValue,
                            result.StatusCodeMatched,
                            ResolveJsonPathLeniencyMode(testCase, profile));
                    var shouldDowngradeIndexedCollectionGap =
                        ShouldDowngradeIndexedCollectionGapToWarning(
                            effectiveJsonPath,
                            actualValue,
                            result.StatusCodeMatched,
                            ResolveJsonPathLeniencyMode(testCase, profile));

                    // If expected still contains unresolved template variables but actual has value,
                    // don't hard-fail: this usually indicates variable extraction/template timing issue,
                    // not a true API behavior mismatch.
                    if (hasUnresolvedPlaceholder && actualValue != null)
                    {
                        result.Warnings.Add(new ValidationWarningModel
                        {
                            Code = "JSONPATH_UNRESOLVED_PLACEHOLDER",
                            Message = $"JSONPath '{check.Key}' cÃ²n placeholder chÆ°a resolve trong expected ('{resolvedExpected}'), nhÆ°ng actual cÃ³ giÃ¡ trá»‹ nÃªn háº¡ xuá»‘ng warning.",
                            Target = check.Key,
                        });
                    }
                    else
                    if (canSoftForgive)
                    {
                        result.Warnings.Add(new ValidationWarningModel
                        {
                            Code = "JSONPATH_PARTIAL_MISMATCH",
                            Message = $"JSONPath '{check.Key}' không tìm thấy, nhưng được bỏ qua vì test Negative/Boundary đã khớp status code đúng.",
                            Target = check.Key,
                        });
                    }
                    else if (shouldDowngradeMessageMismatch)
                    {
                        result.Warnings.Add(new ValidationWarningModel
                        {
                            Code = "JSONPATH_MESSAGE_MISMATCH_WARNING",
                            Message = $"JSONPath '{check.Key}' không khớp exact text (mong đợi: '{resolvedExpected}', thực tế: '{actualValue ?? "(null)"}'). Đã hạ severity xuống warning vì status code đúng.",
                            Target = check.Key,
                        });
                    }
                    else if (shouldDowngradeIndexedCollectionGap)
                    {
                        result.Warnings.Add(new ValidationWarningModel
                        {
                            Code = "JSONPATH_INDEXED_COLLECTION_EMPTY_WARNING",
                            Message = $"JSONPath '{check.Key}' trỏ vào phần tử theo index nhưng collection không có dữ liệu phù hợp ở lần chạy này. Đã hạ xuống warning.",
                            Target = check.Key,
                        });
                    }
                    else if (!isAuthoritative)

                    {

                        result.Warnings.Add(new ValidationWarningModel

                        {

                            Code = "JSONPATH_INFERRED_ASSERTION_WARNING",

                            Message = $"JSONPath '{check.Key}' did not match, but this expectation is AI-inferred/unverified and was not used as a hard gate. Expected: '{resolvedExpected}', actual: '{actualValue ?? "(null)"}'.",

                            Target = matchedJsonPath ?? check.Key,

                        });

                    }

                    else

                    {

                        allPassed = false;

                        result.Failures.Add(new ValidationFailureModel

                        {

                            Code = "JSONPATH_ASSERTION_FAILED",
                            Message = isExistenceCheck
                                ? $"JSONPath '{check.Key}' phải tồn tại nhưng không tìm thấy trong response. {FormatJsonPathResolutionDiagnostics(pathResolution)}"
                                : $"JSONPath '{check.Key}' không khớp. Mong đợi: '{resolvedExpected}', thực tế: '{actualValue ?? "(null)"}'. {FormatJsonPathResolutionDiagnostics(pathResolution)}",
                            Target = matchedJsonPath ?? check.Key,
                            Expected = resolvedExpected,
                            Actual = actualValue,
                        });
                    }
                }
                else if (matchedByIdentifierAlias)
                {
                    result.Warnings.Add(new ValidationWarningModel
                    {
                        Code = "JSONPATH_ALIAS_MATCH",
                        Message = $"JSONPath '{check.Key}' matched by identifier alias value.",
                        Target = matchedJsonPath ?? check.Key,
                    });
                }
            }
        }

        result.JsonPathChecksPassed = allPassed;
        return true;
    }

    private static IReadOnlyCollection<string> BuildJsonPathResolutionSchemas(
        ExecutionTestCaseExpectationDto expectation,
        ApiEndpointMetadataDto endpointMetadata)
    {
        var schemas = new List<string>();
        if (!string.IsNullOrWhiteSpace(expectation?.ResponseSchema))
        {
            schemas.Add(expectation.ResponseSchema);
        }

        if (endpointMetadata?.ResponseSchemaPayloads != null)
        {
            schemas.AddRange(endpointMetadata.ResponseSchemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return schemas;
    }

    private static string FormatJsonPathResolutionDiagnostics(JsonPathResolutionResult resolution)
    {
        if (resolution == null)
        {
            return string.Empty;
        }

        var diagnostics = resolution.Diagnostics?.Count > 0
            ? string.Join(" ", resolution.Diagnostics)
            : "No resolver diagnostic was produced.";
        var candidates = resolution.CandidatePaths?.Count > 0
            ? $" Candidate paths: {string.Join(", ", resolution.CandidatePaths.Take(5).Select(x => $"{x.Path}({x.Source}:{x.Score})"))}."
            : string.Empty;

        return $"Resolver: original='{resolution.OriginalPath}', resolved='{resolution.ResolvedPath ?? "(none)"}', source='{resolution.Source}', strategy='{resolution.ResolutionStrategy}', confidence={resolution.Confidence:0.##}. {diagnostics}{candidates}";
    }

    private static bool TryEvaluateWildcardJsonPathCheck(
        JsonElement root,
        string jsonPath,
        string expected,
        out string actualPreview)
    {
        actualPreview = null;
        if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var marker = "[*].";
        var markerIndex = jsonPath.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return false;
        }

        var arrayPath = jsonPath[..(markerIndex + 3)];
        var leaf = jsonPath[(markerIndex + marker.Length)..];
        if (string.IsNullOrWhiteSpace(leaf))
        {
            return false;
        }

        var arrayElement = VariableExtractor.NavigateJsonPath(root, arrayPath);
        if (!arrayElement.HasValue)
        {
            return false;
        }

        var values = new List<string>();
        if (arrayElement.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arrayElement.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty(leaf, out var prop)
                    && prop.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    var text = prop.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        values.Add(text);
                    }
                }
            }
        }
        else if (arrayElement.Value.ValueKind == JsonValueKind.Object
            && arrayElement.Value.TryGetProperty(leaf, out var singleProp)
            && singleProp.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            // Tolerate schema drift where API returns object instead of array for the same semantic payload.
            var text = singleProp.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }
        }
        else
        {
            return false;
        }

        if (values.Count == 0)
        {
            return false;
        }

        actualPreview = string.Join(", ", values.Take(5));

        if (TryParseExpectationOperator(expected, out var op, out var operand)
            && string.Equals(op, "array_contains", StringComparison.OrdinalIgnoreCase))
        {
            return values.Any(v => ValuesEqual(v, operand));
        }

        // Fallback: check whether any value matches the same expected semantic.
        return values.Any(v => ValuesEqual(v, expected));
    }

    private static bool ShouldDowngradeJsonPathMismatchToWarning(
        string jsonPath,
        string expected,
        string actual,
        bool? statusCodeMatched,
        JsonPathLeniencyMode leniencyMode)
    {
        if (statusCodeMatched != true)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        var normalizedPath = jsonPath.Trim().ToLowerInvariant();
        if (leniencyMode == JsonPathLeniencyMode.Strict)
        {
            return false;
        }

        // Downgrade only text-like mismatches. Keep structural/semantic checks strict.
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        if (LooksLikeExistenceExpectation(expected)
            || LooksLikeNonEmptyExpectation(expected)
            || LooksLikeStringExpectation(expected)
            || LooksLikeBooleanExpectation(expected)
            || LooksLikeNumberExpectation(expected)
            || LooksLikeArrayExpectation(expected)
            || LooksLikeObjectExpectation(expected)
            || LooksLikeDateTimeExpectation(expected)
            || LooksLikeGuidExpectation(expected)
            || LooksLikeRegexPattern(expected))
        {
            return false;
        }

        // Loose mode: nearly all non-critical textual mismatches become warning.
        if (leniencyMode == JsonPathLeniencyMode.Loose)
        {
            return true;
        }

        // Balanced mode: keep current conservative downgrade behavior.
        return IsMessageLikeJsonPath(normalizedPath)
            || IsDescriptionLikeJsonPath(normalizedPath);
    }

    private static bool ShouldDowngradeIndexedCollectionGapToWarning(
        string jsonPath,
        string actual,
        bool? statusCodeMatched,
        JsonPathLeniencyMode leniencyMode)
    {
        if (statusCodeMatched != true || leniencyMode == JsonPathLeniencyMode.Strict)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        // Generic rule: path uses explicit numeric index like $.data.items[0].id
        // and target is missing/null for this run.
        return Regex.IsMatch(jsonPath, @"\[\d+\]");
    }

    private static bool IsMessageLikeJsonPath(string normalizedPath)
    {
        return normalizedPath == "$.message"
            || normalizedPath.EndsWith(".message", StringComparison.Ordinal)
            || normalizedPath.Contains("[\"message\"]", StringComparison.Ordinal);
    }

    private static bool IsDescriptionLikeJsonPath(string normalizedPath)
    {
        return normalizedPath.EndsWith(".description", StringComparison.Ordinal)
            || normalizedPath.EndsWith(".detail", StringComparison.Ordinal)
            || normalizedPath.EndsWith(".details", StringComparison.Ordinal)
            || normalizedPath.EndsWith(".reason", StringComparison.Ordinal)
            || normalizedPath.EndsWith(".title", StringComparison.Ordinal);
    }

    private static JsonPathLeniencyMode ResolveJsonPathLeniencyMode(
        ExecutionTestCaseDto testCase,
        ValidationProfile profile)
    {
        // Optional override per test case via tag:
        // jsonpath-mode:strict|balanced|loose
        if (!string.IsNullOrWhiteSpace(testCase?.Tags))
        {
            try
            {
                var tags = JsonSerializer.Deserialize<string[]>(testCase.Tags) ?? Array.Empty<string>();
                foreach (var rawTag in tags)
                {
                    var tag = rawTag?.Trim();
                    if (string.IsNullOrWhiteSpace(tag) ||
                        !tag.StartsWith("jsonpath-mode:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var mode = tag["jsonpath-mode:".Length..].Trim().ToLowerInvariant();
                    return mode switch
                    {
                        "strict" => JsonPathLeniencyMode.Strict,
                        "loose" => JsonPathLeniencyMode.Loose,
                        _ => JsonPathLeniencyMode.Balanced,
                    };
                }
            }
            catch
            {
                // Ignore malformed tags; use profile mapping.
            }
        }

        return profile switch
        {
            ValidationProfile.SrsStrict => JsonPathLeniencyMode.Strict,
            ValidationProfile.DemoAdaptive => JsonPathLeniencyMode.Loose,
            _ => JsonPathLeniencyMode.Balanced,
        };
    }

    private enum JsonPathLeniencyMode
    {
        Strict = 0,
        Balanced = 1,
        Loose = 2,
    }

    private static JsonElement? TryNavigateJsonPathWithAliases(
        JsonElement root,
        string jsonPath,
        out string matchedJsonPath)
    {
        matchedJsonPath = jsonPath;
        var direct = VariableExtractor.NavigateJsonPath(root, jsonPath);
        if (direct.HasValue)
        {
            return direct;
        }

        foreach (var alias in BuildJsonPathAliases(jsonPath))
        {
            var candidate = VariableExtractor.NavigateJsonPath(root, alias);
            if (candidate.HasValue)
            {
                matchedJsonPath = alias;
                return candidate;
            }
        }

        // Last-resort semantic leaf matching for cross-project naming drift:
        // e.g. trackId vs trackID vs track_id vs storeID.
        if (TryNavigateJsonPathBySemanticLeaf(root, jsonPath, out var semanticPath, out var semanticElement))
        {
            matchedJsonPath = semanticPath;
            return semanticElement;
        }

        return null;
    }

    private static IEnumerable<string> BuildJsonPathAliases(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            yield break;
        }

        var path = jsonPath.Trim();
        var lastDot = path.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= path.Length - 1)
        {
            yield break;
        }

        var prefix = path[..lastDot];
        var leaf = path[(lastDot + 1)..];
        if (string.IsNullOrWhiteSpace(leaf))
        {
            yield break;
        }

        // Common API response aliasing: category -> categoryId, product -> productId, etc.
        if (!leaf.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{prefix}.{leaf}Id";
        }

        // Cross-style aliases for ID-like fields: trackId, trackID, track_id, track-id.
        var normalizedLeaf = NormalizePathToken(leaf);
        if (normalizedLeaf.EndsWith("id", StringComparison.Ordinal))
        {
            var baseName = normalizedLeaf[..^2];
            if (!string.IsNullOrWhiteSpace(baseName))
            {
                yield return $"{prefix}.{baseName}Id";
                yield return $"{prefix}.{baseName}ID";
                yield return $"{prefix}.{baseName}_id";
                yield return $"{prefix}.{baseName}-id";
            }
        }
    }

    private static bool TryNavigateJsonPathBySemanticLeaf(
        JsonElement root,
        string jsonPath,
        out string matchedPath,
        out JsonElement matchedElement)
    {
        matchedPath = jsonPath;
        matchedElement = default;

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        var path = jsonPath.Trim();
        var lastDot = path.LastIndexOf('.');
        if (lastDot <= 1 || lastDot >= path.Length - 1)
        {
            return false;
        }

        var prefix = path[..lastDot];
        var leaf = path[(lastDot + 1)..];
        if (string.IsNullOrWhiteSpace(leaf))
        {
            return false;
        }

        var parent = VariableExtractor.NavigateJsonPath(root, prefix);
        if (!parent.HasValue || parent.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var expectedLeaf = NormalizePathToken(leaf);
        foreach (var prop in parent.Value.EnumerateObject())
        {
            if (!string.Equals(NormalizePathToken(prop.Name), expectedLeaf, StringComparison.Ordinal))
            {
                continue;
            }

            matchedPath = $"{prefix}.{prop.Name}";
            matchedElement = prop.Value;
            return true;
        }

        return false;
    }

    private static string ResolveExpectedFromRequestIfNeeded(
        string expected,
        ExecutionTestCaseRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            !expected.Contains("{{", StringComparison.Ordinal))
        {
            return expected;
        }

        var requestValues = ExtractRequestPrimitiveValues(request);
        if (requestValues.Count == 0)
        {
            return expected;
        }

        return Regex.Replace(expected, @"\{\{(\w+)\}\}", m =>
        {
            var key = m.Groups[1].Value;
            var normalizedKey = NormalizeVariableKey(key);
            foreach (var kv in requestValues)
            {
                if (!string.Equals(NormalizeVariableKey(kv.Key), normalizedKey, StringComparison.Ordinal))
                {
                    continue;
                }

                return kv.Value ?? m.Value;
            }

            return m.Value;
        });
    }

    private static Dictionary<string, string> ExtractRequestPrimitiveValues(ExecutionTestCaseRequestDto request)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request == null)
        {
            return result;
        }

        MergePrimitiveObjectJson(result, request.Body);
        MergePrimitiveObjectJson(result, request.PathParams);
        MergePrimitiveObjectJson(result, request.QueryParams);
        MergePrimitiveObjectJson(result, request.Headers);

        return result;
    }

    private static void MergePrimitiveObjectJson(Dictionary<string, string> target, string jsonText)
    {
        if (target == null || string.IsNullOrWhiteSpace(jsonText))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.ToString(),
                    _ => null,
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    target[property.Name] = value;
                }
            }
        }
        catch
        {
            // Ignore malformed request fragments; keep expected unchanged.
        }
    }

    private static bool TryResolveVariableValue(
        IReadOnlyDictionary<string, string> variableBag,
        string key,
        out string value)
    {
        value = null;
        if (variableBag == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (variableBag.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalizedKey = NormalizeVariableKey(key);
        foreach (var kv in variableBag)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
            {
                continue;
            }

            if (string.Equals(NormalizeVariableKey(kv.Key), normalizedKey, StringComparison.Ordinal))
            {
                value = kv.Value;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeVariableKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return Regex.Replace(key.Trim().ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
    }

    private static string NormalizePathToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return Regex.Replace(token.Trim().ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
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
        if (TryEvaluateCanonicalAssertion(expected, actual, actualElement, out var canonicalResult))
        {
            return canonicalResult;
        }

        if (TryEvaluateFunctionStyleExpectation(expected, actual, actualElement, out var functionStyleResult))
        {
            return functionStyleResult;
        }

        if (TryParseExpectationOperator(expected, out var op, out var operand))
        {
            if (string.Equals(op, "array_contains", StringComparison.OrdinalIgnoreCase))
            {
                if (!actualElement.HasValue || actualElement.Value.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var item in actualElement.Value.EnumerateArray())
                {
                    var itemText = item.ToString();
                    if (ValuesEqual(itemText, operand, item))
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(itemText)
                        && !string.IsNullOrEmpty(operand)
                        && itemText.Contains(operand, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (string.Equals(op, "contains", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(actual)
                    && !string.IsNullOrEmpty(operand)
                    && actual.Contains(operand, StringComparison.OrdinalIgnoreCase);
            }
        }

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

        if (TryEvaluateLengthFunctionExpectation(expected, actual, actualElement, out var lengthResult))
        {
            return lengthResult;
        }

        if (TryEvaluateNumericComparatorExpectation(expected, actual, out var numericComparatorResult))
        {
            return numericComparatorResult;
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
            if (!actualElement.HasValue || actualElement.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var raw = actualElement.Value.GetString();
            if (LooksLikeObjectIdExpectation(expected))
            {
                return IsMongoObjectId(raw);
            }

            return actualElement.HasValue
                && Guid.TryParse(raw, out _);
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

        // Try numeric comparison
        if (decimal.TryParse(actual, out var actualNum) && decimal.TryParse(expected, out var expectedNum))
        {
            return Math.Abs(actualNum - expectedNum) <= 0.000001m;
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

        if (AreSemanticErrorCodesEquivalent(expected, actual))
        {
            return true;
        }

        // Date/time semantic equality: compare parsed instants with tolerance,
        // so "2026-01-01T00:00:00Z" and "2026-01-01T07:00:00+07:00" pass.
        if (TryParseDateTimeOffset(actual, out var actualDateTime) &&
            TryParseDateTimeOffset(expected, out var expectedDateTime))
        {
            var delta = (actualDateTime - expectedDateTime).Duration();
            return delta <= SemanticDateTimeTolerance;
        }

        // Regex check: LLMs sometimes generate `{"regex":"^pattern$"}` (or legacy `{"match":"^pattern$"}`)
        // as the expected value to describe a structural assertion (e.g. JWT format, UUID, email).
        // Extract the pattern and test it against actual.
        if (expected.StartsWith("{", StringComparison.Ordinal) &&
            (expected.Contains("\"regex\"", StringComparison.Ordinal) || expected.Contains("\"match\"", StringComparison.Ordinal)))
        {
            try
            {
                var regexDoc = JsonDocument.Parse(expected);
                if (regexDoc.RootElement.TryGetProperty("regex", out var patternEl) ||
                    regexDoc.RootElement.TryGetProperty("match", out patternEl))
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
                if (Regex.IsMatch(actual, inlinePattern, RegexOptions.None, TimeSpan.FromMilliseconds(500)))
                {
                    return true;
                }

                if (actualElement.HasValue && TryMatchRegexAgainstElement(actualElement.Value, inlinePattern, RegexOptions.None))
                {
                    return true;
                }

                // Some LLM/n8n pipelines over-escape regex strings (e.g. "\\\\." instead of "\\.").
                // Try once with a normalized pattern before failing hard.
                var normalizedInlinePattern = NormalizePossiblyOverEscapedRegex(inlinePattern);
                if (!string.Equals(normalizedInlinePattern, inlinePattern, StringComparison.Ordinal))
                {
                    if (Regex.IsMatch(actual, normalizedInlinePattern, RegexOptions.None, TimeSpan.FromMilliseconds(500)))
                    {
                        return true;
                    }

                    if (actualElement.HasValue && TryMatchRegexAgainstElement(actualElement.Value, normalizedInlinePattern, RegexOptions.None))
                    {
                        return true;
                    }
                }

                return false;
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
                var pattern = expected;
                var options = RegexOptions.None;
                if (TryParseSlashDelimitedRegex(expected, out var slashPattern, out var slashOptions))
                {
                    pattern = slashPattern;
                    options = slashOptions;
                }

                if (Regex.IsMatch(actual, pattern, options, TimeSpan.FromMilliseconds(500)))
                {
                    return true;
                }

                if (actualElement.HasValue && TryMatchRegexAgainstElement(actualElement.Value, pattern, options))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // Invalid regex — fall through to literal comparison.
            }
        }

        // Semantic JSON equality:
        // - object: expected is subset of actual (extra actual fields are allowed)
        // - array: order-insensitive multiset compare
        // - number: tolerance compare
        if (TrySemanticJsonEquality(actual, expected, out var jsonSemanticEqual))
        {
            return jsonSemanticEqual;
        }

        // Text semantic normalization fallback.
        var normalizedActual = NormalizeSemanticText(actual);
        var normalizedExpected = NormalizeSemanticText(expected);
        if (!string.IsNullOrWhiteSpace(normalizedActual) &&
            !string.IsNullOrWhiteSpace(normalizedExpected) &&
            string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static bool TryEvaluateFunctionStyleExpectation(
        string expected,
        string actual,
        JsonElement? actualElement,
        out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = expected.Trim();
        if (!text.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var open = text.IndexOf('(');
        if (open <= 0)
        {
            return false;
        }

        var fn = text[..open].Trim().ToLowerInvariant();
        var argRaw = text[(open + 1)..^1].Trim();
        var arg = TrimContainerSyntax(argRaw);

        switch (fn)
        {
            case "contains":
                if (actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in actualElement.Value.EnumerateArray())
                    {
                        var itemText = item.ToString();
                        if (ValuesEqual(itemText, arg, item))
                        {
                            result = true;
                            return true;
                        }

                        if (!string.IsNullOrEmpty(itemText) &&
                            !string.IsNullOrEmpty(arg) &&
                            NormalizeSemanticText(itemText).Contains(NormalizeSemanticText(arg), StringComparison.OrdinalIgnoreCase))
                        {
                            result = true;
                            return true;
                        }
                    }

                    result = false;
                    return true;
                }

                result = !string.IsNullOrEmpty(actual) &&
                    !string.IsNullOrEmpty(arg) &&
                    NormalizeSemanticText(actual).Contains(NormalizeSemanticText(arg), StringComparison.OrdinalIgnoreCase);
                return true;

            case "notcontains":
            case "not_contains":
                if (actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Array)
                {
                    result = !actualElement.Value.EnumerateArray().Any(x =>
                    {
                        var itemText = x.ToString();
                        return ValuesEqual(itemText, arg, x)
                            || (!string.IsNullOrEmpty(itemText)
                                && !string.IsNullOrEmpty(arg)
                                && NormalizeSemanticText(itemText).Contains(NormalizeSemanticText(arg), StringComparison.OrdinalIgnoreCase));
                    });
                    return true;
                }

                result = string.IsNullOrEmpty(actual) ||
                    string.IsNullOrEmpty(arg) ||
                    !NormalizeSemanticText(actual).Contains(NormalizeSemanticText(arg), StringComparison.OrdinalIgnoreCase);
                return true;
        }

        return false;
    }

    private static string TrimContainerSyntax(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var s = value.Trim().Trim('"', '\'');
        // tolerate LLM forms like contains([{{trackId}}]) or contains(["abc"])
        if (s.Length >= 2 && s[0] == '[' && s[^1] == ']')
        {
            s = s[1..^1].Trim().Trim('"', '\'');
        }

        return s;
    }

    private static bool AreSemanticErrorCodesEquivalent(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        var normalizedExpected = NormalizeErrorCodeToken(expected);
        var normalizedActual = NormalizeErrorCodeToken(actual);
        if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedActual))
        {
            return false;
        }

        if (normalizedExpected == normalizedActual)
        {
            return true;
        }

        // Generic -> specific mapping for validation domain
        if (normalizedExpected == "VALIDATION_ERROR")
        {
            return normalizedActual.Contains("INVALID", StringComparison.Ordinal)
                || normalizedActual.Contains("MISSING", StringComparison.Ordinal)
                || normalizedActual.Contains("REQUIRED", StringComparison.Ordinal)
                || normalizedActual.Contains("EXISTS", StringComparison.Ordinal)
                || normalizedActual.Contains("DUPLICATE", StringComparison.Ordinal)
                || normalizedActual.Contains("FORMAT", StringComparison.Ordinal)
                || normalizedActual.Contains("CONFLICT", StringComparison.Ordinal)
                || normalizedActual == "EMAIL_EXISTS";
        }

        return false;
    }

    private static string NormalizeErrorCodeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var token = value.Trim().Trim('"', '\'');
        return Regex.Replace(token.ToUpperInvariant(), @"[^A-Z0-9_]+", "_").Trim('_');
    }

    private static bool TrySemanticJsonEquality(string actual, string expected, out bool equals)
    {
        equals = false;
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        try
        {
            using var actualDoc = JsonDocument.Parse(actual);
            using var expectedDoc = JsonDocument.Parse(expected);
            equals = JsonSemanticallyMatches(actualDoc.RootElement, expectedDoc.RootElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool JsonSemanticallyMatches(JsonElement actual, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            return actual.ValueKind == JsonValueKind.Null;
        }

        if (actual.ValueKind == JsonValueKind.Number && expected.ValueKind == JsonValueKind.Number)
        {
            if (actual.TryGetDecimal(out var a) && expected.TryGetDecimal(out var e))
            {
                return Math.Abs(a - e) <= 0.000001m;
            }
        }

        if (actual.ValueKind == JsonValueKind.String && expected.ValueKind == JsonValueKind.String)
        {
            var a = actual.GetString();
            var e = expected.GetString();
            if (TryParseDateTimeOffset(a, out var ad) && TryParseDateTimeOffset(e, out var ed))
            {
                return (ad - ed).Duration() <= SemanticDateTimeTolerance;
            }

            return string.Equals(NormalizeSemanticText(a), NormalizeSemanticText(e), StringComparison.OrdinalIgnoreCase);
        }

        if (expected.ValueKind == JsonValueKind.Object && actual.ValueKind == JsonValueKind.Object)
        {
            foreach (var ep in expected.EnumerateObject())
            {
                if (!TryGetPropertyCaseInsensitive(actual, ep.Name, out var ap))
                {
                    return false;
                }

                if (!JsonSemanticallyMatches(ap, ep.Value))
                {
                    return false;
                }
            }

            return true;
        }

        if (expected.ValueKind == JsonValueKind.Array && actual.ValueKind == JsonValueKind.Array)
        {
            var actualItems = actual.EnumerateArray().ToList();
            var expectedItems = expected.EnumerateArray().ToList();
            if (actualItems.Count != expectedItems.Count)
            {
                return false;
            }

            var used = new bool[actualItems.Count];
            foreach (var expectedItem in expectedItems)
            {
                var matched = false;
                for (var i = 0; i < actualItems.Count; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }

                    if (!JsonSemanticallyMatches(actualItems[i], expectedItem))
                    {
                        continue;
                    }

                    used[i] = true;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryParseDateTimeOffset(string value, out DateTimeOffset dto)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            dto = default;
            return false;
        }

        return DateTimeOffset.TryParse(value.Trim(), out dto);
    }

    private static string NormalizeSemanticText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.Trim().ToLowerInvariant();
        return Regex.Replace(lowered, @"\s+", " ");
    }

    private static bool TryEvaluateCanonicalAssertion(
        string expected,
        string actual,
        JsonElement? actualElement,
        out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var trimmed = expected.Trim();
        if (!(trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal)))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            result = EvaluateCanonicalAssertionNode(doc.RootElement, actual, actualElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateCanonicalAssertionNode(JsonElement node, string actual, JsonElement? actualElement)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("op", out var opElement))
        {
            return false;
        }

        var op = opElement.GetString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(op))
        {
            return false;
        }

        switch (op)
        {
            case "exists":
                return actualElement.HasValue;
            case "not_exists":
                return !actualElement.HasValue;
            case "eq":
                return node.TryGetProperty("value", out var eqValue)
                    && ValuesEqual(actual, eqValue.ValueKind == JsonValueKind.String ? eqValue.GetString() : eqValue.GetRawText(), actualElement);
            case "neq":
                return node.TryGetProperty("value", out var neqValue)
                    && !ValuesEqual(actual, neqValue.ValueKind == JsonValueKind.String ? neqValue.GetString() : neqValue.GetRawText(), actualElement);
            case "contains":
                return node.TryGetProperty("value", out var containsValue)
                    && !string.IsNullOrEmpty(actual)
                    && actual.Contains(containsValue.ToString(), StringComparison.OrdinalIgnoreCase);
            case "array_contains":
                return node.TryGetProperty("value", out var arrayContainsValue)
                    && EvaluateArrayContains(actualElement, actual, arrayContainsValue.ToString());
            case "not_contains":
                return node.TryGetProperty("value", out var notContainsValue)
                    && (string.IsNullOrEmpty(actual) || !actual.Contains(notContainsValue.ToString(), StringComparison.OrdinalIgnoreCase));
            case "regex":
                if (!node.TryGetProperty("value", out var regexValue))
                {
                    return false;
                }

                var pattern = regexValue.ToString();
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return false;
                }

                return (!string.IsNullOrEmpty(actual) && Regex.IsMatch(actual, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(500)))
                    || (actualElement.HasValue && TryMatchRegexAgainstElement(actualElement.Value, pattern, RegexOptions.None));
            case "type":
                if (!node.TryGetProperty("value", out var typeValue) || !actualElement.HasValue)
                {
                    return false;
                }

                return TypeMatches(actualElement.Value, typeValue.ToString());
            case "in":
                return node.TryGetProperty("value", out var inValue)
                    && inValue.ValueKind == JsonValueKind.Array
                    && inValue.EnumerateArray().Any(v => ValuesEqual(actual, v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(), actualElement));
            case "not_in":
                return node.TryGetProperty("value", out var notInValue)
                    && notInValue.ValueKind == JsonValueKind.Array
                    && !notInValue.EnumerateArray().Any(v => ValuesEqual(actual, v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(), actualElement));
            case "length":
                return EvaluateLengthOp(node, actual, actualElement);
            case "numeric":
                return EvaluateNumericOp(node, actual);
            case "datetime":
                return EvaluateDateTimeOp(node, actual, actualElement);
            case "any_of":
                return node.TryGetProperty("value", out var anyOf)
                    && anyOf.ValueKind == JsonValueKind.Array
                    && anyOf.EnumerateArray().Any(x => EvaluateCanonicalAssertionNode(x, actual, actualElement));
            case "all_of":
                return node.TryGetProperty("value", out var allOf)
                    && allOf.ValueKind == JsonValueKind.Array
                    && allOf.EnumerateArray().All(x => EvaluateCanonicalAssertionNode(x, actual, actualElement));
            default:
                return false;
        }
    }

    private static bool TypeMatches(JsonElement element, string expectedType)
    {
        var normalized = expectedType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => false,
        };
    }

    private static bool EvaluateLengthOp(JsonElement node, string actual, JsonElement? actualElement)
    {
        var length = actual?.Length ?? 0;
        if (actualElement.HasValue)
        {
            if (actualElement.Value.ValueKind == JsonValueKind.Array)
            {
                length = actualElement.Value.GetArrayLength();
            }
            else if (actualElement.Value.ValueKind == JsonValueKind.String)
            {
                length = actualElement.Value.GetString()?.Length ?? 0;
            }
        }

        if (node.TryGetProperty("eq", out var eq) && eq.TryGetInt32(out var eqLen) && length != eqLen)
        {
            return false;
        }

        if (node.TryGetProperty("min", out var min) && min.TryGetInt32(out var minLen) && length < minLen)
        {
            return false;
        }

        if (node.TryGetProperty("max", out var max) && max.TryGetInt32(out var maxLen) && length > maxLen)
        {
            return false;
        }

        return true;
    }

    private static bool EvaluateNumericOp(JsonElement node, string actual)
    {
        if (!decimal.TryParse(actual, out var value))
        {
            return false;
        }

        if (node.TryGetProperty("eq", out var eq) && eq.TryGetDecimal(out var eqNum) && value != eqNum)
        {
            return false;
        }

        if (node.TryGetProperty("min", out var min) && min.TryGetDecimal(out var minNum) && value < minNum)
        {
            return false;
        }

        if (node.TryGetProperty("max", out var max) && max.TryGetDecimal(out var maxNum) && value > maxNum)
        {
            return false;
        }

        if (node.TryGetProperty("gt", out var gt) && gt.TryGetDecimal(out var gtNum) && value <= gtNum)
        {
            return false;
        }

        if (node.TryGetProperty("gte", out var gte) && gte.TryGetDecimal(out var gteNum) && value < gteNum)
        {
            return false;
        }

        if (node.TryGetProperty("lt", out var lt) && lt.TryGetDecimal(out var ltNum) && value >= ltNum)
        {
            return false;
        }

        if (node.TryGetProperty("lte", out var lte) && lte.TryGetDecimal(out var lteNum) && value > lteNum)
        {
            return false;
        }

        return true;
    }

    private static bool TryMatchRegexAgainstElement(JsonElement element, string pattern, RegexOptions options)
    {
        var candidates = EnumeratePrimitiveStrings(element);
        foreach (var candidate in candidates)
        {
            if (Regex.IsMatch(candidate, pattern, options, TimeSpan.FromMilliseconds(500)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumeratePrimitiveStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var s = element.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    yield return s;
                }

                yield break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                yield return element.ToString();
                yield break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in EnumeratePrimitiveStrings(item))
                    {
                        yield return nested;
                    }
                }

                yield break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var nested in EnumeratePrimitiveStrings(property.Value))
                    {
                        yield return nested;
                    }
                }

                yield break;

            default:
                yield break;
        }
    }

    private static bool TryParseExpectationOperator(string expected, out string op, out string operand)
    {
        op = null;
        operand = null;

        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = expected.Trim();
        var separatorIndex = text.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= text.Length - 1)
        {
            return false;
        }

        op = text[..separatorIndex].Trim();
        operand = text[(separatorIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(op) && operand != null;
    }

    private static bool LooksLikeExistenceExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = expected.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(text, @"[^a-z0-9]+", string.Empty);
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
        var normalized = Regex.Replace(text, @"[^a-z0-9]+", string.Empty);
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

    private static bool LooksLikeObjectIdExpectation(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var text = NormalizeTypeToken(expected);
        return text.Contains("objectid", StringComparison.Ordinal)
            || text.Contains("object id", StringComparison.Ordinal)
            || text.Contains("mongo id", StringComparison.Ordinal);
    }

    private static bool IsMongoObjectId(string value) =>
        !string.IsNullOrWhiteSpace(value) && ObjectIdRegex.IsMatch(value);

    private static bool TryEvaluateLengthFunctionExpectation(
        string expected,
        string actual,
        JsonElement? actualElement,
        out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var match = LengthFunctionRegex.Match(expected.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var expectedLength))
        {
            return false;
        }

        int actualLength;
        if (actualElement.HasValue)
        {
            actualLength = actualElement.Value.ValueKind switch
            {
                JsonValueKind.String => actualElement.Value.GetString()?.Length ?? 0,
                JsonValueKind.Array => actualElement.Value.GetArrayLength(),
                JsonValueKind.Object => actualElement.Value.EnumerateObject().Count(),
                JsonValueKind.Null or JsonValueKind.Undefined => 0,
                _ => actual?.Length ?? 0,
            };
        }
        else
        {
            actualLength = actual?.Length ?? 0;
        }

        result = actualLength == expectedLength;
        return true;
    }

    private static bool EvaluateArrayContains(JsonElement? actualElement, string actual, string expectedValue)
    {
        if (string.IsNullOrWhiteSpace(expectedValue))
        {
            return false;
        }

        if (actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in actualElement.Value.EnumerateArray())
            {
                var itemText = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.GetRawText();

                if (string.Equals(itemText, expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Fallback for stringified arrays or non-array values.
        return !string.IsNullOrEmpty(actual)
            && actual.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateDateTimeOp(JsonElement node, string actual, JsonElement? actualElement)
    {
        var actualText = actual;
        if (string.IsNullOrWhiteSpace(actualText) && actualElement.HasValue && actualElement.Value.ValueKind == JsonValueKind.String)
        {
            actualText = actualElement.Value.GetString();
        }

        if (!TryParseDateTimeOffset(actualText, out var actualDt))
        {
            return false;
        }

        if (!node.TryGetProperty("value", out var expectedNode))
        {
            // If no explicit value, only assert "is valid datetime"
            return true;
        }

        var expectedText = expectedNode.ToString();
        if (!TryParseDateTimeOffset(expectedText, out var expectedDt))
        {
            return false;
        }

        var toleranceSeconds = 0d;
        if (node.TryGetProperty("withinSeconds", out var withinSeconds) && withinSeconds.TryGetDouble(out var ws))
        {
            toleranceSeconds = Math.Max(0d, ws);
        }
        else
        {
            toleranceSeconds = SemanticDateTimeTolerance.TotalSeconds;
        }

        return (actualDt - expectedDt).Duration() <= TimeSpan.FromSeconds(toleranceSeconds);
    }

    private static bool TryEvaluateNumericComparatorExpectation(
        string expected,
        string actual,
        out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var match = NumericComparatorRegex.Match(expected.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!decimal.TryParse(actual, out var actualNum) ||
            !decimal.TryParse(match.Groups[2].Value, out var expectedNum))
        {
            return false;
        }

        result = match.Groups[1].Value switch
        {
            ">" => actualNum > expectedNum,
            ">=" => actualNum >= expectedNum,
            "<" => actualNum < expectedNum,
            "<=" => actualNum <= expectedNum,
            "=" or "==" => actualNum == expectedNum,
            _ => false,
        };

        return true;
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

        // Backward-compat: legacy expectations from n8n/LLM may use
        // "typeof string|number|boolean|array|object".
        if (text.StartsWith("typeof ", StringComparison.Ordinal))
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

    private static bool TryParseSlashDelimitedRegex(string expected, out string pattern, out RegexOptions options)
    {
        pattern = null;
        options = RegexOptions.None;

        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var candidate = expected.Trim();
        if (!candidate.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var lastSlash = candidate.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return false;
        }

        var innerPattern = candidate[1..lastSlash];
        if (string.IsNullOrWhiteSpace(innerPattern))
        {
            return false;
        }

        var flags = candidate[(lastSlash + 1)..];
        foreach (var flag in flags)
        {
            switch (char.ToLowerInvariant(flag))
            {
                case 'i':
                    options |= RegexOptions.IgnoreCase;
                    break;
                case 'm':
                    options |= RegexOptions.Multiline;
                    break;
                case 's':
                    options |= RegexOptions.Singleline;
                    break;
                case ' ':
                    break;
                default:
                    return false;
            }
        }

        pattern = innerPattern;
        return true;
    }

    private static string NormalizePossiblyOverEscapedRegex(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return pattern;
        }

        // Heuristic: only normalize when we detect doubled escapes.
        // Example input: ^[a-z]+\\.[a-z]{2}$  ->  ^[a-z]+\.[a-z]{2}$
        if (!pattern.Contains(@"\\", StringComparison.Ordinal))
        {
            return pattern;
        }

        try
        {
            var unescaped = Regex.Unescape(pattern);
            return string.IsNullOrWhiteSpace(unescaped) ? pattern : unescaped;
        }
        catch
        {
            return pattern;
        }
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

    private sealed class ExpectedProvenanceRuntimeItem
    {
        public string Field { get; set; }

        public string Expected { get; set; }

        public string Type { get; set; }

        public string Source { get; set; }

        public string RequirementCode { get; set; }

        public string Evidence { get; set; }

        public string Confidence { get; set; }
    }
}
