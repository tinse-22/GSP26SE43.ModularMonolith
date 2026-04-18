using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class VariableResolver : IVariableResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
    private static readonly Regex RouteTokenRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

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
        resolvedBody = NormalizeHappyPathSyntheticBody(testCase, resolvedBody, mergedVars);

        // Build final URL
        var finalUrl = BuildFinalUrl(resolvedUrl, environment.BaseUrl);

        // Clamp timeout
        var timeout = Math.Clamp(request.Timeout, 1000, 60000);

        // Collect ALL unresolved placeholders — not just the first one
        var unresolvedIssues = new List<string>();
        CollectUnresolvedPlaceholders(finalUrl, "URL", unresolvedIssues);
        CollectUnresolvedRouteTokens(finalUrl, unresolvedIssues);
        foreach (var kvp in resolvedHeaders)
        {
            CollectUnresolvedPlaceholders(kvp.Value, $"Header:{kvp.Key}", unresolvedIssues);
        }

        foreach (var kvp in resolvedQuery)
        {
            CollectUnresolvedPlaceholders(kvp.Value, $"QueryParam:{kvp.Key}", unresolvedIssues);
        }

        if (resolvedBody != null)
        {
            CollectUnresolvedPlaceholders(resolvedBody, "Body", unresolvedIssues);
        }

        if (unresolvedIssues.Count > 0)
        {
            throw new UnresolvedVariableException(string.Join(" | ", unresolvedIssues));
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

    private static string NormalizeHappyPathSyntheticBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody)
            || !TryGetPreferredTestEmail(variables, out var preferredEmail))
        {
            return resolvedBody;
        }

        JsonNode root;
        try
        {
            root = JsonNode.Parse(resolvedBody);
        }
        catch
        {
            return resolvedBody;
        }

        if (root == null)
        {
            return resolvedBody;
        }

        return ReplaceSyntheticEmails(root, preferredEmail)
            ? root.ToJsonString(JsonOptions)
            : resolvedBody;
    }

    private static bool LooksLikeJsonBody(string bodyType, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        if (string.Equals(bodyType, "JSON", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static bool TryGetPreferredTestEmail(
        IReadOnlyDictionary<string, string> variables,
        out string preferredEmail)
    {
        preferredEmail = null;

        if (variables == null)
        {
            return false;
        }

        if (variables.TryGetValue("testEmail", out var testEmail)
            && !string.IsNullOrWhiteSpace(testEmail))
        {
            preferredEmail = testEmail;
            return true;
        }

        if (variables.TryGetValue("runUniqueEmail", out var runUniqueEmail)
            && !string.IsNullOrWhiteSpace(runUniqueEmail))
        {
            preferredEmail = runUniqueEmail;
            return true;
        }

        return false;
    }

    private static bool ReplaceSyntheticEmails(JsonNode node, string preferredEmail)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var stringValue)
                    && IsEmailField(property.Key)
                    && ShouldRewriteSyntheticEmail(stringValue))
                {
                    obj[property.Key] = preferredEmail;
                    changed = true;
                    continue;
                }

                if (property.Value != null && ReplaceSyntheticEmails(property.Value, preferredEmail))
                {
                    changed = true;
                }
            }

            return changed;
        }

        if (node is JsonArray array)
        {
            var changed = false;
            foreach (var item in array)
            {
                if (item != null && ReplaceSyntheticEmails(item, preferredEmail))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static bool IsEmailField(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName.Equals("email", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith("Email", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRewriteSyntheticEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("{{", StringComparison.Ordinal))
        {
            return false;
        }

        var atIndex = value.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return false;
        }

        var localPart = value[..atIndex].Trim().ToLowerInvariant();
        var domain = value[(atIndex + 1)..].Trim().ToLowerInvariant();

        if (domain.StartsWith("example.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return localPart.StartsWith("test", StringComparison.OrdinalIgnoreCase)
            || localPart.StartsWith("demo", StringComparison.OrdinalIgnoreCase)
            || localPart.StartsWith("sample", StringComparison.OrdinalIgnoreCase)
            || localPart.StartsWith("user", StringComparison.OrdinalIgnoreCase)
            || localPart.StartsWith("qa", StringComparison.OrdinalIgnoreCase)
            || localPart.StartsWith("auto", StringComparison.OrdinalIgnoreCase);
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
                $"Biến '{{{match.Groups[1].Value}}}' chưa được giải quyết trong {surface}.");
        }
    }

    private static void CheckUnresolvedRouteTokens(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        var pathOnly = ExtractPathOnly(url);
        var match = RouteTokenRegex.Match(pathOnly);
        if (match.Success)
        {
            throw new UnresolvedVariableException(
                $"Path parameter '{match.Groups[1].Value}' chưa được giải quyết trong URL.");
        }
    }

    private static void CollectUnresolvedPlaceholders(string value, string surface, List<string> issues)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (Match match in PlaceholderRegex.Matches(value))
        {
            issues.Add($"Variable '{{{{{match.Groups[1].Value}}}}}' chưa được giải quyết trong {surface}");
        }
    }

    private static void CollectUnresolvedRouteTokens(string url, List<string> issues)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        var pathOnly = ExtractPathOnly(url);
        foreach (Match match in RouteTokenRegex.Matches(pathOnly))
        {
            issues.Add($"Path parameter '{match.Groups[1].Value}' chưa được giải quyết trong URL");
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

    private static string ExtractPathOnly(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            var path = absolute.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            return path.StartsWith('/') ? path : $"/{path}";
        }

        var queryIndex = url.IndexOfAny(new[] { '?', '#' });
        return queryIndex >= 0 ? url[..queryIndex] : url;
    }
}

public class UnresolvedVariableException : Exception
{
    public UnresolvedVariableException(string message) : base(message)
    {
    }
}
