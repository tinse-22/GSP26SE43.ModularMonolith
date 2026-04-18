using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
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
                        Message = $"Path parameter '{paramName}' dùng variable '{{{{{{varName}}}}}}' nhưng variable này chưa có giá trị. " +
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
            if (!mergedVars.ContainsKey(varName))
            {
                result.Errors.Add(new ValidationFailureModel
                {
                    Code = "UNRESOLVED_VARIABLE",
                    Message = $"Variable '{{{{{{varName}}}}}}' trong {surface} chưa có giá trị. " +
                              "Cách khắc phục: (1) Thêm vào ExecutionEnvironment.Variables, " +
                              "(2) Đảm bảo test trước có extraction rule, " +
                              "(3) Re-generate test cases.",
                    Target = surface,
                    Expected = $"Giá trị cho {{{{{varName}}}}}",
                    Actual = "(chưa resolve)",
                });
            }
        }
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
