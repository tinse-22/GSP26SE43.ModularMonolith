using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestExecution.Services;

/// <summary>
/// Pre-execution validator that checks test case request completeness BEFORE sending HTTP requests.
/// Collects ALL issues in a single pass with actionable fix suggestions.
/// </summary>
public class PreExecutionValidator : IPreExecutionValidator
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private static readonly Regex RouteTokenRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex DuplicatedIdentifierPlaceholderRegex = new(@"IdId(s)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly HashSet<string> BodyRequiredMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH",
    };

    public PreExecutionValidationResult Validate(
        ExecutionTestCaseDto testCase,
        ResolvedExecutionEnvironment environment,
        IReadOnlyDictionary<string, string> variableBag,
        ApiEndpointMetadataDto endpointMetadata)
    {
        var result = new PreExecutionValidationResult();

        ValidateEnvironment(environment, result);

        if (testCase.Request == null)
        {
            result.Errors.Add(new ValidationFailureModel
            {
                Code = "MISSING_REQUEST",
                Message = "Test case không có request data. LLM có thể đã không sinh đúng request object.",
                Target = testCase.Name,
            });
            return result;
        }

        ValidatePathParams(testCase, variableBag, environment, result);
        ValidateRequiredQueryParams(testCase, endpointMetadata, result);
        ValidateBody(testCase, endpointMetadata, result);
        ValidateUnresolvedPlaceholders(testCase, variableBag, environment, result);
        ValidateVariableChaining(testCase, variableBag, result);

        return result;
    }

    private static void ValidateEnvironment(
        ResolvedExecutionEnvironment environment,
        PreExecutionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(environment?.BaseUrl))
        {
            result.Errors.Add(new ValidationFailureModel
            {
                Code = "MISSING_BASE_URL",
                Message = "ExecutionEnvironment chưa cấu hình BaseUrl. Vui lòng thiết lập BaseUrl trong environment settings.",
                Target = "Environment.BaseUrl",
                Expected = "https://api.example.com",
                Actual = "(trống)",
            });
        }
    }

    private static void ValidatePathParams(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variableBag,
        ResolvedExecutionEnvironment environment,
        PreExecutionValidationResult result)
    {
        var url = testCase.Request.Url ?? string.Empty;

        // Find all route tokens like {id}, {userId} in the URL
        var routeTokens = RouteTokenRegex.Matches(url);
        if (routeTokens.Count == 0)
        {
            return;
        }

        var pathParams = DeserializeDictionary(testCase.Request.PathParams);

        // Build merged variable set for placeholder resolution check
        var mergedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (environment?.Variables != null)
        {
            foreach (var kvp in environment.Variables)
            {
                mergedVars[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in variableBag)
        {
            mergedVars[kvp.Key] = kvp.Value;
        }

        var requiredPathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match token in routeTokens)
        {
            requiredPathParams.Add(token.Groups[1].Value);
        }

        foreach (var routeParam in requiredPathParams)
        {
            var paramName = routeParam;

            // Check if path param value is provided
            if (!pathParams.TryGetValue(paramName, out var paramValue) || string.IsNullOrWhiteSpace(paramValue))
            {
                result.Errors.Add(new ValidationFailureModel
                {
                    Code = "MISSING_PATH_PARAM",
                    Message = $"Path parameter '{paramName}' chưa có giá trị trong request.pathParams. " +
                              $"URL template: {url}. " +
                              "Cách khắc phục: Kiểm tra LLM đã sinh đủ pathParams, hoặc thêm variable extraction từ test trước.",
                    Target = $"PathParams.{paramName}",
                    Expected = $"Giá trị cho {{{paramName}}}",
                    Actual = "(không có)",
                });
                continue;
            }

            // Check if path param value is a placeholder that can be resolved
            var placeholderMatch = PlaceholderRegex.Match(paramValue);
            if (placeholderMatch.Success)
            {
                var varName = placeholderMatch.Groups[1].Value;
                if (!mergedVars.ContainsKey(varName))
                {
                    result.Errors.Add(new ValidationFailureModel
                    {
                        Code = "UNRESOLVABLE_PATH_PARAM",
                        Message = $"Path parameter '{paramName}' dùng variable '{{{{{varName}}}}}' nhưng variable này chưa có giá trị. " +
                                  "Cách khắc phục: Đảm bảo test trước có variable extraction rule cho '" + varName + "', " +
                                  "hoặc thêm biến vào ExecutionEnvironment.Variables.",
                        Target = $"PathParams.{paramName}",
                        Expected = $"Giá trị đã resolve cho {{{{{varName}}}}}",
                        Actual = paramValue,
                    });
                }
            }
        }
    }

    private static void ValidateRequiredQueryParams(
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata,
        PreExecutionValidationResult result)
    {
        if (endpointMetadata?.RequiredQueryParameterNames == null || endpointMetadata.RequiredQueryParameterNames.Count == 0)
        {
            return;
        }

        var queryParams = DeserializeDictionary(testCase.Request?.QueryParams);
        foreach (var requiredQueryParam in endpointMetadata.RequiredQueryParameterNames)
        {
            if (string.IsNullOrWhiteSpace(requiredQueryParam))
            {
                continue;
            }

            if (IsLegacyBodyQueryRequirementSatisfied(testCase, requiredQueryParam))
            {
                continue;
            }

            if (!queryParams.TryGetValue(requiredQueryParam, out var value) || string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add(new ValidationFailureModel
                {
                    Code = "MISSING_REQUIRED_QUERY_PARAM",
                    Message = $"Thiếu required query parameter '{requiredQueryParam}' theo endpoint contract.",
                    Target = $"QueryParams.{requiredQueryParam}",
                    Expected = "Giá trị không rỗng",
                    Actual = "(không có)",
                });
            }
        }
    }

    private static bool IsLegacyBodyQueryRequirementSatisfied(
        ExecutionTestCaseDto testCase,
        string requiredQueryParam)
    {
        if (testCase?.Request == null ||
            !string.Equals(requiredQueryParam, "body", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!BodyRequiredMethods.Contains(testCase.Request.HttpMethod ?? string.Empty))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(testCase.Request.Body);
    }

    private static void ValidateBody(
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata,
        PreExecutionValidationResult result)
    {
        var httpMethod = testCase.Request.HttpMethod?.Trim().ToUpperInvariant() ?? "GET";

        var requiresBodyByMethod = BodyRequiredMethods.Contains(httpMethod);
        var requiresBodyByContract = endpointMetadata?.HasRequiredRequestBody == true;
        if (!requiresBodyByMethod && !requiresBodyByContract)
        {
            return;
        }

        var hasBody = !string.IsNullOrWhiteSpace(testCase.Request.Body);
        var bodyType = testCase.Request.BodyType?.Trim().ToUpperInvariant();

        if (!hasBody && requiresBodyByContract)
        {
            result.Errors.Add(new ValidationFailureModel
            {
                Code = "MISSING_REQUIRED_BODY",
                Message = "Endpoint contract yêu cầu request body nhưng request hiện tại không có body.",
                Target = "Request.Body",
                Expected = "Body JSON hợp lệ",
                Actual = "(trống)",
            });
            return;
        }

        if (hasBody && requiresBodyByContract && IsMeaninglessRequiredBody(testCase.Request.Body, endpointMetadata))
        {
            if (IsLikelyErrorCase(testCase))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "MEANINGLESS_REQUIRED_BODY_FOR_ERROR_CASE",
                    Message = "Request body đang là object rỗng cho test case lỗi (negative/boundary). Hệ thống vẫn cho phép chạy để xác nhận response 4xx thực tế.",
                });
                return;
            }

            result.Errors.Add(new ValidationFailureModel
            {
                Code = "MEANINGLESS_REQUIRED_BODY",
                Message = "Request body hiện chỉ chứa object rỗng hoặc payload không có trường meaningful dù endpoint contract yêu cầu body có schema cụ thể.",
                Target = "Request.Body",
                Expected = "JSON body với các trường required/example/default hợp lệ",
                Actual = testCase.Request.Body,
            });
            return;
        }

        if (!hasBody && bodyType != "NONE" && !string.IsNullOrEmpty(bodyType))
        {
            result.Errors.Add(new ValidationFailureModel
            {
                Code = "MISSING_BODY",
                Message = $"HTTP {httpMethod} request thiếu body nhưng bodyType='{testCase.Request.BodyType}'. " +
                          "LLM đã không sinh request body. " +
                          "Cách khắc phục: Kiểm tra test case request trong DB, hoặc re-generate test cases với đầy đủ schema.",
                Target = "Request.Body",
                Expected = $"Body content cho {httpMethod} request",
                Actual = "(trống)",
            });
        }
        else if (!hasBody && requiresBodyByMethod)
        {
            // Warning instead of error — some POST endpoints may not require a body (e.g., trigger actions)
            result.Warnings.Add(new ValidationWarningModel
            {
                Code = "EMPTY_BODY_FOR_WRITE_METHOD",
                Message = $"HTTP {httpMethod} request không có body. Nếu endpoint yêu cầu body, test sẽ bị reject bởi server (400/422).",
            });
        }
    }

    private static bool IsMeaninglessRequiredBody(string body, ApiEndpointMetadataDto endpointMetadata)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return true;
        }

        var schema = endpointMetadata?.Parameters?
            .FirstOrDefault(parameter =>
                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Schema))
            ?.Schema
            ?? endpointMetadata?.ParameterSchemaPayloads?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(schema))
        {
            return false;
        }

        try
        {
            using var bodyDocument = JsonDocument.Parse(body);
            if (bodyDocument.RootElement.ValueKind != JsonValueKind.Object ||
                bodyDocument.RootElement.EnumerateObject().Any())
            {
                return false;
            }

            using var schemaDocument = JsonDocument.Parse(schema);
            var schemaRoot = schemaDocument.RootElement;

            if (schemaRoot.TryGetProperty("required", out var required) &&
                required.ValueKind == JsonValueKind.Array &&
                required.GetArrayLength() > 0)
            {
                return true;
            }

            return schemaRoot.TryGetProperty("properties", out var properties) &&
                   properties.ValueKind == JsonValueKind.Object &&
                   properties.EnumerateObject().Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyErrorCase(ExecutionTestCaseDto testCase)
    {
        if (testCase == null)
        {
            return false;
        }

        if (ContainsAny(testCase.TestType, "negative", "boundary", "invalid") ||
            ContainsAny(testCase.Name, "negative", "boundary", "invalid"))
        {
            return true;
        }

        var statuses = ParseExpectedStatuses(testCase.Expectation?.ExpectedStatus);
        return statuses.Count > 0 && statuses.All(status => status >= 400 && status < 600);
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

    private static void ValidateUnresolvedPlaceholders(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variableBag,
        ResolvedExecutionEnvironment environment,
        PreExecutionValidationResult result)
    {
        var mergedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (environment?.Variables != null)
        {
            foreach (var kvp in environment.Variables)
            {
                mergedVars[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in variableBag)
        {
            mergedVars[kvp.Key] = kvp.Value;
        }

        // Check all surfaces for unresolvable placeholders
        CheckPlaceholders(testCase.Request.Url, "URL", mergedVars, result);
        CheckPlaceholders(testCase.Request.Body, "Body", mergedVars, result);

        var headers = DeserializeDictionary(testCase.Request.Headers);
        foreach (var kvp in headers)
        {
            CheckPlaceholders(kvp.Value, $"Header:{kvp.Key}", mergedVars, result);
        }

        var queryParams = DeserializeDictionary(testCase.Request.QueryParams);
        foreach (var kvp in queryParams)
        {
            CheckPlaceholders(kvp.Value, $"QueryParam:{kvp.Key}", mergedVars, result);
        }
    }

    private static void CheckPlaceholders(
        string value,
        string surface,
        Dictionary<string, string> mergedVars,
        PreExecutionValidationResult result)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (Match match in PlaceholderRegex.Matches(value))
        {
            var varName = match.Groups[1].Value;
            if (HasResolvableVariable(mergedVars, varName))
            {
                continue;
            }

            if (string.Equals(surface, "Body", StringComparison.OrdinalIgnoreCase)
                && IsNumericSemanticVariableName(varName))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "NUMERIC_PLACEHOLDER_DEFAULT_FALLBACK",
                    Message = $"Variable '{{{{{varName}}}}}' trong Body chưa có giá trị. Hệ thống sẽ dùng giá trị số mặc định an toàn để tiếp tục chạy test.",
                });
                continue;
            }

            if (string.Equals(surface, "Body", StringComparison.OrdinalIgnoreCase)
                && IsKnownDuplicatedIdentifierPlaceholderName(varName))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "IDENTIFIER_PLACEHOLDER_DUPLICATE_ID_FALLBACK",
                    Message = $"Variable '{{{{{varName}}}}}' trong Body có pattern lỗi sinh dữ liệu (duplicate 'Id'). Hệ thống sẽ dùng fallback identifier mặc định để tiếp tục chạy test.",
                });
                continue;
            }

            if (string.Equals(surface, "Body", StringComparison.OrdinalIgnoreCase)
                && !IsIdentifierSemanticVariableName(varName))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "TEXT_PLACEHOLDER_DEFAULT_FALLBACK",
                    Message = $"Variable '{{{{{varName}}}}}' trong Body chưa có giá trị. Hệ thống sẽ dùng giá trị text mặc định an toàn để tiếp tục chạy test.",
                });
                continue;
            }

            if (ShouldAllowAuthHeaderFallback(surface, varName))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "AUTH_HEADER_PLACEHOLDER_FALLBACK",
                    Message = $"Variable '{{{{{varName}}}}}' trong {surface} chưa có giá trị. Hệ thống sẽ tiếp tục chạy request thật để server tự xác thực.",
                });
                continue;
            }

            if (TryBuildAuthHeaderFallbackWarning(surface, value, varName, out var warningCode, out var warningMessage))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = warningCode,
                    Message = warningMessage,
                });
                continue;
            }

            if (IsAuthorizationLikeHeader(surface))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "AUTH_HEADER_PLACEHOLDER_FALLBACK",
                    Message = $"Variable '{{{{{varName}}}}}' trong {surface} chưa có giá trị. Hệ thống sẽ tiếp tục chạy request thật để server tự xác thực.",
                });
                continue;
            }

            result.Errors.Add(new ValidationFailureModel
            {
                Code = "UNRESOLVED_VARIABLE",
                Message = $"Variable '{{{{{varName}}}}}' trong {surface} chưa có giá trị. " +
                          "Cách khắc phục: (1) Thêm vào ExecutionEnvironment.Variables, " +
                          "(2) Đảm bảo test trước có extraction rule, " +
                          "(3) Re-generate test cases.",
                Target = surface,
                Expected = $"Giá trị cho {{{{{varName}}}}}",
                Actual = "(chưa resolve)",
            });
        }
    }

    private static bool HasResolvableVariable(
        IReadOnlyDictionary<string, string> mergedVars,
        string varName)
    {
        if (mergedVars == null || string.IsNullOrWhiteSpace(varName))
        {
            return false;
        }

        if (mergedVars.ContainsKey(varName))
        {
            return true;
        }

        if (IsAuthTokenAlias(varName))
        {
            foreach (var alias in GetAuthTokenAliases(varName))
            {
                if (mergedVars.ContainsKey(alias))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAuthTokenAlias(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        return string.Equals(variableName, "authToken", StringComparison.OrdinalIgnoreCase)
            || string.Equals(variableName, "accessToken", StringComparison.OrdinalIgnoreCase)
            || string.Equals(variableName, "token", StringComparison.OrdinalIgnoreCase)
            || string.Equals(variableName, "jwt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(variableName, "sessionToken", StringComparison.OrdinalIgnoreCase)
            || string.Equals(variableName, "bearerToken", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAllowAuthHeaderFallback(string surface, string varName)
    {
        return IsSecurityHeader(surface)
            && IsAuthTokenAlias(varName);
    }

    private static bool IsAuthorizationLikeHeader(string surface)
    {
        return IsSecurityHeader(surface);
    }

    private static bool TryBuildAuthHeaderFallbackWarning(
        string surface,
        string headerValue,
        string varName,
        out string warningCode,
        out string warningMessage)
    {
        warningCode = null;
        warningMessage = null;

        if (string.IsNullOrWhiteSpace(surface) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        if (!IsSecurityHeader(surface))
        {
            return false;
        }

        if (!TryExtractInlineHeaderTokenFormat(headerValue, out var scheme))
        {
            warningCode = "AUTH_HEADER_PLACEHOLDER_FALLBACK";
            warningMessage = $"Variable '{{{{{varName}}}}}' trong {surface} chưa có giá trị. Hệ thống sẽ tiếp tục chạy request thật để server tự xác thực.";
            return true;
        }

        warningCode = "AUTH_HEADER_FORMAT_FALLBACK";
        warningMessage = $"Header {surface} dùng format '{scheme} {{...}}'. Hệ thống sẽ cho phép chạy fallback để cover nhiều kiểu auth header khác nhau.";
        return true;
    }

    private static bool IsSecurityHeader(string surface)
    {
        if (string.IsNullOrWhiteSpace(surface))
        {
            return false;
        }

        return surface.StartsWith("Header:Authorization", StringComparison.OrdinalIgnoreCase)
            || surface.StartsWith("Header:Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || surface.StartsWith("Header:X-Authorization", StringComparison.OrdinalIgnoreCase)
            || surface.StartsWith("Header:X-Auth", StringComparison.OrdinalIgnoreCase)
            || surface.StartsWith("Header:X-API-Key", StringComparison.OrdinalIgnoreCase)
            || surface.StartsWith("Header:Api-Key", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("api-key", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("bearer", StringComparison.OrdinalIgnoreCase)
            || surface.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractInlineHeaderTokenFormat(string headerValue, out string scheme)
    {
        scheme = null;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var trimmed = headerValue.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            return false;
        }

        var firstPart = trimmed[..spaceIndex].Trim();
        var remainder = trimmed[(spaceIndex + 1)..].Trim();
        if (!remainder.Contains("{{", StringComparison.Ordinal) || !remainder.Contains("}}", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(firstPart, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstPart, "Token", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstPart, "ApiKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstPart, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            scheme = firstPart;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetAuthTokenAliases(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            yield break;
        }

        var normalized = variableName.Trim();
        if (string.Equals(normalized, "authToken", StringComparison.OrdinalIgnoreCase))
        {
            yield return "accessToken";
            yield return "token";
            yield return "jwt";
            yield return "sessionToken";
            yield return "bearerToken";
            yield break;
        }

        if (string.Equals(normalized, "accessToken", StringComparison.OrdinalIgnoreCase))
        {
            yield return "authToken";
            yield return "token";
            yield return "jwt";
            yield return "sessionToken";
            yield return "bearerToken";
            yield break;
        }

        yield return "authToken";
        yield return "accessToken";
        yield return "token";
        yield return "jwt";
        yield return "sessionToken";
        yield return "bearerToken";
    }

    private static bool IsIdentifierSemanticVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        return string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownDuplicatedIdentifierPlaceholderName(string variableName)
    {
        return !string.IsNullOrWhiteSpace(variableName)
            && DuplicatedIdentifierPlaceholderRegex.IsMatch(variableName);
    }

    private static bool IsNumericSemanticVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        var normalized = variableName.Trim().ToLowerInvariant();
        var numericKeywords = new[]
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
        };

        foreach (var keyword in numericKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateVariableChaining(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variableBag,
        PreExecutionValidationResult result)
    {
        if (testCase.Variables == null || testCase.Variables.Count == 0)
        {
            return;
        }

        // Warn if extraction rules look incomplete
        foreach (var variable in testCase.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.ExtractFrom))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "INCOMPLETE_EXTRACTION_RULE",
                    Message = $"Variable '{variable.VariableName}' có extraction rule nhưng thiếu ExtractFrom. " +
                              "Variable này sẽ không được extract từ response.",
                });
                continue;
            }

            if (variable.ExtractFrom == "ResponseBody" && string.IsNullOrWhiteSpace(variable.JsonPath))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "MISSING_JSON_PATH",
                    Message = $"Variable '{variable.VariableName}' extract từ ResponseBody nhưng thiếu JsonPath. " +
                              "Cách khắc phục: Thêm JsonPath expression (ví dụ: $.data.id).",
                });
            }

            if (variable.ExtractFrom == "ResponseHeader" && string.IsNullOrWhiteSpace(variable.HeaderName))
            {
                result.Warnings.Add(new ValidationWarningModel
                {
                    Code = "MISSING_HEADER_NAME",
                    Message = $"Variable '{variable.VariableName}' extract từ ResponseHeader nhưng thiếu HeaderName.",
                });
            }
        }
    }

    private static Dictionary<string, string> DeserializeDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
