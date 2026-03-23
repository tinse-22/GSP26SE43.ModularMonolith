using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class VariableResolver : IVariableResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public ResolvedTestCaseRequest Resolve(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables,
        ResolvedExecutionEnvironment environment)
    {
        var request = testCase.Request;
        if (request == null)
        {
            return new ResolvedTestCaseRequest
            {
                TestCaseId = testCase.TestCaseId,
                Name = testCase.Name,
                HttpMethod = "GET",
                ResolvedUrl = environment.BaseUrl ?? string.Empty,
                TimeoutMs = 30000,
                DependencyIds = testCase.DependencyIds,
            };
        }

        // Build merged variable set: extracted run vars > env vars > literal
        var mergedVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in environment.Variables)
        {
            mergedVars[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in variables)
        {
            mergedVars[kvp.Key] = kvp.Value;
        }

        // Resolve URL
        var resolvedUrl = ResolvePlaceholders(request.Url ?? string.Empty, mergedVars);

        // Resolve path params and apply to URL
        var pathParams = DeserializeDictionary(request.PathParams);
        foreach (var kvp in pathParams)
        {
            var resolvedValue = ResolvePlaceholders(kvp.Value, mergedVars);
            resolvedUrl = resolvedUrl.Replace($"{{{kvp.Key}}}", Uri.EscapeDataString(resolvedValue));
        }

        // Resolve query params
        var queryParams = DeserializeDictionary(request.QueryParams);
        var resolvedQuery = new Dictionary<string, string>();

        // Start with env default query params
        foreach (var kvp in environment.DefaultQueryParams)
        {
            resolvedQuery[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in queryParams)
        {
            resolvedQuery[kvp.Key] = ResolvePlaceholders(kvp.Value, mergedVars);
        }

        // Resolve headers
        var requestHeaders = DeserializeDictionary(request.Headers);
        var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Env default headers first
        foreach (var kvp in environment.DefaultHeaders)
        {
            resolvedHeaders[kvp.Key] = kvp.Value;
        }

        // Request headers override
        foreach (var kvp in requestHeaders)
        {
            resolvedHeaders[kvp.Key] = ResolvePlaceholders(kvp.Value, mergedVars);
        }

        // Resolve body
        var resolvedBody = !string.IsNullOrEmpty(request.Body)
            ? ResolvePlaceholders(request.Body, mergedVars)
            : null;

        // Build final URL
        var finalUrl = BuildFinalUrl(resolvedUrl, environment.BaseUrl);

        // Clamp timeout
        var timeout = Math.Clamp(request.Timeout, 1000, 60000);

        // Check for unresolved placeholders
        CheckUnresolvedPlaceholders(finalUrl, "URL");
        foreach (var kvp in resolvedHeaders)
        {
            CheckUnresolvedPlaceholders(kvp.Value, $"Header:{kvp.Key}");
        }

        foreach (var kvp in resolvedQuery)
        {
            CheckUnresolvedPlaceholders(kvp.Value, $"QueryParam:{kvp.Key}");
        }

        if (resolvedBody != null)
        {
            CheckUnresolvedPlaceholders(resolvedBody, "Body");
        }

        return new ResolvedTestCaseRequest
        {
            TestCaseId = testCase.TestCaseId,
            Name = testCase.Name,
            HttpMethod = request.HttpMethod,
            ResolvedUrl = finalUrl,
            Headers = resolvedHeaders,
            QueryParams = resolvedQuery,
            Body = resolvedBody,
            BodyType = request.BodyType,
            TimeoutMs = timeout,
            DependencyIds = testCase.DependencyIds,
        };
    }

    private static string ResolvePlaceholders(string input, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return PlaceholderRegex.Replace(input, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static void CheckUnresolvedPlaceholders(string value, string surface)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var match = PlaceholderRegex.Match(value);
        if (match.Success)
        {
            throw new UnresolvedVariableException(
                $"Biến '{{{{{{match.Groups[1].Value}}}}}}' chưa được giải quyết trong {surface}.");
        }
    }

    private static string BuildFinalUrl(string resolvedUrl, string baseUrl)
    {
        if (string.IsNullOrEmpty(resolvedUrl))
        {
            return baseUrl ?? string.Empty;
        }

        // If already absolute, keep as-is
        if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            return resolvedUrl;
        }

        // Combine with base URL
        if (!string.IsNullOrEmpty(baseUrl))
        {
            var trimmedBase = baseUrl.TrimEnd('/');
            var trimmedPath = resolvedUrl.TrimStart('/');
            return $"{trimmedBase}/{trimmedPath}";
        }

        return resolvedUrl;
    }

    private static Dictionary<string, string> DeserializeDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();
    }
}

public class UnresolvedVariableException : Exception
{
    public UnresolvedVariableException(string message) : base(message)
    {
    }
}
