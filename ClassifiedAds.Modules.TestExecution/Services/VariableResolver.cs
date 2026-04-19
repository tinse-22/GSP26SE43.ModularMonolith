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
    private static readonly Regex SimpleSyntheticNameRegex = new(@"^[A-Za-z0-9 _\-]{2,80}$", RegexOptions.Compiled);

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
        var normalizedPathParams = NormalizePathParams(pathParams, resolvedUrl, mergedVars);
        foreach (var kvp in normalizedPathParams)
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

        ApplyAuthFallbackHeader(resolvedHeaders, mergedVars);

        // Resolve body
        var resolvedBody = !string.IsNullOrEmpty(request.Body)
            ? ResolvePlaceholders(request.Body, mergedVars)
            : null;
        resolvedBody = NormalizeHappyPathCredentials(testCase, resolvedBody, mergedVars);
        resolvedBody = NormalizeIdentifierLiteralsInJsonBody(resolvedBody, mergedVars);
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

    private static Dictionary<string, string> NormalizePathParams(
        IReadOnlyDictionary<string, string> pathParams,
        string resolvedUrl,
        IReadOnlyDictionary<string, string> variables)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (pathParams == null || pathParams.Count == 0)
        {
            return normalized;
        }

        foreach (var kvp in pathParams)
        {
            var resolvedValue = kvp.Value;
            if (ShouldReplaceIdentifierLiteral(resolvedValue) &&
                TryResolveVariableValue(BuildPathParamVariableCandidates(kvp.Key, resolvedUrl), variables, out var replacement))
            {
                resolvedValue = replacement;
            }

            normalized[kvp.Key] = resolvedValue;
        }

        return normalized;
    }

    private static IEnumerable<string> BuildPathParamVariableCandidates(string paramName, string resolvedUrl)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(paramName))
        {
            if (string.Equals(paramName, "id", StringComparison.OrdinalIgnoreCase))
            {
                var resourceIdVariable = BuildResourceIdVariableNameFromPath(resolvedUrl, paramName);
                if (!string.IsNullOrWhiteSpace(resourceIdVariable))
                {
                    candidates.Add(resourceIdVariable);
                }

                candidates.Add("resourceId");
                candidates.Add("id");
            }
            else
            {
                candidates.Add(paramName);
                candidates.Add(StripIdSuffix(paramName) + "Id");
                candidates.Add("resourceId");
                candidates.Add("id");
            }
        }

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyAuthFallbackHeader(
        IDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> variables)
    {
        if (headers == null || variables == null)
        {
            return;
        }

        if (headers.TryGetValue("Authorization", out var existingAuthorization) &&
            !string.IsNullOrWhiteSpace(existingAuthorization))
        {
            return;
        }

        foreach (var key in new[] { "authToken", "accessToken", "token", "jwt" })
        {
            if (!variables.TryGetValue(key, out var value) ||
                string.IsNullOrWhiteSpace(value) ||
                value.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            headers["Authorization"] = $"Bearer {value}";
            return;
        }
    }

    private static string NormalizeHappyPathCredentials(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody)
            || !IsLoginLikeRequest(testCase))
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

        string preferredEmail = null;
        if (variables != null)
        {
            if (variables.TryGetValue("registeredEmail", out var registeredEmail) && !string.IsNullOrWhiteSpace(registeredEmail))
            {
                preferredEmail = registeredEmail;
            }
            else if (variables.TryGetValue("testEmail", out var testEmail) && !string.IsNullOrWhiteSpace(testEmail))
            {
                preferredEmail = testEmail;
            }
        }

        var preferredPassword = variables != null
            && variables.TryGetValue("registeredPassword", out var registeredPassword)
            && !string.IsNullOrWhiteSpace(registeredPassword)
                ? registeredPassword
                : null;

        if (string.IsNullOrWhiteSpace(preferredEmail) && string.IsNullOrWhiteSpace(preferredPassword))
        {
            return resolvedBody;
        }

        var changed = RewriteCredentialFields(root, preferredEmail, preferredPassword);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static bool RewriteCredentialFields(JsonNode node, string preferredEmail, string preferredPassword)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var current))
                {
                    if (property.Key.Equals("email", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(preferredEmail) &&
                        !current.Contains("{{", StringComparison.Ordinal) &&
                        !string.Equals(current, preferredEmail, StringComparison.Ordinal))
                    {
                        obj[property.Key] = preferredEmail;
                        changed = true;
                        continue;
                    }

                    if (property.Key.Equals("password", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(preferredPassword) &&
                        !current.Contains("{{", StringComparison.Ordinal) &&
                        !string.Equals(current, preferredPassword, StringComparison.Ordinal))
                    {
                        obj[property.Key] = preferredPassword;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null && RewriteCredentialFields(property.Value, preferredEmail, preferredPassword))
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
                if (item != null && RewriteCredentialFields(item, preferredEmail, preferredPassword))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static string NormalizeIdentifierLiteralsInJsonBody(
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody) || variables == null)
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

        var changed = ReplaceIdentifierLiterals(root, variables);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static bool ReplaceIdentifierLiterals(JsonNode node, IReadOnlyDictionary<string, string> variables)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue)
                {
                    var currentValue = property.Value?.ToJsonString(JsonOptions)?.Trim('"');
                    if (IsIdentifierField(property.Key) &&
                        ShouldReplaceIdentifierLiteral(currentValue) &&
                        TryResolveVariableValue(BuildBodyIdentifierCandidates(property.Key), variables, out var replacement))
                    {
                        obj[property.Key] = replacement;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null && ReplaceIdentifierLiterals(property.Value, variables))
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
                if (item != null && ReplaceIdentifierLiterals(item, variables))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static IEnumerable<string> BuildBodyIdentifierCandidates(string propertyName)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            candidates.Add(propertyName);

            var stripped = StripIdSuffix(propertyName);
            if (!string.IsNullOrWhiteSpace(stripped))
            {
                candidates.Add(stripped + "Id");
                candidates.Add(stripped);
            }
        }

        candidates.Add("resourceId");
        candidates.Add("id");

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryResolveVariableValue(
        IEnumerable<string> candidates,
        IReadOnlyDictionary<string, string> variables,
        out string resolved)
    {
        if (candidates != null)
        {
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)
                    || !variables.TryGetValue(candidate, out var value)
                    || string.IsNullOrWhiteSpace(value)
                    || value.Contains("{{", StringComparison.Ordinal)
                    || ShouldReplaceIdentifierLiteral(value))
                {
                    continue;
                }

                resolved = value;
                return true;
            }
        }

        resolved = null;
        return false;
    }

    private static bool ShouldReplaceIdentifierLiteral(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        if (normalized.Contains("{{", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized == "0" || normalized == "1")
        {
            return true;
        }

        if (Guid.TryParse(normalized, out var parsedGuid))
        {
            return parsedGuid == Guid.Empty || normalized.StartsWith("00000000", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsIdentifierField(string propertyName)
    {
        return !string.IsNullOrWhiteSpace(propertyName)
            && (string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase)
                || propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                || propertyName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildResourceIdVariableNameFromPath(string urlOrPath, string token)
    {
        var resourceSegment = ResolveResourceSegmentForPathToken(urlOrPath, token);
        if (string.IsNullOrWhiteSpace(resourceSegment))
        {
            return null;
        }

        var identifier = ToCamelIdentifier(Singularize(resourceSegment));
        return string.IsNullOrWhiteSpace(identifier) ? null : identifier + "Id";
    }

    private static string ResolveResourceSegmentForPathToken(string urlOrPath, string token)
    {
        var path = ExtractPathOnly(urlOrPath ?? string.Empty);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (!string.Equals(segments[i], $"{{{token}}}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var j = i - 1; j >= 0; j--)
            {
                var segment = segments[j].Trim();
                if (string.IsNullOrWhiteSpace(segment)
                    || string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase)
                    || IsVersionSegment(segment)
                    || (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)))
                {
                    continue;
                }

                return segment;
            }
        }

        return null;
    }

    private static bool IsVersionSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment.Length <= 3
            && segment.StartsWith('v')
            && segment.Skip(1).All(char.IsDigit);
    }

    private static string StripIdSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
        {
            return value[..^3];
        }

        if (value.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && value.Length > 2)
        {
            return value[..^2];
        }

        return value;
    }

    private static string Singularize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
        {
            return value[..^3] + "y";
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            && value.Length > 1)
        {
            return value[..^1];
        }

        return value;
    }

    private static string ToCamelIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value
            .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return value;
        }

        var first = parts[0].Length == 0
            ? string.Empty
            : char.ToLowerInvariant(parts[0][0]) + parts[0][1..];

        var rest = parts
            .Skip(1)
            .Select(part => part.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(part[0]) + part[1..]);

        return first + string.Concat(rest);
    }

    private static bool IsLoginLikeRequest(ExecutionTestCaseDto testCase)
    {
        var signature = $"{testCase?.Request?.HttpMethod} {testCase?.Request?.Url} {testCase?.Name}";
        return signature.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signin", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("sign-in", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/token", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHappyPathSyntheticBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(testCase.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody)
            )
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

        var hasPreferredEmail = TryGetPreferredTestEmail(variables, out var preferredEmail);
        var runSuffix = GetRunSuffix(variables);

        var emailRewritten = hasPreferredEmail && ReplaceSyntheticEmails(root, preferredEmail);
        var nameRewritten = ReplaceSyntheticResourceNames(root, runSuffix);

        return emailRewritten || nameRewritten
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

    private static string GetRunSuffix(IReadOnlyDictionary<string, string> variables)
    {
        if (variables == null)
        {
            return null;
        }

        var candidateKeys = new[] { "runSuffix", "runIdSuffix", "runId" };
        foreach (var key in candidateKeys)
        {
            if (!variables.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = new string(raw
                .Trim()
                .Where(char.IsLetterOrDigit)
                .ToArray());

            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            return normalized.Length <= 10
                ? normalized.ToLowerInvariant()
                : normalized[^10..].ToLowerInvariant();
        }

        return null;
    }

    private static bool ReplaceSyntheticResourceNames(JsonNode node, string runSuffix)
    {
        if (string.IsNullOrWhiteSpace(runSuffix))
        {
            return false;
        }

        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var stringValue)
                    && IsSyntheticNameField(property.Key)
                    && ShouldRewriteSyntheticName(stringValue, runSuffix))
                {
                    obj[property.Key] = BuildSyntheticNameValue(stringValue, property.Key, runSuffix);
                    changed = true;
                    continue;
                }

                if (property.Value != null && ReplaceSyntheticResourceNames(property.Value, runSuffix))
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
                if (item != null && ReplaceSyntheticResourceNames(item, runSuffix))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static bool IsSyntheticNameField(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || IsEmailField(propertyName))
        {
            return false;
        }

        if (propertyName.Equals("name", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("title", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("slug", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("code", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!propertyName.EndsWith("Name", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !propertyName.Equals("firstName", StringComparison.OrdinalIgnoreCase)
            && !propertyName.Equals("lastName", StringComparison.OrdinalIgnoreCase)
            && !propertyName.Equals("middleName", StringComparison.OrdinalIgnoreCase)
            && !propertyName.Equals("fullName", StringComparison.OrdinalIgnoreCase)
            && !propertyName.Equals("userName", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRewriteSyntheticName(string value, string runSuffix)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("{{", StringComparison.Ordinal)
            || value.Contains('@')
            || value.Contains(runSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!SimpleSyntheticNameRegex.IsMatch(trimmed))
        {
            return false;
        }

        return !Guid.TryParse(trimmed, out _);
    }

    private static string BuildSyntheticNameValue(string originalValue, string propertyName, string runSuffix)
    {
        if (string.Equals(propertyName, "slug", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSlug(originalValue, runSuffix);
        }

        var baseValue = originalValue.Trim();
        var maxBaseLength = Math.Max(8, 80 - runSuffix.Length - 1);
        if (baseValue.Length > maxBaseLength)
        {
            baseValue = baseValue[..maxBaseLength].TrimEnd();
        }

        return $"{baseValue}-{runSuffix}";
    }

    private static string BuildSlug(string value, string runSuffix)
    {
        var combined = $"{value}-{runSuffix}".Trim().ToLowerInvariant();
        var normalizedChars = combined
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(normalizedChars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
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
