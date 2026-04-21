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
    private static readonly Regex ExactPlaceholderRegex = new(@"^\{\{(?<name>\w+)\}\}$", RegexOptions.Compiled);
    private static readonly Regex InlinePropertyPlaceholderRegex = new(
        "\"(?<prop>[A-Za-z0-9_]+)\"\\s*:\\s*\"?\\{\\{(?<name>\\w+)\\}\\}\"?",
        RegexOptions.Compiled);
    private static readonly Regex ObjectIdRegex = new(@"^[a-fA-F0-9]{24}$", RegexOptions.Compiled);
    private static readonly Regex RouteTokenRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex SimpleSyntheticNameRegex = new(@"^[A-Za-z0-9 _\-]{2,80}$", RegexOptions.Compiled);
    private static readonly Regex DuplicatedIdentifierPlaceholderRegex = new(@"IdId(s)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        var allowIdentifierLiteralReplacement = string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase);
        var normalizedPathParams = NormalizePathParams(pathParams, resolvedUrl, mergedVars, allowIdentifierLiteralReplacement);
        var resolvedPathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var routeTokenApplied = false;
        foreach (var kvp in normalizedPathParams)
        {
            var resolvedValue = ResolvePlaceholders(kvp.Value, mergedVars);
            resolvedPathParams[kvp.Key] = resolvedValue;

            var token = $"{{{kvp.Key}}}";
            if (!resolvedUrl.Contains(token, StringComparison.Ordinal))
            {
                continue;
            }

            resolvedUrl = resolvedUrl.Replace(token, Uri.EscapeDataString(resolvedValue));
            routeTokenApplied = true;
        }

        if (!routeTokenApplied)
        {
            resolvedUrl = TryApplyHappyPathLiteralRouteReplacement(testCase, resolvedUrl, resolvedPathParams, mergedVars);
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
        resolvedBody = NormalizeIdentifierLiteralsInJsonBody(testCase, resolvedBody, mergedVars);
        resolvedBody = NormalizeHappyPathSyntheticBody(testCase, resolvedBody, mergedVars);
        resolvedBody = NormalizeNumericPlaceholderDefaultsInJsonBody(testCase, resolvedBody);
        resolvedBody = NormalizeTextPlaceholderDefaultsInJsonBody(testCase, resolvedBody);

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
            if (variables.TryGetValue(key, out var value))
            {
                return value;
            }

            return TryResolveDuplicateIdentifierAliasValue(key, variables, out var aliasValue)
                ? aliasValue
                : match.Value;
        });
    }

    private static bool TryResolveDuplicateIdentifierAliasValue(
        string placeholderName,
        IReadOnlyDictionary<string, string> variables,
        out string resolved)
    {
        if (!IsKnownDuplicatedIdentifierPlaceholderName(placeholderName))
        {
            resolved = null;
            return false;
        }

        var candidates = new List<string>();
        var strippedOnce = StripIdSuffix(placeholderName);
        if (!string.IsNullOrWhiteSpace(strippedOnce))
        {
            candidates.Add(strippedOnce);
        }

        var strippedTwice = StripIdSuffix(strippedOnce);
        if (!string.IsNullOrWhiteSpace(strippedTwice))
        {
            candidates.Add(strippedTwice);
            candidates.Add(strippedTwice + "Id");
        }

        candidates.Add("resourceId");
        candidates.Add("id");

        if (TryResolveVariableValue(candidates.Distinct(StringComparer.OrdinalIgnoreCase), variables, out var candidateResolved)
            && IsSafeDuplicateIdentifierAliasValue(candidateResolved))
        {
            resolved = candidateResolved;
            return true;
        }

        var availableIdentifierValues = variables
            .Where(kvp => IsIdentifierSemanticVariableName(kvp.Key)
                && !string.IsNullOrWhiteSpace(kvp.Value)
                && !kvp.Value.Contains("{{", StringComparison.Ordinal)
                && !ShouldReplaceIdentifierLiteral(kvp.Value))
            .Select(kvp => kvp.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (availableIdentifierValues.Count == 1
            && IsSafeDuplicateIdentifierAliasValue(availableIdentifierValues[0]))
        {
            resolved = availableIdentifierValues[0];
            return true;
        }

        resolved = null;
        return false;
    }

    private static bool IsSafeDuplicateIdentifierAliasValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Contains("{{", StringComparison.Ordinal))
        {
            return false;
        }

        // Treat compact 32-char hex values as opaque/session-like for idId aliasing.
        if (IsHex32String(normalized))
        {
            return false;
        }

        if (long.TryParse(normalized, out _)
            || Guid.TryParse(normalized, out _)
            || ObjectIdRegex.IsMatch(normalized))
        {
            return true;
        }

        return true;
    }

    private static bool IsHex32String(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 32)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> NormalizePathParams(
        IReadOnlyDictionary<string, string> pathParams,
        string resolvedUrl,
        IReadOnlyDictionary<string, string> variables,
        bool allowIdentifierLiteralReplacement)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (pathParams == null || pathParams.Count == 0)
        {
            return normalized;
        }

        foreach (var kvp in pathParams)
        {
            var resolvedValue = kvp.Value;
            if (allowIdentifierLiteralReplacement &&
                (ShouldReplaceIdentifierLiteral(resolvedValue)
                    || IsLikelyObjectIdLiteralPlaceholder(resolvedValue)) &&
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
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || variables == null
            || !string.Equals(testCase?.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase))
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
                        (ShouldReplaceIdentifierLiteral(currentValue)
                            || IsLikelyObjectIdLiteralPlaceholder(currentValue)) &&
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

        if (long.TryParse(normalized, out var numericValue))
        {
            return numericValue is 0 or 1 or 12345;
        }

        return false;
    }

    private static bool IsLikelyObjectIdLiteralPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return ObjectIdRegex.IsMatch(normalized);
    }

    private static string TryApplyHappyPathLiteralRouteReplacement(
        ExecutionTestCaseDto testCase,
        string resolvedUrl,
        IReadOnlyDictionary<string, string> resolvedPathParams,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedUrl)
            || !string.Equals(testCase?.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || resolvedUrl.Contains("{", StringComparison.Ordinal))
        {
            return resolvedUrl;
        }

        var replacementValue = SelectPreferredResolvedPathParamValue(resolvedPathParams);
        if (string.IsNullOrWhiteSpace(replacementValue)
            && TryResolveVariableValue(BuildRouteIdentifierVariableCandidates(resolvedUrl), variables, out var fallbackVariableValue))
        {
            replacementValue = fallbackVariableValue;
        }

        if (string.IsNullOrWhiteSpace(replacementValue) || replacementValue.Contains("{{", StringComparison.Ordinal))
        {
            return resolvedUrl;
        }

        return TryReplaceLikelyIdentifierSegment(resolvedUrl, replacementValue, out var rewritten)
            ? rewritten
            : resolvedUrl;
    }

    private static IEnumerable<string> BuildRouteIdentifierVariableCandidates(string resolvedUrl)
    {
        var candidates = new List<string>();

        var resourceIdVariableName = BuildResourceIdVariableNameFromLiteralPath(resolvedUrl);
        if (!string.IsNullOrWhiteSpace(resourceIdVariableName))
        {
            candidates.Add(resourceIdVariableName);
        }

        candidates.Add("resourceId");
        candidates.Add("id");

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildResourceIdVariableNameFromLiteralPath(string urlOrPath)
    {
        var path = ExtractPathOnly(urlOrPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var identifierSegmentIndex = Array.FindLastIndex(segments, IsLikelySyntheticIdentifierSegment);
        if (identifierSegmentIndex <= 0)
        {
            return null;
        }

        for (var i = identifierSegmentIndex - 1; i >= 0; i--)
        {
            var segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment)
                || string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase)
                || IsVersionSegment(segment)
                || (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)))
            {
                continue;
            }

            var identifier = ToCamelIdentifier(Singularize(segment));
            return string.IsNullOrWhiteSpace(identifier) ? null : identifier + "Id";
        }

        return null;
    }

    private static string SelectPreferredResolvedPathParamValue(IReadOnlyDictionary<string, string> resolvedPathParams)
    {
        if (resolvedPathParams == null || resolvedPathParams.Count == 0)
        {
            return null;
        }

        if (resolvedPathParams.TryGetValue("id", out var idValue) && !string.IsNullOrWhiteSpace(idValue))
        {
            return idValue;
        }

        var keyedId = resolvedPathParams
            .FirstOrDefault(kvp => kvp.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kvp.Value));
        if (!string.IsNullOrWhiteSpace(keyedId.Value))
        {
            return keyedId.Value;
        }

        return resolvedPathParams
            .FirstOrDefault(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Value;
    }

    private static bool TryReplaceLikelyIdentifierSegment(string url, string replacement, out string rewrittenUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(replacement))
        {
            rewrittenUrl = url;
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            var segments = absolute.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var index = segments.FindLastIndex(IsLikelySyntheticIdentifierSegment);
            if (index < 0)
            {
                rewrittenUrl = url;
                return false;
            }

            segments[index] = Uri.EscapeDataString(replacement);
            var builder = new UriBuilder(absolute)
            {
                Path = "/" + string.Join("/", segments),
            };

            rewrittenUrl = builder.Uri.AbsoluteUri;
            return true;
        }

        if (!TrySplitPathAndSuffix(url, out var pathPart, out var suffix))
        {
            rewrittenUrl = url;
            return false;
        }

        var relativeSegments = pathPart
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var relativeIndex = relativeSegments.FindLastIndex(IsLikelySyntheticIdentifierSegment);
        if (relativeIndex < 0)
        {
            rewrittenUrl = url;
            return false;
        }

        relativeSegments[relativeIndex] = Uri.EscapeDataString(replacement);
        var normalizedPath = (pathPart.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty)
            + string.Join("/", relativeSegments);

        rewrittenUrl = normalizedPath + suffix;
        return true;
    }

    private static bool IsLikelySyntheticIdentifierSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        var decoded = Uri.UnescapeDataString(segment).Trim();
        if (string.IsNullOrWhiteSpace(decoded) || decoded.Contains("{{", StringComparison.Ordinal))
        {
            return false;
        }

        if (Guid.TryParse(decoded, out var guidValue))
        {
            return guidValue == Guid.Empty || decoded.StartsWith("00000000", StringComparison.OrdinalIgnoreCase);
        }

        if (decoded is "0" or "1" or "12345")
        {
            return true;
        }

        if (IsLikelyObjectIdLiteralPlaceholder(decoded))
        {
            return true;
        }

        return decoded.All(char.IsDigit) && decoded.Length >= 3;
    }

    private static bool TrySplitPathAndSuffix(string url, out string pathPart, out string suffix)
    {
        if (url == null)
        {
            pathPart = null;
            suffix = string.Empty;
            return false;
        }

        var splitIndex = url.IndexOfAny(new[] { '?', '#' });
        if (splitIndex < 0)
        {
            pathPart = url;
            suffix = string.Empty;
            return true;
        }

        pathPart = url[..splitIndex];
        suffix = url[splitIndex..];
        return true;
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

    private static string NormalizeNumericPlaceholderDefaultsInJsonBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody))
        {
            return resolvedBody;
        }

        var preferInvalidFallback = IsLikelyErrorCase(testCase);

        var textRewritten = ReplaceNumericPlaceholderLiterals(resolvedBody, preferInvalidFallback);
        if (!string.Equals(textRewritten, resolvedBody, StringComparison.Ordinal))
        {
            resolvedBody = textRewritten;
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

        var changed = ReplaceMissingNumericPlaceholders(root, preferInvalidFallback);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string NormalizeTextPlaceholderDefaultsInJsonBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody))
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

        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var preferInvalidFallback = IsLikelyErrorCase(testCase);
        var changed = ReplaceMissingTextPlaceholders(root, suffix, preferInvalidFallback);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string ReplaceNumericPlaceholderLiterals(string body, bool preferInvalidFallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        return InlinePropertyPlaceholderRegex.Replace(body, match =>
        {
            var propertyName = match.Groups["prop"].Value;
            var placeholderName = match.Groups["name"].Value;
            if (!TryGetNumericPlaceholderDefaultLiteral(propertyName, placeholderName, preferInvalidFallback, out var defaultLiteral))
            {
                return match.Value;
            }

            return $"\"{propertyName}\":{defaultLiteral}";
        });
    }

    private static bool ReplaceMissingNumericPlaceholders(JsonNode node, bool preferInvalidFallback)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var raw)
                    && TryExtractExactPlaceholder(raw, out var placeholderName)
                    && TryBuildNumericPlaceholderDefaultNode(property.Key, placeholderName, preferInvalidFallback, out var replacementNode))
                {
                    obj[property.Key] = replacementNode;
                    changed = true;
                    continue;
                }

                if (property.Value != null && ReplaceMissingNumericPlaceholders(property.Value, preferInvalidFallback))
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
                if (item != null && ReplaceMissingNumericPlaceholders(item, preferInvalidFallback))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static bool ReplaceMissingTextPlaceholders(JsonNode node, string suffix, bool preferInvalidFallback)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var raw))
                {
                    var rewritten = RewriteTextPlaceholderValue(raw, property.Key, suffix, preferInvalidFallback);
                    if (!string.Equals(rewritten, raw, StringComparison.Ordinal))
                    {
                        obj[property.Key] = rewritten;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null && ReplaceMissingTextPlaceholders(property.Value, suffix, preferInvalidFallback))
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
                if (item != null && ReplaceMissingTextPlaceholders(item, suffix, preferInvalidFallback))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static string RewriteTextPlaceholderValue(string rawValue, string propertyName, string suffix, bool preferInvalidFallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return rawValue;
        }

        return PlaceholderRegex.Replace(rawValue, match =>
        {
            var placeholderName = match.Groups[1].Value;
            if (IsIdentifierSemanticVariableName(placeholderName)
                || IsIdentifierField(propertyName)
                || TryGetNumericPlaceholderDefaultLiteral(propertyName, placeholderName, preferInvalidFallback, out _))
            {
                return match.Value;
            }

            return BuildSyntheticPlaceholderValue(propertyName, placeholderName, suffix, preferInvalidFallback);
        });
    }

    private static string BuildSyntheticPlaceholderValue(string propertyName, string placeholderName, string suffix, bool preferInvalidFallback)
    {
        var probe = $"{propertyName} {placeholderName}".ToLowerInvariant();

        if (preferInvalidFallback)
        {
            if (probe.Contains("email", StringComparison.Ordinal))
            {
                return "invalid-email";
            }

            if (probe.Contains("url", StringComparison.Ordinal) || probe.Contains("link", StringComparison.Ordinal))
            {
                return "not-a-url";
            }

            if (probe.Contains("phone", StringComparison.Ordinal) || probe.Contains("mobile", StringComparison.Ordinal))
            {
                return "invalid-phone";
            }

            if (probe.Contains("password", StringComparison.Ordinal))
            {
                return "123";
            }

            return string.Empty;
        }

        if (probe.Contains("email", StringComparison.Ordinal))
        {
            return $"autogen.{suffix}@example.test";
        }

        if (probe.Contains("description", StringComparison.Ordinal)
            || probe.Contains("content", StringComparison.Ordinal)
            || probe.Contains("message", StringComparison.Ordinal)
            || probe.Contains("note", StringComparison.Ordinal))
        {
            return $"Auto generated {placeholderName} {suffix}";
        }

        if (probe.Contains("name", StringComparison.Ordinal)
            || probe.Contains("title", StringComparison.Ordinal)
            || probe.Contains("label", StringComparison.Ordinal)
            || probe.Contains("slug", StringComparison.Ordinal))
        {
            return $"Auto {placeholderName} {suffix}";
        }

        if (probe.Contains("password", StringComparison.Ordinal))
        {
            var shortSuffix = suffix.Length >= 4 ? suffix[^4..] : suffix;
            return $"P@ssw0rd!{shortSuffix}";
        }

        if (probe.Contains("url", StringComparison.Ordinal) || probe.Contains("link", StringComparison.Ordinal))
        {
            return $"https://example.test/{suffix}";
        }

        if (probe.Contains("phone", StringComparison.Ordinal) || probe.Contains("mobile", StringComparison.Ordinal))
        {
            return "0900000000";
        }

        return $"auto-{placeholderName}-{suffix}";
    }

    private static bool TryExtractExactPlaceholder(string value, out string placeholderName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var match = ExactPlaceholderRegex.Match(value.Trim());
            if (match.Success)
            {
                placeholderName = match.Groups["name"].Value;
                return true;
            }
        }

        placeholderName = null;
        return false;
    }

    private static bool TryBuildNumericPlaceholderDefaultNode(
        string propertyName,
        string placeholderName,
        bool preferInvalidFallback,
        out JsonNode replacementNode)
    {
        if (!TryGetNumericPlaceholderDefaultLiteral(propertyName, placeholderName, preferInvalidFallback, out var defaultLiteral))
        {
            replacementNode = null;
            return false;
        }

        replacementNode = defaultLiteral switch
        {
            "9.99" => JsonValue.Create(9.99m),
            "-1.0" => JsonValue.Create(-1.0m),
            "-1" => JsonValue.Create(-1),
            _ => JsonValue.Create(1),
        };
        return true;
    }

    private static bool TryGetNumericPlaceholderDefaultLiteral(
        string propertyName,
        string placeholderName,
        bool preferInvalidFallback,
        out string defaultLiteral)
    {
        var probe = ($"{propertyName} {placeholderName}").ToLowerInvariant();

        if (IsKnownDuplicatedIdentifierPlaceholderName(placeholderName))
        {
            defaultLiteral = preferInvalidFallback ? "-1" : "1";
            return true;
        }

        if (probe.Contains("price", StringComparison.Ordinal)
            || probe.Contains("amount", StringComparison.Ordinal)
            || probe.Contains("cost", StringComparison.Ordinal)
            || probe.Contains("rate", StringComparison.Ordinal)
            || probe.Contains("percent", StringComparison.Ordinal)
            || probe.Contains("percentage", StringComparison.Ordinal))
        {
            defaultLiteral = preferInvalidFallback ? "-1.0" : "9.99";
            return true;
        }

        if (probe.Contains("stock", StringComparison.Ordinal)
            || probe.Contains("quantity", StringComparison.Ordinal)
            || probe.Contains("qty", StringComparison.Ordinal)
            || probe.Contains("count", StringComparison.Ordinal)
            || probe.Contains("total", StringComparison.Ordinal)
            || probe.Contains("number", StringComparison.Ordinal)
            || probe.Contains("num", StringComparison.Ordinal)
            || probe.Contains("limit", StringComparison.Ordinal)
            || probe.Contains("offset", StringComparison.Ordinal))
        {
            defaultLiteral = preferInvalidFallback ? "-1" : "1";
            return true;
        }

        defaultLiteral = null;
        return false;
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

    private static bool IsLikelyErrorCase(ExecutionTestCaseDto testCase)
    {
        if (testCase == null)
        {
            return false;
        }

        return ContainsAny(testCase.TestType, "negative", "boundary", "invalid")
            || ContainsAny(testCase.Name, "negative", "boundary", "invalid");
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
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
