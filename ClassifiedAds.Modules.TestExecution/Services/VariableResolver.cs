using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;

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
    private static readonly Regex AuthHeaderTemplateRegex = new(
        @"^(?<scheme>[A-Za-z][A-Za-z0-9_-]*)\s*[:=]?\s*\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}\s*$",
        RegexOptions.Compiled);
    private static readonly Regex AuthHeaderValueWithSeparatorRegex = new(
        @"^(?<scheme>[A-Za-z][A-Za-z0-9_-]*)\s*[:=]\s*(?<value>.+)$",
        RegexOptions.Compiled);
    private static readonly string[] AuthModeHeaderNames =
    {
        "X-Test-Auth-Mode",
        "X-Auth-Mode",
        "X-LLM-Auth-Mode",
    };
    private static readonly Regex ObjectIdRegex = new(@"^[a-fA-F0-9]{24}$", RegexOptions.Compiled);
    private static readonly Regex RouteTokenRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex SimpleSyntheticNameRegex = new(@"^[A-Za-z0-9 _\-]{2,80}$", RegexOptions.Compiled);
    private static readonly Regex DuplicatedIdentifierPlaceholderRegex = new(@"IdId(s)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const string CredentialPolicyTagPrefix = "cred-policy:";
    private const string CredentialLockTagPrefix = "cred-lock:";
    private const string AuthModeTagPrefix = "auth-mode:";
    private const string FlowRequiredTagPrefix = "flow-required:";
    private const string FlowDependsOnTagPrefix = "flow-depends-on:";
    private const string FlowProducesTagPrefix = "flow-produces:";
    private const string FlowConsumesTagPrefix = "flow-consumes:";
    private const string RewritePolicyTagPrefix = "rewrite-policy:";
    private const string AuthFallbackTagPrefix = "auth-fallback:";
    // Matches values already uniquified by {{tcUniqueId}} resolution — local part ends with _xxxxxxxx (8 hex chars).
    private static readonly Regex AlreadyUniquifiedRegex = new(@"_[a-f0-9]{8}$", RegexOptions.Compiled);

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

        // Inject per-EXECUTION unique ID so LLM-generated bodies can embed {{tcUniqueId}}
        // in any field requiring uniqueness (email, username, code, slug, etc.) without
        // the BE needing to know which field names exist in the request payload.
        // Must be random per execution (not derived from TestCaseId) so repeated runs
        // of the same test case never collide on unique-constraint fields (e.g. email).
        mergedVars["tcUniqueId"] = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();

        // Flow-first: materialize declared consumed variables from dependency-scoped outputs
        // (case.<dependencyId>.<var>) before any header/body placeholder resolution.
        PromoteFlowConsumesFromDependencies(testCase, mergedVars);

        ApplyTokenAliases(mergedVars);
        var allowHeuristicRewrite = IsRewritePolicyEnabled(testCase);
        var resolutionTrace = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Resolve URL
        var resolvedUrl = ResolvePlaceholdersWithProvenance(
            request.Url ?? string.Empty,
            "url",
            mergedVars,
            testCase,
            variables,
            environment?.Variables,
            resolutionTrace);

        // Resolve path params and apply to URL
        var pathParams = DeserializeDictionary(request.PathParams);
        var allowIdentifierLiteralReplacement = AllowsIdentifierLiteralReplacement(testCase);
        var normalizedPathParams = NormalizePathParams(testCase, pathParams, resolvedUrl, mergedVars, allowIdentifierLiteralReplacement);
        var resolvedPathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var routeTokenApplied = false;
        foreach (var kvp in normalizedPathParams)
        {
            var resolvedValue = ResolvePlaceholdersWithProvenance(
                kvp.Value,
                $"path.{kvp.Key}",
                mergedVars,
                testCase,
                variables,
                environment?.Variables,
                resolutionTrace);
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
            resolvedQuery[kvp.Key] = ResolvePlaceholdersWithProvenance(
                kvp.Value,
                $"query.{kvp.Key}",
                mergedVars,
                testCase,
                variables,
                environment?.Variables,
                resolutionTrace);
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
        var hasUnresolvedConsumedAuthPlaceholder = false;
        foreach (var kvp in requestHeaders)
        {
            var resolvedValue = ResolvePlaceholdersWithProvenance(
                kvp.Value,
                $"headers.{kvp.Key}",
                mergedVars,
                testCase,
                variables,
                environment?.Variables,
                resolutionTrace);
            var unresolvedConsumedAuthPlaceholder =
                IsAuthHeaderName(kvp.Key)
                && ContainsUnresolvedPlaceholder(resolvedValue)
                && TryExtractAuthPlaceholderName(kvp.Value, out var authPlaceholderName)
                && IsFlowConsumedVariable(testCase, authPlaceholderName);

            // Keep concrete auth header from environment when request-level auth template
            // is still unresolved (e.g., Bearer {{authToken}} but token not yet materialized).
            if (IsAuthHeaderName(kvp.Key)
                && ContainsUnresolvedPlaceholder(resolvedValue)
                && !unresolvedConsumedAuthPlaceholder
                && resolvedHeaders.TryGetValue(kvp.Key, out var existingAuthValue)
                && !ContainsUnresolvedPlaceholder(existingAuthValue))
            {
                continue;
            }

            if (unresolvedConsumedAuthPlaceholder)
            {
                hasUnresolvedConsumedAuthPlaceholder = true;
            }

            resolvedHeaders[kvp.Key] = resolvedValue;
        }

        var requestHasAuthHeader = HasExplicitAuthHeader(requestHeaders);
        var authMode = ResolveAuthModeFromHeaders(resolvedHeaders)
            ?? ResolveAuthModeFromTags(testCase);
        if (TryConsumeNoAuthSentinel(resolvedHeaders))
        {
            authMode = "none";
        }

        var disableAuth = string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase);
        var optionalAuth = string.Equals(authMode, "optional", StringComparison.OrdinalIgnoreCase);
        var requiredAuth = string.Equals(authMode, "required", StringComparison.OrdinalIgnoreCase);
        // No-auth intent must win even when the generator accidentally sends
        // an Authorization header in the request template.
        var inferredMissingAuthScenario = IsNoAuthExpectationScenario(testCase);
        if (disableAuth || inferredMissingAuthScenario || (optionalAuth && !requestHasAuthHeader))
        {
            RemoveAuthHeaders(resolvedHeaders);
        }

        if (!disableAuth)
        {
            ApplyAuthHeaderTemplates(resolvedHeaders, mergedVars);

            // Only inject fallback token when auth intent is explicit:
            // - n8n sets auth-mode=required, or
            // - request explicitly declares an auth header template/value.
            // Never inject auth fallback for "no-auth" scenarios inferred from
            // expectation/name/description (e.g. expected 401/403, no token cases).
            if (!inferredMissingAuthScenario
                && !hasUnresolvedConsumedAuthPlaceholder
                && (requiredAuth || requestHasAuthHeader)
                && IsAuthFallbackAllowed(testCase))
            {
                ApplyAuthFallbackHeader(resolvedHeaders, mergedVars);
            }
        }

        // Resolve body
        var resolvedBody = !string.IsNullOrEmpty(request.Body)
            ? ResolvePlaceholdersWithProvenance(
                request.Body,
                "body",
                mergedVars,
                testCase,
                variables,
                environment?.Variables,
                resolutionTrace)
            : null;

        if (allowHeuristicRewrite)
        {
            // Assisted mode only: these normalizations mutate body payload.
            resolvedBody = NormalizeNumericPlaceholderDefaultsInJsonBody(testCase, resolvedBody);
            resolvedBody = NormalizeDependencyScopedCredentialsByPolicy(testCase, resolvedBody, mergedVars);
            resolvedBody = NormalizeDependencyScopedBodyBindingsForFlow(testCase, resolvedBody, mergedVars);
            resolvedBody = NormalizeDependencyScopedConsumesBindings(testCase, resolvedBody, mergedVars);
        }
        if (allowHeuristicRewrite)
        {
            resolvedBody = NormalizeCrossResourceIdentifierBindings(testCase, resolvedBody, resolvedUrl, mergedVars);
        }

        if (allowHeuristicRewrite && !IsLlmSourced(testCase))
        {
            // Non-LLM test cases: run the full normalization pipeline.
            resolvedBody = NormalizeCredentialsByPolicy(testCase, resolvedBody, mergedVars);
            resolvedBody = NormalizeIdentifierLiteralsInJsonBody(testCase, resolvedBody, mergedVars);
            resolvedBody = NormalizeHappyPathSyntheticBody(testCase, resolvedBody, mergedVars);
        }
        // Always apply safe text placeholder fallback as the final guard.
        // This prevents runtime UNRESOLVED_VARIABLE failures when generators emit
        // undeclared text placeholders (e.g. {{categoryName}}) in request bodies.
        if (allowHeuristicRewrite)
        {
            resolvedBody = NormalizeTextPlaceholderDefaultsInJsonBody(testCase, resolvedBody);
        }

        // Build final URL
        resolvedUrl = NormalizeSwaggerDocsApiUrl(resolvedUrl);
        var finalUrl = BuildFinalUrl(resolvedUrl, environment.BaseUrl);

        // Hard-block auth for no-auth scenarios: strip any auth-like headers/query params
        // right before request materialization so no later merge/fallback can leak tokens.
        if (IsNoAuthExpectationScenario(testCase))
        {
            RemoveAuthHeaders(resolvedHeaders);
            RemoveAggressiveAuthLikeHeaders(resolvedHeaders);
            RemoveAggressiveAuthLikeQueryParams(resolvedQuery);
        }

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
            TcUniqueId = mergedVars.TryGetValue("tcUniqueId", out var tcId) ? tcId : null,
            VariableResolutionTrace = resolutionTrace,
        };
    }

    private static string ResolvePlaceholdersWithProvenance(
        string template,
        string surface,
        Dictionary<string, string> mergedVars,
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> runVariables,
        IReadOnlyDictionary<string, string> envVariables,
        IDictionary<string, string> traceSink)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var resolved = ResolvePlaceholders(template, mergedVars);
        if (traceSink == null)
        {
            return resolved;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in PlaceholderRegex.Matches(template))
        {
            if (!m.Success || m.Groups.Count < 2)
            {
                continue;
            }

            var placeholderName = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(placeholderName) || !seen.Add(placeholderName))
            {
                continue;
            }

            if (!mergedVars.TryGetValue(placeholderName, out var candidate)
                || string.IsNullOrWhiteSpace(candidate)
                || candidate.Contains("{{", StringComparison.Ordinal))
            {
                traceSink[$"{surface}.{placeholderName}"] = "unresolved";
                continue;
            }

            traceSink[$"{surface}.{placeholderName}"] = ResolvePlaceholderSource(
                placeholderName,
                testCase,
                runVariables,
                envVariables);
        }

        return resolved;
    }

    private static string ResolvePlaceholderSource(
        string placeholderName,
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> runVariables,
        IReadOnlyDictionary<string, string> envVariables)
    {
        if (string.Equals(placeholderName, "tcUniqueId", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime";
        }

        if (testCase?.DependencyIds != null
            && runVariables != null
            && testCase.DependencyIds.Count > 0)
        {
            for (var i = testCase.DependencyIds.Count - 1; i >= 0; i--)
            {
                var depId = testCase.DependencyIds[i];
                var scopedKey = $"case.{depId:N}.{placeholderName}";
                if (runVariables.TryGetValue(scopedKey, out var scopedValue)
                    && !string.IsNullOrWhiteSpace(scopedValue)
                    && !scopedValue.Contains("{{", StringComparison.Ordinal))
                {
                    return $"dependency-scoped(case.{depId:N})";
                }
            }
        }

        if (runVariables != null
            && runVariables.TryGetValue(placeholderName, out var globalValue)
            && !string.IsNullOrWhiteSpace(globalValue)
            && !globalValue.Contains("{{", StringComparison.Ordinal))
        {
            return "global";
        }

        if (envVariables != null
            && envVariables.TryGetValue(placeholderName, out var envValue)
            && !string.IsNullOrWhiteSpace(envValue)
            && !envValue.Contains("{{", StringComparison.Ordinal))
        {
            return "env";
        }

        return "merged";
    }

    private static string NormalizeCrossResourceIdentifierBindings(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        string resolvedUrl,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody)
            || variables == null
            || variables.Count == 0
            || IsLikelyErrorCase(testCase))
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

        var routeResourceIdVar = BuildResourceIdVariableNameFromLiteralPath(resolvedUrl);
        variables.TryGetValue(routeResourceIdVar ?? string.Empty, out var routeResourceIdValue);

        var changed = FixCrossResourceIdentifierBindings(root, testCase, variables, routeResourceIdVar, routeResourceIdValue);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static bool FixCrossResourceIdentifierBindings(
        JsonNode node,
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables,
        string routeResourceIdVar,
        string routeResourceIdValue)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue
                    && IsIdentifierField(property.Key)
                    && !string.Equals(property.Key, "id", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(property.Key, routeResourceIdVar, StringComparison.OrdinalIgnoreCase)
                    && TryResolveVariableValueFromDependencies(testCase, new[] { property.Key }, variables, allowGlobalFallback: false, out var preferredIdentifier)
                    && !string.IsNullOrWhiteSpace(preferredIdentifier)
                    && !preferredIdentifier.Contains("{{", StringComparison.Ordinal))
                {
                    var currentValue = property.Value?.ToJsonString(JsonOptions)?.Trim('"');
                    if (ShouldPreserveExplicitIdentifierLiteral(testCase, currentValue))
                    {
                        continue;
                    }
                    var looksWrongBinding =
                        string.IsNullOrWhiteSpace(currentValue)
                        || currentValue.Contains("{{", StringComparison.Ordinal)
                        || ShouldReplaceIdentifierLiteral(currentValue)
                        || IsLikelyObjectIdLiteralPlaceholder(currentValue)
                        || (!string.IsNullOrWhiteSpace(routeResourceIdValue)
                            && string.Equals(currentValue, routeResourceIdValue, StringComparison.OrdinalIgnoreCase));

                    if (looksWrongBinding && !string.Equals(currentValue, preferredIdentifier, StringComparison.OrdinalIgnoreCase))
                    {
                        obj[property.Key] = preferredIdentifier;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null
                    && FixCrossResourceIdentifierBindings(property.Value, testCase, variables, routeResourceIdVar, routeResourceIdValue))
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
                if (item != null
                    && FixCrossResourceIdentifierBindings(item, testCase, variables, routeResourceIdVar, routeResourceIdValue))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
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
        ExecutionTestCaseDto testCase,
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
            var candidates = BuildPathParamVariableCandidates(kvp.Key, resolvedUrl);
            if (allowIdentifierLiteralReplacement &&
                (ShouldReplaceIdentifierLiteral(resolvedValue)
                    || IsLikelyObjectIdLiteralPlaceholder(resolvedValue)) &&
                TryResolveVariableValueFromDependencies(testCase, candidates, variables, allowGlobalFallback: !HasDependencies(testCase), out var replacement))
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

    private static void ApplyAuthHeaderTemplates(
        IDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> variables)
    {
        if (headers == null)
        {
            return;
        }

        foreach (var header in headers.ToList())
        {
            if (!IsAuthHeaderName(header.Key) || string.IsNullOrWhiteSpace(header.Value))
            {
                continue;
            }

            if (TryParseAuthHeaderTemplate(header.Value, out var templateScheme, out var variableName))
            {
                if (variables == null)
                {
                    continue;
                }

                if (variables.TryGetValue(variableName, out var resolvedValue)
                    && !string.IsNullOrWhiteSpace(resolvedValue)
                    && !ContainsUnresolvedPlaceholder(resolvedValue))
                {
                    headers[header.Key] = FormatAuthHeaderValue(templateScheme, resolvedValue);
                }

                continue;
            }

            if (!TryParseAuthHeaderValueWithSeparator(header.Value, out var scheme, out var currentValue))
            {
                continue;
            }

            if (ContainsUnresolvedPlaceholder(currentValue))
            {
                continue;
            }

            headers[header.Key] = FormatAuthHeaderValue(scheme, currentValue);
        }
    }

    private static void ApplyAuthFallbackHeader(
        IDictionary<string, string> headers,
        IReadOnlyDictionary<string, string> variables)
    {
        if (headers == null || variables == null)
        {
            return;
        }

        if (TryGetFirstPresentHeader(headers, new[] { "Authorization", "Proxy-Authorization", "X-Authorization", "X-Auth", "X-API-Key", "Api-Key" }, out var existingAuthHeader))
        {
            if (!ContainsUnresolvedPlaceholder(existingAuthHeader.Value))
            {
                return;
            }

            if (TryGetFirstTokenValue(variables, out var resolvedToken))
            {
                var scheme = ResolvePreferredAuthScheme(existingAuthHeader.Key, existingAuthHeader.Value);
                headers[existingAuthHeader.Key] = FormatAuthHeaderValue(scheme, resolvedToken);
            }

            return;
        }

        if (TryGetFirstTokenValue(variables, out var fallbackToken))
        {
            headers["Authorization"] = $"Bearer {fallbackToken}";
        }
    }

    private static string FormatAuthHeaderValue(string scheme, string value)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return value;
        }

        return string.Equals(scheme, "ApiKey", StringComparison.OrdinalIgnoreCase)
            ? $"ApiKey {value}"
            : $"{scheme} {value}";
    }

    private static bool TryParseAuthHeaderTemplate(string headerValue, out string scheme, out string variableName)
    {
        scheme = null;
        variableName = null;

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var match = AuthHeaderTemplateRegex.Match(headerValue);
        if (!match.Success)
        {
            return false;
        }

        scheme = match.Groups["scheme"].Value.Trim();
        variableName = match.Groups["name"].Value.Trim();
        return !string.IsNullOrWhiteSpace(scheme) && !string.IsNullOrWhiteSpace(variableName);
    }

    private static bool TryParseAuthHeaderValueWithSeparator(string headerValue, out string scheme, out string value)
    {
        scheme = null;
        value = null;

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var match = AuthHeaderValueWithSeparatorRegex.Match(headerValue);
        if (!match.Success)
        {
            return false;
        }

        scheme = match.Groups["scheme"].Value.Trim();
        value = match.Groups["value"].Value.Trim();

        return !string.IsNullOrWhiteSpace(scheme) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetFirstPresentHeader(
        IDictionary<string, string> headers,
        IEnumerable<string> candidateHeaders,
        out KeyValuePair<string, string> found)
    {
        if (headers == null || candidateHeaders == null)
        {
            found = default;
            return false;
        }

        foreach (var headerName in candidateHeaders)
        {
            if (headers.TryGetValue(headerName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                found = new KeyValuePair<string, string>(headerName, value);
                return true;
            }
        }

        found = default;
        return false;
    }

    private static string ResolveAuthModeFromHeaders(IDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return null;
        }

        foreach (var name in AuthModeHeaderNames)
        {
            if (headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                headers.Remove(name);
                return NormalizeAuthMode(value);
            }

            headers.Remove(name);
        }

        return null;
    }

    private static string NormalizeAuthMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "none" or "noauth" or "disableauth" => "none",
            "optional" => "optional",
            "required" or "default" => "required",
            _ => null,
        };
    }

    private static bool HasExplicitAuthHeader(IDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return false;
        }

        foreach (var kvp in headers)
        {
            if (IsAuthHeaderName(kvp.Key)
                && !string.IsNullOrWhiteSpace(kvp.Value)
                && !IsNoAuthSentinel(kvp.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveAuthHeaders(IDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return;
        }

        foreach (var headerName in headers.Keys.ToList())
        {
            if (IsAuthHeaderName(headerName))
            {
                headers.Remove(headerName);
            }
        }
    }

    private static bool TryConsumeNoAuthSentinel(IDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return false;
        }

        var removed = false;
        foreach (var headerName in headers.Keys.ToList())
        {
            if (IsAuthHeaderName(headerName) && IsNoAuthSentinel(headers[headerName]))
            {
                headers.Remove(headerName);
                removed = true;
            }
        }

        return removed;
    }

    private static bool IsNoAuthSentinel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized == "__no_auth__"
            || normalized == "no_auth"
            || normalized == "no-auth"
            || normalized == "noauth";
    }

    private static void PromoteFlowConsumesFromDependencies(
        ExecutionTestCaseDto testCase,
        IDictionary<string, string> mergedVars)
    {
        if (testCase == null || mergedVars == null || !HasDependencies(testCase))
        {
            return;
        }

        var consumes = GetTagValues(testCase.Tags, FlowConsumesTagPrefix)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (consumes.Count == 0)
        {
            return;
        }

        var readOnly = new Dictionary<string, string>(mergedVars, StringComparer.OrdinalIgnoreCase);
        foreach (var consume in consumes)
        {
            var candidates = new List<string> { consume };
            if (consume.StartsWith("request.body.", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(consume["request.body.".Length..]);
            }

            // Contract-first flow: when dependencies exist, consumed variables must come
            // from dependency-scoped values, not from global/environment leftovers.
            foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                mergedVars.Remove(c);
            }

            if (TryResolveVariableValueFromDependencies(
                    testCase,
                    candidates,
                    readOnly,
                    allowGlobalFallback: false,
                    out var resolved)
                && !string.IsNullOrWhiteSpace(resolved))
            {
                foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    mergedVars[c] = resolved;
                }
            }
        }
    }

    private static bool TryExtractAuthPlaceholderName(string headerValue, out string variableName)
    {
        variableName = null;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var match = Regex.Match(headerValue, @"\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}");
        if (!match.Success)
        {
            return false;
        }

        variableName = match.Groups["name"].Value?.Trim();
        return !string.IsNullOrWhiteSpace(variableName);
    }

    private static bool IsFlowConsumedVariable(ExecutionTestCaseDto testCase, string variableName)
    {
        if (testCase == null || string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        var consumes = GetTagValues(testCase.Tags, FlowConsumesTagPrefix);
        return consumes.Any(v =>
            string.Equals(v, variableName, StringComparison.OrdinalIgnoreCase)
            || v.EndsWith("." + variableName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetFirstTokenValue(
        IReadOnlyDictionary<string, string> variables,
        out string tokenValue)
    {
        tokenValue = null;
        if (variables == null || variables.Count == 0)
        {
            return false;
        }

        foreach (var candidateKey in new[]
                 {
                     "authToken",
                     "auth_token",
                     "accessToken",
                     "access_token",
                     "token",
                     "jwt",
                     "idToken",
                     "id_token",
                     "sessionToken",
                     "session_token",
                     "bearerToken",
                     "bearer_token",
                     "apiKey",
                     "apikey",
                 })
        {
            if (variables.TryGetValue(candidateKey, out var candidate)
                && !string.IsNullOrWhiteSpace(candidate)
                && !ContainsUnresolvedPlaceholder(candidate))
            {
                tokenValue = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ResolvePreferredAuthScheme(string headerName, string headerValue)
    {
        if (TryParseAuthHeaderTemplate(headerValue, out var templateScheme, out _))
        {
            return templateScheme;
        }

        if (string.Equals(headerName, "X-API-Key", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Api-Key", StringComparison.OrdinalIgnoreCase))
        {
            return "ApiKey";
        }

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            var trimmed = headerValue.Trim();
            if (trimmed.StartsWith("Token", StringComparison.OrdinalIgnoreCase))
            {
                return "Token";
            }

            if (trimmed.StartsWith("ApiKey", StringComparison.OrdinalIgnoreCase))
            {
                return "ApiKey";
            }

            if (trimmed.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
            {
                return "Basic";
            }
        }

        return "Bearer";
    }

    private static bool IsAuthHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        var normalized = NormalizeHeaderNameForMatch(headerName);
        return normalized is "authorization"
            or "proxyauthorization"
            or "xauthorization"
            or "xauth"
            or "xapikey"
            or "apikey"
            or "authtoken"
            or "accesstoken"
            or "bearertoken";
    }

    private static void RemoveAggressiveAuthLikeHeaders(IDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return;
        }

        foreach (var key in headers.Keys.ToList())
        {
            if (LooksLikeAuthSurface(key))
            {
                headers.Remove(key);
            }
        }
    }

    private static void RemoveAggressiveAuthLikeQueryParams(IDictionary<string, string> queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return;
        }

        foreach (var key in queryParams.Keys.ToList())
        {
            if (LooksLikeAuthSurface(key))
            {
                queryParams.Remove(key);
            }
        }
    }

    private static bool LooksLikeAuthSurface(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = NormalizeHeaderNameForMatch(key);
        return normalized.Contains("auth", StringComparison.Ordinal)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("bearer", StringComparison.Ordinal)
            || normalized.Contains("accesskey", StringComparison.Ordinal);
    }

    private static string NormalizeHeaderNameForMatch(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return string.Empty;
        }

        var chars = headerName
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private static bool ContainsUnresolvedPlaceholder(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains("{{", StringComparison.Ordinal)
            && value.Contains("}}", StringComparison.Ordinal);
    }

    private static void ApplyTokenAliases(Dictionary<string, string> mergedVars)
    {
        if (mergedVars == null || mergedVars.Count == 0)
        {
            return;
        }

        var tokenKeys = new[]
        {
            "authToken",
            "auth_token",
            "accessToken",
            "access_token",
            "token",
            "jwt",
            "idToken",
            "id_token",
            "sessionToken",
            "session_token",
            "bearerToken",
            "bearer_token",
            "apiKey",
            "apikey",
        };
        foreach (var key in tokenKeys)
        {
            if (mergedVars.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && !value.Contains("{{", StringComparison.Ordinal))
            {
                foreach (var alias in GetTokenAliasNames(key))
                {
                    if (!mergedVars.ContainsKey(alias) || string.IsNullOrWhiteSpace(mergedVars[alias]))
                    {
                        mergedVars[alias] = value;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> GetTokenAliasNames(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            yield break;
        }

        var normalized = key.Trim();
        if (string.Equals(normalized, "apiKey", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "apikey", StringComparison.OrdinalIgnoreCase))
        {
            yield return "apiKey";
            yield return "apikey";
            yield return "X-API-Key";
            yield return "Api-Key";
            yield break;
        }

        yield return "authToken";
        yield return "auth_token";
        yield return "accessToken";
        yield return "access_token";
        yield return "token";
        yield return "jwt";
        yield return "idToken";
        yield return "id_token";
        yield return "sessionToken";
        yield return "session_token";
        yield return "bearerToken";
        yield return "bearer_token";
    }

    private static string NormalizeCredentialsByPolicy(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        var policy = ResolveCredentialRewritePolicy(testCase);
        var lockMap = ResolveLockedCredentialFields(testCase);
        var hasExplicitPolicy = policy != CredentialRewritePolicy.LegacyDefault;

        // Generic default mode for multi-project support:
        // never rewrite credential-like fields unless policy is explicit from tags.
        // This avoids auth-specific hardcoded behavior leaking into unrelated APIs.
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody))
        {
            return resolvedBody;
        }

        if (!hasExplicitPolicy)
        {
            return resolvedBody;
        }

        var allowEmailRewrite = policy switch
        {
            CredentialRewritePolicy.RewriteEmail => true,
            CredentialRewritePolicy.RewriteBoth => true,
            _ => false,
        };

        var allowPasswordRewrite = policy switch
        {
            CredentialRewritePolicy.RewritePassword => true,
            CredentialRewritePolicy.RewriteBoth => true,
            _ => false,
        };

        if (policy == CredentialRewritePolicy.Preserve)
        {
            return resolvedBody;
        }

        if (lockMap.LockEmail)
        {
            allowEmailRewrite = false;
        }

        if (lockMap.LockPassword)
        {
            allowPasswordRewrite = false;
        }

        if (!allowEmailRewrite && !allowPasswordRewrite)
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

        TryGetPreferredCredentialValue(variables, new[] { "registeredEmail", "requestEmail", "testEmail", "runUniqueEmail" }, out var preferredEmail);
        TryGetPreferredCredentialValue(variables, new[] { "registeredPassword", "requestPassword", "testPassword", "runUniquePassword" }, out var preferredPassword);

        if (string.IsNullOrWhiteSpace(preferredEmail) && string.IsNullOrWhiteSpace(preferredPassword))
        {
            return resolvedBody;
        }

        var changed = RewriteCredentialFields(root, preferredEmail, preferredPassword, allowEmailRewrite, allowPasswordRewrite);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string ResolveAuthModeFromTags(ExecutionTestCaseDto testCase)
    {
        var tags = ParseTags(testCase?.Tags);
        if (tags == null || tags.Count == 0)
        {
            return null;
        }

        foreach (var rawTag in tags)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                continue;
            }

            var tag = rawTag.Trim();
            if (!tag.StartsWith(AuthModeTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[AuthModeTagPrefix.Length..].Trim();
            var normalized = NormalizeAuthMode(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static bool RewriteCredentialFields(
        JsonNode node,
        string preferredEmail,
        string preferredPassword,
        bool allowEmailRewrite,
        bool allowPasswordRewrite)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var current))
                {
                    if (allowEmailRewrite
                        && property.Key.Equals("email", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(preferredEmail)
                        && ShouldRewriteSyntheticEmail(current)
                        && !string.Equals(current, preferredEmail, StringComparison.Ordinal))
                    {
                        obj[property.Key] = preferredEmail;
                        changed = true;
                        continue;
                    }

                    if (allowPasswordRewrite
                        && property.Key.Equals("password", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(preferredPassword)
                        && ShouldRewriteSyntheticPassword(current)
                        && !string.Equals(current, preferredPassword, StringComparison.Ordinal))
                    {
                        obj[property.Key] = preferredPassword;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null
                    && RewriteCredentialFields(property.Value, preferredEmail, preferredPassword, allowEmailRewrite, allowPasswordRewrite))
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
                if (item != null
                    && RewriteCredentialFields(item, preferredEmail, preferredPassword, allowEmailRewrite, allowPasswordRewrite))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static CredentialRewritePolicy ResolveCredentialRewritePolicy(ExecutionTestCaseDto testCase)
    {
        var tags = ParseTags(testCase?.Tags);
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(CredentialPolicyTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[CredentialPolicyTagPrefix.Length..].Trim().ToLowerInvariant();
            return value switch
            {
                "preserve" => CredentialRewritePolicy.Preserve,
                "rewrite_email" => CredentialRewritePolicy.RewriteEmail,
                "rewrite_password" => CredentialRewritePolicy.RewritePassword,
                "rewrite_both" => CredentialRewritePolicy.RewriteBoth,
                "bind_dependency" => CredentialRewritePolicy.BindDependencyCredentials,
                _ => CredentialRewritePolicy.LegacyDefault,
            };
        }

        // Metadata-first behavior: when flow metadata exists but no explicit credential
        // policy is provided, preserve request body exactly as produced by n8n.
        if (HasFlowMetadata(testCase))
        {
            return CredentialRewritePolicy.Preserve;
        }

        return CredentialRewritePolicy.LegacyDefault;
    }

    private static CredentialFieldLocks ResolveLockedCredentialFields(ExecutionTestCaseDto testCase)
    {
        var result = new CredentialFieldLocks();
        var tags = ParseTags(testCase?.Tags);
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(CredentialLockTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[CredentialLockTagPrefix.Length..].Trim().ToLowerInvariant();
            if (value == "request.body.email")
            {
                result.LockEmail = true;
            }
            else if (value == "request.body.password")
            {
                result.LockPassword = true;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseTags(string serializedTags)
    {
        if (string.IsNullOrWhiteSpace(serializedTags))
        {
            return Array.Empty<string>();
        }

        try
        {
            var tags = JsonSerializer.Deserialize<string[]>(serializedTags, JsonOptions);
            return tags ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private enum CredentialRewritePolicy
    {
        LegacyDefault = 0,
        Preserve = 1,
        RewriteEmail = 2,
        RewritePassword = 3,
        RewriteBoth = 4,
        BindDependencyCredentials = 5,
    }

    private sealed class CredentialFieldLocks
    {
        public bool LockEmail { get; set; }

        public bool LockPassword { get; set; }
    }

    private static bool TryGetPreferredCredentialValue(
        IReadOnlyDictionary<string, string> variables,
        IEnumerable<string> candidateKeys,
        out string preferredValue)
    {
        preferredValue = null;
        if (variables == null || candidateKeys == null)
        {
            return false;
        }

        foreach (var key in candidateKeys)
        {
            if (variables.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value)
                && !ContainsUnresolvedPlaceholder(value))
            {
                preferredValue = value;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeIdentifierLiteralsInJsonBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        // Run for HappyPath test cases (AllowsIdentifierLiteralReplacement) OR for any
        // test case that has declared dependencies — even Boundary/Negative LLM tests
        // need literal IDs replaced with the runtime IDs from upstream POST responses.
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || variables == null
            || (!AllowsIdentifierLiteralReplacement(testCase) && !HasDependencies(testCase))
            || IsIdentifierErrorIntentScenario(testCase))
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

        var changed = ReplaceIdentifierLiterals(root, testCase, variables);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string NormalizeDependencyScopedCredentialsByPolicy(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        var policy = ResolveCredentialRewritePolicy(testCase);
        var enforceByFlow = ShouldEnforceDependencyCredentialBindingByFlow(testCase);
        var fallbackLoginCredentialBinding =
            ShouldFallbackBindLoginCredentialsFromGlobalRegister(testCase);
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || variables == null
            || (!HasDependencies(testCase) && !fallbackLoginCredentialBinding)
            || (policy != CredentialRewritePolicy.BindDependencyCredentials
                && !enforceByFlow
                && !fallbackLoginCredentialBinding)
            || !LooksLikeJsonBody(testCase?.Request?.BodyType, resolvedBody))
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

        if (root is not JsonObject obj)
        {
            return resolvedBody;
        }

        var changed = false;

        if (TryResolveVariableValueFromDependencies(
                testCase,
                new[] { "registeredEmail", "email", "requestEmail", "testEmail" },
                variables,
                allowGlobalFallback: !HasDependencies(testCase),
                out var dependencyEmail)
            && obj.TryGetPropertyValue("email", out var emailNode)
            && emailNode is JsonValue
            && ShouldReplaceLoginCredentialValue(
                emailNode,
                forceRewriteLiteral: enforceByFlow || fallbackLoginCredentialBinding)
            && !string.IsNullOrWhiteSpace(dependencyEmail))
        {
            obj["email"] = dependencyEmail;
            changed = true;
        }

        if (TryResolveVariableValueFromDependencies(
                testCase,
                new[] { "registeredPassword", "password", "requestPassword", "testPassword" },
                variables,
                allowGlobalFallback: !HasDependencies(testCase),
                out var dependencyPassword)
            && obj.TryGetPropertyValue("password", out var passwordNode)
            && passwordNode is JsonValue
            && ShouldReplaceLoginCredentialValue(
                passwordNode,
                forceRewriteLiteral: enforceByFlow || fallbackLoginCredentialBinding)
            && !string.IsNullOrWhiteSpace(dependencyPassword))
        {
            obj["password"] = dependencyPassword;
            changed = true;
        }

        return changed ? obj.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static bool ShouldReplaceLoginCredentialValue(JsonNode credentialNode, bool forceRewriteLiteral = false)
    {
        if (credentialNode is not JsonValue value)
        {
            return false;
        }

        if (!value.TryGetValue<string>(out var currentValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return true;
        }

        if (forceRewriteLiteral)
        {
            // Flow-enforced happy login should always use credentials from dependency chain.
            return true;
        }

        // Only replace unresolved placeholders. Keep literal values from n8n
        // (e.g. wrongPassword in Negative cases) unchanged.
        return currentValue.Contains("{{", StringComparison.Ordinal)
            && currentValue.Contains("}}", StringComparison.Ordinal);
    }

    private static string NormalizeDependencyScopedBodyBindingsForFlow(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || variables == null
            || !HasDependencies(testCase)
            || !LooksLikeJsonBody(testCase?.Request?.BodyType, resolvedBody)
            || !string.Equals(testCase?.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || IsNoAuthExpectationScenario(testCase))
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

        var changed = RewriteDependencyScopedFields(root, testCase, variables);
        return changed ? root.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string NormalizeDependencyScopedConsumesBindings(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || variables == null
            || !HasDependencies(testCase)
            || !LooksLikeJsonBody(testCase?.Request?.BodyType, resolvedBody)
            || IsIdentifierErrorIntentScenario(testCase)
            || IsNoAuthExpectationScenario(testCase)
            || !IsLikelySuccessExpectation(testCase?.Expectation?.ExpectedStatus))
        {
            return resolvedBody;
        }

        var consumes = GetTagValues(testCase?.Tags, FlowConsumesTagPrefix)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (consumes.Count == 0)
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

        if (root is not JsonObject obj)
        {
            return resolvedBody;
        }

        var changed = false;
        foreach (var consumeKey in consumes)
        {
            var propertyName = ExtractBodyFieldFromConsumeKey(consumeKey);
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                continue;
            }

            if (!obj.TryGetPropertyValue(propertyName, out var existingNode) || existingNode is not JsonValue)
            {
                continue;
            }

            if (!TryResolveVariableValueFromDependencies(
                    testCase,
                    new[] { propertyName, consumeKey },
                    variables,
                    allowGlobalFallback: false,
                    out var dependencyValue)
                || string.IsNullOrWhiteSpace(dependencyValue))
            {
                continue;
            }

            var currentValue = existingNode.ToJsonString(JsonOptions).Trim('"');
            if (string.Equals(currentValue, dependencyValue, StringComparison.Ordinal))
            {
                continue;
            }

            obj[propertyName] = dependencyValue;
            changed = true;
        }

        return changed ? obj.ToJsonString(JsonOptions) : resolvedBody;
    }

    private static string ExtractBodyFieldFromConsumeKey(string consumeKey)
    {
        if (string.IsNullOrWhiteSpace(consumeKey))
        {
            return null;
        }

        var key = consumeKey.Trim();
        const string bodyPrefix = "request.body.";
        if (key.StartsWith(bodyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key[bodyPrefix.Length..];
        }

        // Only plain field-like consume keys are considered body properties.
        return key.Contains('.', StringComparison.Ordinal) ? null : key;
    }

    private static bool IsLikelySuccessExpectation(string expectedStatus)
    {
        if (string.IsNullOrWhiteSpace(expectedStatus))
        {
            return false;
        }

        try
        {
            var statuses = JsonSerializer.Deserialize<List<int>>(expectedStatus, JsonOptions);
            return statuses?.Any(code => code >= 200 && code < 300) == true;
        }
        catch
        {
            var trimmed = expectedStatus.Trim().Trim('[', ']');
            return int.TryParse(trimmed, out var single) && single >= 200 && single < 300;
        }
    }

    private static bool RewriteDependencyScopedFields(
        JsonNode node,
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var currentValue))
                {
                    if (ShouldRewriteSyntheticFieldValue(property.Key, currentValue)
                        && TryResolveVariableValueFromDependencies(
                            testCase,
                            BuildGenericDependencyFieldCandidates(property.Key),
                            variables,
                            allowGlobalFallback: false,
                            out var dependencyValue)
                        && !string.IsNullOrWhiteSpace(dependencyValue)
                        && !string.Equals(currentValue, dependencyValue, StringComparison.Ordinal))
                    {
                        obj[property.Key] = dependencyValue;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null && RewriteDependencyScopedFields(property.Value, testCase, variables))
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
                if (item != null && RewriteDependencyScopedFields(item, testCase, variables))
                {
                    changed = true;
                }
            }

            return changed;
        }

        return false;
    }

    private static IEnumerable<string> BuildGenericDependencyFieldCandidates(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return Array.Empty<string>();
        }

        var pascal = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        return new[]
        {
            propertyName,
            $"request{pascal}",
            $"registered{pascal}",
            $"test{pascal}",
        }.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldRewriteSyntheticFieldValue(string propertyName, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return true;
        }

        if (currentValue.Contains("{{", StringComparison.Ordinal) && currentValue.Contains("}}", StringComparison.Ordinal))
        {
            return true;
        }

        if (propertyName.Equals("email", StringComparison.OrdinalIgnoreCase))
        {
            return currentValue.Contains("@example.com", StringComparison.OrdinalIgnoreCase)
                || currentValue.Contains("@test.com", StringComparison.OrdinalIgnoreCase);
        }

        if (propertyName.Equals("password", StringComparison.OrdinalIgnoreCase))
        {
            return currentValue.StartsWith("password", StringComparison.OrdinalIgnoreCase)
                || currentValue.StartsWith("secure", StringComparison.OrdinalIgnoreCase)
                || currentValue.StartsWith("test", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool ShouldEnforceDependencyCredentialBindingByFlow(ExecutionTestCaseDto testCase)
    {
        if (testCase == null
            || !HasDependencies(testCase)
            || !string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsNoAuthExpectationScenario(testCase))
        {
            return false;
        }

        // Strict metadata path: only enforce binding when flow explicitly marks that
        // this scenario consumes credential values from previous steps.
        var consumes = GetTagValues(testCase?.Tags, FlowConsumesTagPrefix);
        var consumesCredential = consumes.Any(v =>
            string.Equals(v, "email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "password", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "request.body.email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "request.body.password", StringComparison.OrdinalIgnoreCase));
        if (consumesCredential)
        {
            return true;
        }

        return false;
    }

    private static bool ShouldFallbackBindLoginCredentialsFromGlobalRegister(ExecutionTestCaseDto testCase)
    {
        if (testCase == null
            || HasDependencies(testCase)
            || !string.Equals(testCase.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || IsNoAuthExpectationScenario(testCase))
        {
            return false;
        }

        return IsLikelyLoginRequest(testCase);
    }

    private static bool IsLikelyLoginRequest(ExecutionTestCaseDto testCase)
    {
        var url = testCase?.Request?.Url ?? string.Empty;
        var name = testCase?.Name ?? string.Empty;
        var description = testCase?.Description ?? string.Empty;
        var combined = $"{url} {name} {description}";

        if (combined.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0
            || combined.IndexOf("sign-in", StringComparison.OrdinalIgnoreCase) >= 0
            || combined.IndexOf("signin", StringComparison.OrdinalIgnoreCase) >= 0
            || combined.IndexOf("/auth", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static bool ReplaceIdentifierLiterals(
        JsonNode node,
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables)
    {
        if (node is JsonObject obj)
        {
            var changed = false;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue)
                {
                    var currentValue = property.Value?.ToJsonString(JsonOptions)?.Trim('"');
                    if (ShouldPreserveExplicitIdentifierLiteral(testCase, currentValue))
                    {
                        continue;
                    }
                    if (IsIdentifierField(property.Key) &&
                        (ShouldReplaceIdentifierLiteral(currentValue)
                            || IsLikelyObjectIdLiteralPlaceholder(currentValue)) &&
                        TryResolveVariableValueFromDependencies(testCase, BuildBodyIdentifierCandidates(property.Key), variables, allowGlobalFallback: !HasDependencies(testCase), out var replacement))
                    {
                        obj[property.Key] = replacement;
                        changed = true;
                        continue;
                    }
                }

                if (property.Value != null && ReplaceIdentifierLiterals(property.Value, testCase, variables))
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
                if (item != null && ReplaceIdentifierLiterals(item, testCase, variables))
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
            
            // For semantic identifier fields (e.g. categoryId, userId, productId),
            // DO NOT fall back to generic id/resourceId because that can cross-wire
            // dependencies (e.g. product id accidentally used as category id).
            // Only plain "id" should use generic fallback candidates.
            if (string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add("resourceId");
                candidates.Add("id");
            }
        }
        else
        {
            candidates.Add("resourceId");
            candidates.Add("id");
        }

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

        // Semantic fallback for multi-project naming differences:
        // productId <-> product_id <-> productUUID <-> pid, etc.
        if (candidates != null && variables != null && variables.Count > 0)
        {
            foreach (var candidate in candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (TryResolveSemanticIdentifierAlias(candidate, variables, out resolved))
                {
                    return true;
                }
            }
        }

        resolved = null;
        return false;
    }

    private static bool TryResolveVariableValueFromDependencies(
        ExecutionTestCaseDto testCase,
        IEnumerable<string> candidates,
        IReadOnlyDictionary<string, string> variables,
        bool allowGlobalFallback,
        out string resolved)
    {
        // Flow-safe default: when case has explicit dependency chaining metadata,
        // never fall back to global variable bag for consumed values.
        // This prevents cross-flow value leaks (e.g. userEmail resolving to admin email).
        if (ShouldEnforceDependencyScopedResolution(testCase))
        {
            allowGlobalFallback = false;
        }

        if (testCase?.DependencyIds != null
            && testCase.DependencyIds.Count > 0
            && candidates != null
            && variables != null
            && variables.Count > 0)
        {
            var candidateList = candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = testCase.DependencyIds.Count - 1; i >= 0; i--)
            {
                var dependencyId = testCase.DependencyIds[i];
                foreach (var candidate in candidateList)
                {
                    var scopedKey = $"case.{dependencyId:N}.{candidate}";
                    if (!variables.TryGetValue(scopedKey, out var value)
                        || string.IsNullOrWhiteSpace(value)
                        || value.Contains("{{", StringComparison.Ordinal)
                        || ShouldReplaceIdentifierLiteral(value))
                    {
                        continue;
                    }

                    resolved = value;
                    return true;
                }

                foreach (var candidate in candidateList)
                {
                    if (TryResolveSemanticScopedIdentifierAlias(dependencyId, candidate, variables, out resolved))
                    {
                        return true;
                    }
                }
            }
        }

        if (!allowGlobalFallback)
        {
            resolved = null;
            return false;
        }

        return TryResolveVariableValue(candidates, variables, out resolved);
    }

    private static bool ShouldEnforceDependencyScopedResolution(ExecutionTestCaseDto testCase)
    {
        if (!HasDependencies(testCase))
        {
            return false;
        }

        if (HasFlowMetadata(testCase))
        {
            return true;
        }

        return true;
    }

    private static bool TryResolveSemanticScopedIdentifierAlias(
        Guid dependencyId,
        string candidate,
        IReadOnlyDictionary<string, string> variables,
        out string resolved)
    {
        var scopePrefix = $"case.{dependencyId:N}.";
        foreach (var kvp in variables)
        {
            if (!kvp.Key.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var leafName = kvp.Key[scopePrefix.Length..];
            if (!IsSemanticIdentifierAlias(candidate, leafName))
            {
                continue;
            }

            var value = kvp.Value;
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains("{{", StringComparison.Ordinal)
                || ShouldReplaceIdentifierLiteral(value))
            {
                continue;
            }

            resolved = value;
            return true;
        }

        resolved = null;
        return false;
    }

    private static bool TryResolveSemanticIdentifierAlias(
        string candidate,
        IReadOnlyDictionary<string, string> variables,
        out string resolved)
    {
        foreach (var kvp in variables)
        {
            if (!IsSemanticIdentifierAlias(candidate, kvp.Key))
            {
                continue;
            }

            var value = kvp.Value;
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains("{{", StringComparison.Ordinal)
                || ShouldReplaceIdentifierLiteral(value))
            {
                continue;
            }

            resolved = value;
            return true;
        }

        resolved = null;
        return false;
    }

    private static bool IsSemanticIdentifierAlias(string candidate, string actualKey)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(actualKey))
        {
            return false;
        }

        var a = NormalizeIdentifierKey(candidate);
        var b = NormalizeIdentifierKey(actualKey);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // treat generic suffixes as equivalent for resource IDs.
        a = StripIdentifierSuffixToken(a);
        b = StripIdentifierSuffixToken(b);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIdentifierKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var raw = value.Trim();
        // remove scoped prefixes like "case.<id>."
        var lastDot = raw.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < raw.Length - 1)
        {
            raw = raw[(lastDot + 1)..];
        }

        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    private static string StripIdentifierSuffixToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value;
        foreach (var suffix in new[] { "identifier", "identity", "uuid", "guid", "code", "key", "id" })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && normalized.Length > suffix.Length)
            {
                normalized = normalized[..^suffix.Length];
                break;
            }
        }

        // common short aliases for id-only keys
        if (string.Equals(normalized, "p", StringComparison.OrdinalIgnoreCase))
        {
            return "product";
        }

        return normalized;
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
        // Run for HappyPath OR any test case with dependencies (PUT/PATCH/DELETE that need
        // to use the real resource ID from an upstream POST in the same run).
        var eligible = string.Equals(testCase?.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || HasDependencies(testCase);
        if (string.IsNullOrWhiteSpace(resolvedUrl)
            || !eligible
            || resolvedUrl.Contains("{", StringComparison.Ordinal))
        {
            return resolvedUrl;
        }

        var replacementValue = SelectPreferredResolvedPathParamValue(resolvedPathParams);
        if (string.IsNullOrWhiteSpace(replacementValue)
            && TryResolveVariableValueFromDependencies(testCase, BuildRouteIdentifierVariableCandidates(resolvedUrl), variables, allowGlobalFallback: !HasDependencies(testCase), out var fallbackVariableValue))
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

    private static bool AllowsIdentifierLiteralReplacement(ExecutionTestCaseDto testCase)
    {
        if (!IsRewritePolicyEnabled(testCase))
        {
            return false;
        }

        // HappyPath test cases always need literal ID replacement.
        // Dependency-based replacement is allowed only for non-error scenarios.
        return string.Equals(testCase?.TestType, "HappyPath", StringComparison.OrdinalIgnoreCase)
            || (HasDependencies(testCase) && !IsLikelyErrorCase(testCase));
    }

    private static bool IsRewritePolicyEnabled(ExecutionTestCaseDto testCase)
    {
        var tags = ParseTags(testCase?.Tags);
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(RewritePolicyTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[RewritePolicyTagPrefix.Length..].Trim().ToLowerInvariant();
            if (value is "safe" or "minimal")
            {
                return true;
            }

            return false;
        }

        // Default: disable heuristic rewriting to preserve n8n intent exactly.
        return false;
    }

    private static bool IsAuthFallbackAllowed(ExecutionTestCaseDto testCase)
    {
        var tags = ParseTags(testCase?.Tags);
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(AuthFallbackTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[AuthFallbackTagPrefix.Length..].Trim().ToLowerInvariant();
            return value is "allow" or "true" or "on";
        }

        // Default strict behavior: do not auto-inject auth fallback.
        return false;
    }

    private static bool ShouldPreserveExplicitIdentifierLiteral(ExecutionTestCaseDto testCase, string currentValue)
    {
        // In Negative/Boundary/Invalid scenarios, an explicit literal (for example "invalid")
        // is often intentional to assert 4xx validation behavior and must not be rewritten.
        if (!IsLikelyErrorCase(testCase) || string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        var trimmed = currentValue.Trim();
        return !trimmed.Contains("{{", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true when the test case was materialized from an LLM suggestion.
    /// Primary detection: "llm-suggested" tag (set by LlmSuggestionMaterializer).
    /// Backward compat: older materialized test cases have "auto-generated" but NOT
    /// "rule-based" (rule-based mutations always carry "rule-based") and NOT "happy-path"
    /// (HappyPath generated tests always carry "happy-path" and need their own normalization).
    /// LLM-sourced test cases execute with the EXACT body n8n provided — no normalization.
    /// </summary>
    private static bool IsLlmSourced(ExecutionTestCaseDto testCase)
    {
        var tags = testCase?.Tags;
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        // Primary: explicit marker added by LlmSuggestionMaterializer.EnsureLlmSourcedTag
        if (tags.Contains("llm-suggested", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Backward compat 1: test cases materialized from LLM suggestions before the
        // EnsureLlmSourcedTag fix often had "auto-generated" but no explicit "llm-suggested".
        if (tags.Contains("auto-generated", StringComparison.OrdinalIgnoreCase)
            && !tags.Contains("rule-based", StringComparison.OrdinalIgnoreCase)
            && !tags.Contains("happy-path", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Backward compat 2: some legacy Boundary/Negative LLM rows only preserved raw
        // n8n tags (e.g. ["boundary","happy-path"]) and missed system tags.
        // If it's Boundary/Negative and clearly not rule-based, treat as LLM-sourced.
        if (!tags.Contains("rule-based", StringComparison.OrdinalIgnoreCase)
            && (tags.Contains("boundary", StringComparison.OrdinalIgnoreCase)
                || tags.Contains("negative", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsLoginLikeRequest(ExecutionTestCaseDto testCase)
    {
        if (!HasFlowMetadata(testCase))
        {
            return false;
        }

        var consumes = GetTagValues(testCase?.Tags, FlowConsumesTagPrefix);
        var dependsOn = GetTagValues(testCase?.Tags, FlowDependsOnTagPrefix);
        return dependsOn.Count > 0
            && consumes.Any(v =>
                string.Equals(v, "email", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "password", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "request.body.email", StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, "request.body.password", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRegisterLikeRequest(ExecutionTestCaseDto testCase)
    {
        if (!HasFlowMetadata(testCase))
        {
            return false;
        }

        var produces = GetTagValues(testCase?.Tags, FlowProducesTagPrefix);
        var hasCredentialProduces = produces.Any(v =>
            string.Equals(v, "email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "password", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "request.body.email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "request.body.password", StringComparison.OrdinalIgnoreCase));
        var hasDependencies = GetTagValues(testCase?.Tags, FlowDependsOnTagPrefix).Count > 0;
        return hasCredentialProduces && !hasDependencies;
    }

    /// <summary>
    /// Returns true when the test case is intentionally testing duplicate/existing email
    /// registration (expects 409 Conflict). These tests must NOT have their email uniquified
    /// so they can trigger the duplicate-email constraint on the server.
    /// </summary>
    private static bool IsDuplicateEmailTestCase(ExecutionTestCaseDto testCase)
    {
        if (!HasFlowMetadata(testCase))
        {
            return false;
        }

        var consumes = GetTagValues(testCase?.Tags, FlowConsumesTagPrefix);
        var consumesEmail = consumes.Any(v =>
            string.Equals(v, "email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "request.body.email", StringComparison.OrdinalIgnoreCase));
        if (!consumesEmail)
        {
            return false;
        }

        var expectedStatus = testCase?.Expectation?.ExpectedStatus;
        return !string.IsNullOrWhiteSpace(expectedStatus)
            && expectedStatus.Contains("409", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true when the test case depends on at least one prior test case.
    /// Used to enable identifier literal replacement (e.g. categoryId: 1 → real ID)
    /// even for LLM-sourced and non-HappyPath test cases.
    /// </summary>
    private static bool HasDependencies(ExecutionTestCaseDto testCase)
        => testCase?.DependencyIds?.Count > 0;

    private static bool IsNoAuthExpectationScenario(ExecutionTestCaseDto testCase)
    {
        if (testCase == null)
        {
            return false;
        }

        var expectedStatus = testCase.Expectation?.ExpectedStatus ?? string.Empty;
        if (expectedStatus.Contains("401", StringComparison.Ordinal)
            || expectedStatus.Contains("403", StringComparison.Ordinal))
        {
            return true;
        }

        // Fallback inference: some generated negative cases describe no-auth intent
        // in name/description/tags but may not carry a clean 401/403 expectation.
        var name = testCase.Name ?? string.Empty;
        var description = testCase.Description ?? string.Empty;
        var tags = string.Join(' ', ParseTags(testCase.Tags) ?? Array.Empty<string>());
        var surface = $"{name} {description} {tags}";

        return ContainsAny(surface,
            "no token",
            "without token",
            "missing token",
            "without authorization",
            "no authorization",
            "missing authorization",
            "unauthorized",
            "unauthenticated",
            "no auth",
            "no-auth",
            "no_auth");
    }

    private static bool IsIdentifierErrorIntentScenario(ExecutionTestCaseDto testCase)
    {
        if (testCase == null)
        {
            return false;
        }

        var expectedStatus = testCase.Expectation?.ExpectedStatus ?? string.Empty;
        if (expectedStatus.Contains("404", StringComparison.Ordinal))
        {
            return true;
        }

        var surface = $"{testCase.Name ?? string.Empty} {testCase.Description ?? string.Empty}";
        return ContainsAny(surface,
            "not found",
            "non-existent",
            "non existent",
            "does not exist",
            "doesn't exist",
            "missing id",
            "id not found",
            "invalid id",
            "invalid identifier",
            "invalid format",
            "invalid objectid",
            "invalid object id",
            "malformed id",
            "wrong id format",
            "category not found",
            "product not found",
            "user not found");
    }

    private static string NormalizeHappyPathSyntheticBody(
        ExecutionTestCaseDto testCase,
        string resolvedBody,
        IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(resolvedBody)
            || testCase?.Request == null
            || !string.Equals(testCase.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || !LooksLikeJsonBody(testCase.Request.BodyType, resolvedBody)
            || IsLoginLikeRequest(testCase))
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

        var hasPreferredEmail = TryGetPreferredEmailForSyntheticBody(testCase, variables, out var preferredEmail);

        // Use tcUniqueId (per-execution random) as suffix so that HappyPath POST test cases
        // produce a unique name/code/slug and don't collide on unique-constraint fields.
        var runSuffix = GetRunSuffix(variables);

        var isHappyPath = string.Equals(testCase.TestType?.Trim(), "HappyPath", StringComparison.OrdinalIgnoreCase);

        // Never rewrite email outside HappyPath. Boundary/Negative tests carry intentional
        // values from LLM (including placeholders) and must execute exactly as defined.
        var shouldRewriteEmail = hasPreferredEmail && isHappyPath;
        var emailRewritten = shouldRewriteEmail && ReplaceSyntheticEmails(root, preferredEmail);

        // Only uniquify name/code/slug for HappyPath POST tests.
        var nameRewritten = isHappyPath
            && !string.IsNullOrWhiteSpace(runSuffix)
            && ReplaceSyntheticResourceNames(root, runSuffix);

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

    private static bool TryGetPreferredEmailForSyntheticBody(
        ExecutionTestCaseDto testCase,
        IReadOnlyDictionary<string, string> variables,
        out string preferredEmail)
    {
        preferredEmail = null;

        if (IsRegisterLikeRequest(testCase))
        {
            // Derive a per-execution unique email suffix so multiple register test cases
            // in the same run (HappyPath + Boundary + Negative) don't collide (409).
            // Prefer tcUniqueId (changes each execution) over TestCaseId (stable).
            var tcSuffix = variables != null
                && variables.TryGetValue("tcUniqueId", out var tcId)
                && !string.IsNullOrWhiteSpace(tcId)
                    ? tcId
                    : testCase.TestCaseId.ToString("N")[..8].ToLowerInvariant();

            if (variables != null
                && variables.TryGetValue("runUniqueEmail", out var runEmail)
                && !string.IsNullOrWhiteSpace(runEmail))
            {
                var atIdx = runEmail.IndexOf('@');
                preferredEmail = atIdx > 0
                    ? $"{runEmail[..atIdx]}_{tcSuffix}{runEmail[atIdx..]}"
                    : $"{runEmail}_{tcSuffix}";
                return true;
            }

            // No runUniqueEmail configured: synthesize a unique email from tcUniqueId.
            preferredEmail = $"testuser_{tcSuffix}@example.test";
            return true;
        }

        return TryGetPreferredTestEmail(variables, out preferredEmail);
    }

    private static bool HasFlowMetadata(ExecutionTestCaseDto testCase)
    {
        var tags = ParseTags(testCase?.Tags);
        return tags.Any(tag =>
            tag.StartsWith(FlowRequiredTagPrefix, StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith(FlowDependsOnTagPrefix, StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith(FlowProducesTagPrefix, StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith(FlowConsumesTagPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetTagValues(string serializedTags, string prefix)
    {
        var tags = ParseTags(serializedTags);
        var values = new List<string>();
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag) || !tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[prefix.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
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

        // Already uniquified via {{tcUniqueId}} resolution (ends with _xxxxxxxx) — do not rewrite.
        if (AlreadyUniquifiedRegex.IsMatch(localPart))
        {
            return false;
        }

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

    private static bool ShouldRewriteSyntheticPassword(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && !value.Contains("{{", StringComparison.Ordinal);
    }

    private static string GetRunSuffix(IReadOnlyDictionary<string, string> variables)
    {
        if (variables == null)
        {
            return null;
        }

        // Prefer tcUniqueId (per-execution random) so multiple POST test cases of the
        // same resource type within a single run each get a distinct suffix.
        // Fall back to run-level keys for environments where tcUniqueId is not injected.
        var candidateKeys = new[] { "tcUniqueId", "runSuffix", "runIdSuffix", "runId" };
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
            if (TryBuildUrlWithBaseOverride(baseUrl, resolvedUrl, out var combinedUrl))
            {
                return combinedUrl;
            }

            var trimmedBase = baseUrl.TrimEnd('/');
            var trimmedPath = resolvedUrl.TrimStart('/');
            return $"{trimmedBase}/{trimmedPath}";
        }

        return resolvedUrl;
    }

    private static bool TryBuildUrlWithBaseOverride(string baseUrl, string resolvedUrl, out string combinedUrl)
    {
        combinedUrl = null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(resolvedUrl) || resolvedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!resolvedUrl.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        if (!basePath.EndsWith("/api-docs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootBuilder = new UriBuilder(baseUri)
        {
            Path = basePath[..^"/api-docs".Length],
        };

        combinedUrl = rootBuilder.Uri.AbsoluteUri.TrimEnd('/') + resolvedUrl;
        return true;
    }

    private static string NormalizeSwaggerDocsApiUrl(string resolvedUrl)
    {
        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            return resolvedUrl;
        }

        var normalized = ExtractPathOnly(resolvedUrl).Trim();
        if (!normalized.StartsWith("/api-docs/", StringComparison.OrdinalIgnoreCase))
        {
            return resolvedUrl;
        }

        var apiIndex = normalized.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (apiIndex < 0)
        {
            return resolvedUrl;
        }

        return normalized[apiIndex..];
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

