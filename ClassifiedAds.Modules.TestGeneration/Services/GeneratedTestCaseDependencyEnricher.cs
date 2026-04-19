using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Enriches generated test cases with missing dependency links and route-param placeholders
/// so downstream execution can resolve identifiers produced by earlier happy-path cases.
/// </summary>
public static class GeneratedTestCaseDependencyEnricher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly Regex RouteTokenRegex = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(@"^\{\{(?<name>[A-Za-z0-9_]+)\}\}$", RegexOptions.Compiled);

    private static readonly string[] IdentifierJsonPaths =
    {
        "$.data.id",
        "$.data._id",
        "$.id",
        "$._id",
        "$.result.id",
        "$.result._id",
        "$.data[0].id",
        "$.data[0]._id",
        "$[0].id",
        "$[0]._id",
    };

    private static readonly string[] AuthTokenJsonPaths =
    {
        "$.data.token",
        "$.token",
        "$.data.accessToken",
        "$.accessToken",
        "$.data.jwt",
        "$.jwt",
    };

    public static GeneratedTestCaseEnrichmentResult Enrich(
        IReadOnlyList<TestCase> generatedTestCases,
        IReadOnlyList<ApiOrderItemModel> approvedOrder,
        IReadOnlyList<TestCase> existingProducerCases = null,
        IReadOnlyList<TestCaseVariable> existingProducerVariables = null)
    {
        if (generatedTestCases == null || generatedTestCases.Count == 0 || approvedOrder == null || approvedOrder.Count == 0)
        {
            return GeneratedTestCaseEnrichmentResult.Empty;
        }

        var orderItemMap = approvedOrder
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.First());

        var existingVariableMap = (existingProducerVariables ?? Array.Empty<TestCaseVariable>())
            .GroupBy(x => x.TestCaseId)
            .ToDictionary(g => g.Key, g => (IList<TestCaseVariable>)g.ToList());

        var producerCandidates = BuildProducerCandidates(
            generatedTestCases,
            existingProducerCases ?? Array.Empty<TestCase>(),
            orderItemMap,
            existingVariableMap);

        if (producerCandidates.Count == 0)
        {
            return GeneratedTestCaseEnrichmentResult.Empty;
        }

        var producersByEndpoint = producerCandidates
            .GroupBy(x => x.EndpointId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.TestType == TestType.HappyPath)
                    .ThenByDescending(x => GetMethodPriority(x.OrderItem?.HttpMethod))
                    .ThenBy(x => x.OrderIndex)
                    .First());

        var pendingExistingVariables = new List<TestCaseVariable>();

        foreach (var testCase in generatedTestCases)
        {
            if (!testCase.EndpointId.HasValue || !orderItemMap.TryGetValue(testCase.EndpointId.Value, out var orderItem))
            {
                continue;
            }

            AddDeclaredDependencies(testCase, orderItem, producersByEndpoint);
            FillMissingRouteParams(
                testCase,
                orderItem,
                producerCandidates,
                producersByEndpoint,
                pendingExistingVariables);

            // Skip body enrichment for HTTP methods that don't accept request bodies.
            // This prevents false dependency links from being created for DELETE/GET/HEAD/OPTIONS endpoints.
            var httpMethod = (orderItem.HttpMethod ?? string.Empty).Trim().ToUpperInvariant();
            if (httpMethod is not ("DELETE" or "GET" or "HEAD" or "OPTIONS"))
            {
                FillMissingBodyBindings(
                    testCase,
                    orderItem,
                    producerCandidates,
                    producersByEndpoint,
                    pendingExistingVariables);
            }

            FillMissingAuthBindings(
                testCase,
                orderItem,
                producerCandidates,
                producersByEndpoint,
                pendingExistingVariables);
        }

        return pendingExistingVariables.Count == 0
            ? GeneratedTestCaseEnrichmentResult.Empty
            : new GeneratedTestCaseEnrichmentResult(pendingExistingVariables);
    }

    private static List<ProducerCandidate> BuildProducerCandidates(
        IReadOnlyList<TestCase> generatedTestCases,
        IReadOnlyList<TestCase> existingProducerCases,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, IList<TestCaseVariable>> existingVariableMap)
    {
        var result = new List<ProducerCandidate>();

        foreach (var testCase in generatedTestCases.Where(x => x.EndpointId.HasValue && x.TestType == TestType.HappyPath))
        {
            if (!orderItemMap.TryGetValue(testCase.EndpointId!.Value, out var orderItem))
            {
                continue;
            }

            result.Add(new ProducerCandidate(
                testCase,
                testCase.EndpointId.Value,
                orderItem,
                testCase.OrderIndex,
                testCase.TestType,
                testCase.Variables,
                isExisting: false));
        }

        foreach (var testCase in existingProducerCases.Where(x => x.EndpointId.HasValue && x.TestType == TestType.HappyPath))
        {
            if (!orderItemMap.TryGetValue(testCase.EndpointId!.Value, out var orderItem))
            {
                continue;
            }

            existingVariableMap.TryGetValue(testCase.Id, out var variables);
            result.Add(new ProducerCandidate(
                testCase,
                testCase.EndpointId.Value,
                orderItem,
                testCase.OrderIndex,
                testCase.TestType,
                variables ?? new List<TestCaseVariable>(),
                isExisting: true));
        }

        return result
            .GroupBy(x => x.TestCaseId)
            .Select(g => g.First())
            .ToList();
    }

    private static void AddDeclaredDependencies(
        TestCase testCase,
        ApiOrderItemModel orderItem,
        IReadOnlyDictionary<Guid, ProducerCandidate> producersByEndpoint)
    {
        if (orderItem.DependsOnEndpointIds == null || orderItem.DependsOnEndpointIds.Count == 0)
        {
            return;
        }

        foreach (var dependencyEndpointId in orderItem.DependsOnEndpointIds)
        {
            if (!producersByEndpoint.TryGetValue(dependencyEndpointId, out var producer))
            {
                continue;
            }

            EnsureDependency(testCase, producer.TestCaseId);
        }
    }

    private static void FillMissingRouteParams(
        TestCase testCase,
        ApiOrderItemModel orderItem,
        IReadOnlyList<ProducerCandidate> producerCandidates,
        IReadOnlyDictionary<Guid, ProducerCandidate> producersByEndpoint,
        ICollection<TestCaseVariable> pendingExistingVariables)
    {
        if (testCase.Request == null)
        {
            return;
        }

        var routePathTemplate = orderItem.Path ?? testCase.Request.Url;
        var routeTokens = ExtractRouteTokens(routePathTemplate);
        if (routeTokens.Count == 0)
        {
            return;
        }

        var currentPathParams = DeserializeDictionary(testCase.Request.PathParams);
        var changed = NormalizeUrlTemplateAndPathParams(testCase.Request, routePathTemplate, currentPathParams);

        var dependencyCandidates = (orderItem.DependsOnEndpointIds ?? new List<Guid>())
            .Select(x => producersByEndpoint.TryGetValue(x, out var producer) ? producer : null)
            .Where(x => x != null && x.TestCaseId != testCase.Id)
            .ToList();

        foreach (var token in routeTokens)
        {
            if (currentPathParams.TryGetValue(token, out var existingValue) && !string.IsNullOrWhiteSpace(existingValue))
            {
                if (TryExtractPlaceholderVariable(existingValue, out var placeholderVariable))
                {
                    var existingProducer = SelectProducerForToken(
                        token,
                        routePathTemplate,
                        dependencyCandidates,
                        producerCandidates,
                        testCase.Id)
                        ?? SelectProducerForToken(
                            placeholderVariable,
                            routePathTemplate,
                            dependencyCandidates,
                            producerCandidates,
                            testCase.Id);

                    if (existingProducer != null)
                    {
                        var resolvedVariableName = EnsureProducerVariable(
                            token,
                            routePathTemplate,
                            existingProducer,
                            pendingExistingVariables,
                            preferredVariableName: placeholderVariable);

                        if (!string.IsNullOrWhiteSpace(resolvedVariableName) &&
                            !string.Equals(resolvedVariableName, placeholderVariable, StringComparison.OrdinalIgnoreCase))
                        {
                            currentPathParams[token] = $"{{{{{resolvedVariableName}}}}}";
                            changed = true;
                        }

                        EnsureDependency(testCase, existingProducer.TestCaseId);
                    }

                    continue;
                }

                // For HappyPath identifier tokens, replace hardcoded route values (e.g. 12345)
                // with dependency placeholders so update/delete cases target resources created earlier in the run.
                if (!ShouldReplaceRouteParamValue(testCase, token, existingValue))
                {
                    continue;
                }

                var producerForLiteral = SelectProducerForToken(
                    token,
                    routePathTemplate,
                    dependencyCandidates,
                    producerCandidates,
                    testCase.Id);

                if (producerForLiteral == null)
                {
                    continue;
                }

                var routeVariableName = EnsureProducerVariable(
                    token,
                    routePathTemplate,
                    producerForLiteral,
                    pendingExistingVariables);

                if (string.IsNullOrWhiteSpace(routeVariableName))
                {
                    continue;
                }

                currentPathParams[token] = $"{{{{{routeVariableName}}}}}";
                EnsureDependency(testCase, producerForLiteral.TestCaseId);
                changed = true;
                continue;
            }

            var producer = SelectProducerForToken(
                token,
                routePathTemplate,
                dependencyCandidates,
                producerCandidates,
                testCase.Id);

            if (producer == null)
            {
                continue;
            }

            var variableName = EnsureProducerVariable(token, routePathTemplate, producer, pendingExistingVariables);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            currentPathParams[token] = $"{{{{{variableName}}}}}";
            EnsureDependency(testCase, producer.TestCaseId);
            changed = true;
        }

        if (changed)
        {
            testCase.Request.PathParams = currentPathParams.Count == 0
                ? null
                : JsonSerializer.Serialize(currentPathParams, JsonOpts);
        }
    }

    private static bool NormalizeUrlTemplateAndPathParams(
        TestCaseRequest request,
        string routePathTemplate,
        IDictionary<string, string> currentPathParams)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(routePathTemplate) ||
            ExtractRouteTokens(routePathTemplate).Count == 0 ||
            string.IsNullOrWhiteSpace(request.Url))
        {
            return false;
        }

        var currentPath = ExtractPathOnly(request.Url);
        if (string.IsNullOrWhiteSpace(currentPath) ||
            string.Equals(currentPath, routePathTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryExtractRouteTokenValues(routePathTemplate, currentPath, out var inferredPathParams) ||
            inferredPathParams.Count == 0)
        {
            return false;
        }

        var changed = false;
        foreach (var kvp in inferredPathParams)
        {
            if (currentPathParams.TryGetValue(kvp.Key, out var existingValue) && !string.IsNullOrWhiteSpace(existingValue))
            {
                continue;
            }

            currentPathParams[kvp.Key] = kvp.Value;
            changed = true;
        }

        var normalizedUrl = ReplacePathKeepingSuffix(request.Url, routePathTemplate);
        if (!string.Equals(request.Url, normalizedUrl, StringComparison.Ordinal))
        {
            request.Url = normalizedUrl;
            changed = true;
        }

        return changed;
    }

    private static bool TryExtractRouteTokenValues(
        string routePathTemplate,
        string currentPath,
        out Dictionary<string, string> inferredPathParams)
    {
        inferredPathParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var templateSegments = routePathTemplate
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        var currentSegments = currentPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (templateSegments.Length != currentSegments.Length)
        {
            return false;
        }

        var matchedTokenCount = 0;

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            var currentSegment = Uri.UnescapeDataString(currentSegments[i]);

            if (templateSegment.StartsWith("{", StringComparison.Ordinal)
                && templateSegment.EndsWith("}", StringComparison.Ordinal)
                && templateSegment.Length > 2)
            {
                var token = templateSegment[1..^1];
                if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(currentSegment))
                {
                    inferredPathParams[token] = currentSegment;
                    matchedTokenCount++;
                }

                continue;
            }

            if (!string.Equals(templateSegment, currentSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                inferredPathParams.Clear();
                return false;
            }
        }

        return matchedTokenCount > 0;
    }

    private static string ExtractPathOnly(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            return absolute.AbsolutePath;
        }

        var splitIndex = url.IndexOfAny(new[] { '?', '#' });
        return splitIndex >= 0 ? url[..splitIndex] : url;
    }

    private static string ReplacePathKeepingSuffix(string url, string routePathTemplate)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return routePathTemplate;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            var builder = new UriBuilder(absolute)
            {
                Path = routePathTemplate,
            };

            return builder.Uri.AbsoluteUri;
        }

        var splitIndex = url.IndexOfAny(new[] { '?', '#' });
        if (splitIndex < 0)
        {
            return routePathTemplate;
        }

        return routePathTemplate + url[splitIndex..];
    }

    private static void FillMissingBodyBindings(
        TestCase testCase,
        ApiOrderItemModel orderItem,
        IReadOnlyList<ProducerCandidate> producerCandidates,
        IReadOnlyDictionary<Guid, ProducerCandidate> producersByEndpoint,
        ICollection<TestCaseVariable> pendingExistingVariables)
    {
        if (testCase.Request == null || string.IsNullOrWhiteSpace(testCase.Request.Body))
        {
            return;
        }

        JsonNode bodyNode;
        try
        {
            bodyNode = JsonNode.Parse(testCase.Request.Body);
        }
        catch
        {
            return;
        }

        if (bodyNode == null)
        {
            return;
        }

        var dependencyCandidates = (orderItem.DependsOnEndpointIds ?? new List<Guid>())
            .Select(x => producersByEndpoint.TryGetValue(x, out var producer) ? producer : null)
            .Where(x => x != null && x.TestCaseId != testCase.Id)
            .ToList();

        var changed = BindBodyNode(
            bodyNode,
            orderItem.Path ?? testCase.Request.Url,
            dependencyCandidates,
            producerCandidates,
            testCase,
            pendingExistingVariables,
            allowAuthBootstrapBindings: !IsRegisterLikeEndpoint(orderItem));

        if (changed)
        {
            testCase.Request.Body = bodyNode.ToJsonString(JsonOpts);
        }
    }

    private static void FillMissingAuthBindings(
        TestCase testCase,
        ApiOrderItemModel orderItem,
        IReadOnlyList<ProducerCandidate> producerCandidates,
        IReadOnlyDictionary<Guid, ProducerCandidate> producersByEndpoint,
        ICollection<TestCaseVariable> pendingExistingVariables)
    {
        if (testCase.Request == null)
        {
            return;
        }

        // Auth endpoints (register/login/token...) should stay bootstrap nodes.
        // Auto-injecting Authorization here can create register<->login cycles.
        if (IsAuthLikeEndpoint(orderItem))
        {
            return;
        }

        var dependencyCandidates = (orderItem.DependsOnEndpointIds ?? new List<Guid>())
            .Select(x => producersByEndpoint.TryGetValue(x, out var producer) ? producer : null)
            .Where(x => x != null && x.TestCaseId != testCase.Id)
            .ToList();

        var authProducer = SelectAuthProducer(dependencyCandidates, producerCandidates, testCase.Id, preferRegister: false);
        if (authProducer == null)
        {
            return;
        }

        var headers = DeserializeDictionary(testCase.Request.Headers);
        var changed = false;

        if (headers.TryGetValue("Authorization", out var existingAuthValue) &&
            !string.IsNullOrWhiteSpace(existingAuthValue) &&
            !TryExtractPlaceholderVariable(existingAuthValue, out _))
        {
            return;
        }

        var variableName = EnsureAuthTokenVariable(authProducer, pendingExistingVariables);
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        var desiredHeader = $"Bearer {{{{{variableName}}}}}";
        if (!headers.TryGetValue("Authorization", out var currentHeader) ||
            !string.Equals(currentHeader, desiredHeader, StringComparison.Ordinal))
        {
            headers["Authorization"] = desiredHeader;
            changed = true;
        }

        EnsureDependency(testCase, authProducer.TestCaseId);

        if (changed)
        {
            testCase.Request.Headers = headers.Count == 0
                ? null
                : JsonSerializer.Serialize(headers, JsonOpts);
        }
    }

    private static bool BindBodyNode(
        JsonNode node,
        string consumerPath,
        IReadOnlyList<ProducerCandidate> dependencyCandidates,
        IReadOnlyList<ProducerCandidate> allCandidates,
        TestCase testCase,
        ICollection<TestCaseVariable> pendingExistingVariables,
        bool allowAuthBootstrapBindings)
    {
        var changed = false;

        if (node is JsonObject obj)
        {
            var authProducer = allowAuthBootstrapBindings
                ? SelectAuthProducer(dependencyCandidates, allCandidates, testCase.Id, preferRegister: true)
                : null;
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue && authProducer != null)
                {
                    if (string.Equals(property.Key, "email", StringComparison.OrdinalIgnoreCase))
                    {
                        var variableName = EnsureRequestBodyVariable(
                            authProducer,
                            "registeredEmail",
                            "$.email",
                            pendingExistingVariables);
                        if (ShouldSetPlaceholderValue(property.Value, variableName))
                        {
                            obj[property.Key] = $"{{{{{variableName}}}}}";
                            EnsureDependency(testCase, authProducer.TestCaseId);
                            changed = true;
                            continue;
                        }
                    }

                    if (string.Equals(property.Key, "password", StringComparison.OrdinalIgnoreCase))
                    {
                        var variableName = EnsureRequestBodyVariable(
                            authProducer,
                            "registeredPassword",
                            "$.password",
                            pendingExistingVariables);
                        if (ShouldSetPlaceholderValue(property.Value, variableName))
                        {
                            obj[property.Key] = $"{{{{{variableName}}}}}";
                            EnsureDependency(testCase, authProducer.TestCaseId);
                            changed = true;
                            continue;
                        }
                    }
                }

                if (!allowAuthBootstrapBindings && IsCredentialField(property.Key))
                {
                    continue;
                }

                if (property.Value is JsonValue || property.Value == null)
                {
                    var producer = SelectProducerForToken(
                        property.Key,
                        consumerPath,
                        dependencyCandidates,
                        allCandidates,
                        testCase.Id);

                    if (producer != null)
                    {
                        var currentValue = property.Value?.ToJsonString(JsonOpts)?.Trim('"');
                        var preferredVariableName = TryExtractPlaceholderVariable(currentValue, out var placeholderVariable)
                            ? placeholderVariable
                            : null;
                        var variableName = EnsureProducerVariable(
                            property.Key,
                            consumerPath,
                            producer,
                            pendingExistingVariables,
                            preferredVariableName);

                        var shouldReplace = preferredVariableName != null
                            || (IsIdentifierToken(property.Key) && ShouldReplaceIdentifierValue(currentValue));

                        if (!string.IsNullOrWhiteSpace(variableName) &&
                            shouldReplace)
                        {
                            obj[property.Key] = $"{{{{{variableName}}}}}";
                            EnsureDependency(testCase, producer.TestCaseId);
                            changed = true;
                            continue;
                        }
                    }
                }

                if (property.Value != null &&
                    BindBodyNode(
                        property.Value,
                        consumerPath,
                        dependencyCandidates,
                        allCandidates,
                        testCase,
                        pendingExistingVariables,
                        allowAuthBootstrapBindings))
                {
                    changed = true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null &&
                    BindBodyNode(
                        item,
                        consumerPath,
                        dependencyCandidates,
                        allCandidates,
                        testCase,
                        pendingExistingVariables,
                        allowAuthBootstrapBindings))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static ProducerCandidate SelectProducerForToken(
        string token,
        string consumerPath,
        IReadOnlyList<ProducerCandidate> dependencyCandidates,
        IReadOnlyList<ProducerCandidate> allCandidates,
        Guid currentTestCaseId)
    {
        var preferred = dependencyCandidates.Count > 0
            ? dependencyCandidates
            : allCandidates.Where(x => x.TestCaseId != currentTestCaseId).ToList();

        if (preferred.Count == 0)
        {
            return null;
        }

        return preferred
            .GroupBy(x => x.TestCaseId)
            .Select(g => g.First())
            .OrderByDescending(x => ScoreProducer(x, token, consumerPath))
            .ThenBy(x => x.OrderIndex)
            .FirstOrDefault();
    }

    private static int ScoreProducer(ProducerCandidate producer, string token, string consumerPath)
    {
        var score = 0;
        var targetResource = ResolveTargetResourceSegment(consumerPath, token);

        var producerResources = ExtractStaticResourceSegments(producer.OrderItem?.Path);
        if (!string.IsNullOrWhiteSpace(targetResource))
        {
            if (producerResources.Count > 0 && ResourceEquals(producerResources[^1], targetResource))
            {
                score += 100;
            }
            else if (producerResources.Any(x => ResourceEquals(x, targetResource)))
            {
                score += 60;
            }
        }

        var strippedToken = StripIdSuffix(token);
        if (!string.IsNullOrWhiteSpace(strippedToken) &&
            producerResources.Any(x => ResourceEquals(x, strippedToken)))
        {
            score += 30;
        }

        if (producer.TestType == TestType.HappyPath)
        {
            score += 20;
        }

        score += GetMethodPriority(producer.OrderItem?.HttpMethod);
        return score;
    }

    private static string EnsureProducerVariable(
        string token,
        string consumerPath,
        ProducerCandidate producer,
        ICollection<TestCaseVariable> pendingExistingVariables,
        string preferredVariableName = null)
    {
        var existingVariableName = !string.IsNullOrWhiteSpace(preferredVariableName)
            ? FindReusableVariableName(producer, preferredVariableName, consumerPath)
            : FindReusableVariableName(producer, token, consumerPath);
        if (!string.IsNullOrWhiteSpace(existingVariableName))
        {
            return existingVariableName;
        }

        var variableName = !string.IsNullOrWhiteSpace(preferredVariableName)
            ? preferredVariableName
            : BuildPreferredVariableName(token, consumerPath);
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        foreach (var jsonPath in IdentifierJsonPaths)
        {
            EnsureVariableRule(
                producer,
                pendingExistingVariables,
                variableName,
                ExtractFrom.ResponseBody,
                jsonPath: jsonPath);
        }

        EnsureVariableRule(
            producer,
            pendingExistingVariables,
            variableName,
            ExtractFrom.ResponseHeader,
            headerName: "Location",
            regex: "([^/?#]+)$");

        return variableName;
    }

    private static string EnsureRequestBodyVariable(
        ProducerCandidate producer,
        string variableName,
        string jsonPath,
        ICollection<TestCaseVariable> pendingExistingVariables)
    {
        if (string.IsNullOrWhiteSpace(variableName) || string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        EnsureVariableRule(
            producer,
            pendingExistingVariables,
            variableName,
            ExtractFrom.RequestBody,
            jsonPath: jsonPath);

        return variableName;
    }

    private static string EnsureAuthTokenVariable(
        ProducerCandidate producer,
        ICollection<TestCaseVariable> pendingExistingVariables)
    {
        var existing = producer.Variables
            .Select(v => v.VariableName)
            .FirstOrDefault(name =>
                string.Equals(name, "authToken", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "accessToken", StringComparison.OrdinalIgnoreCase));

        var variableName = string.IsNullOrWhiteSpace(existing) ? "authToken" : existing;
        foreach (var jsonPath in AuthTokenJsonPaths)
        {
            EnsureVariableRule(
                producer,
                pendingExistingVariables,
                variableName,
                ExtractFrom.ResponseBody,
                jsonPath: jsonPath);
        }

        EnsureVariableRule(
            producer,
            pendingExistingVariables,
            variableName,
            ExtractFrom.ResponseHeader,
            headerName: "Authorization",
            regex: "(?:Bearer\\s+)?(?<value>[^\\s]+)$");

        return variableName;
    }

    private static void EnsureVariableRule(
        ProducerCandidate producer,
        ICollection<TestCaseVariable> pendingExistingVariables,
        string variableName,
        ExtractFrom extractFrom,
        string jsonPath = null,
        string headerName = null,
        string regex = null)
    {
        if (producer.Variables.Any(v =>
                string.Equals(v.VariableName, variableName, StringComparison.OrdinalIgnoreCase) &&
                v.ExtractFrom == extractFrom &&
                string.Equals(v.JsonPath, jsonPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.HeaderName, headerName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.Regex, regex, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var variable = new TestCaseVariable
        {
            Id = Guid.NewGuid(),
            TestCaseId = producer.TestCaseId,
            VariableName = variableName,
            ExtractFrom = extractFrom,
            JsonPath = jsonPath,
            HeaderName = headerName,
            Regex = regex,
        };

        producer.Variables.Add(variable);

        if (producer.IsExisting)
        {
            pendingExistingVariables.Add(variable);
        }
    }

    private static string FindReusableVariableName(ProducerCandidate producer, string token, string consumerPath)
    {
        if (producer.Variables.Count == 0)
        {
            return null;
        }

        var preferredName = BuildPreferredVariableName(token, consumerPath);
        var targetResource = ResolveTargetResourceSegment(consumerPath, token);
        var strippedToken = StripIdSuffix(token);

        return producer.Variables
            .Select(v => new
            {
                v.VariableName,
                Score = ScoreVariableName(v.VariableName, preferredName, token, strippedToken, targetResource),
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.VariableName.Length)
            .Select(x => x.VariableName)
            .FirstOrDefault();
    }

    private static int ScoreVariableName(
        string variableName,
        string preferredName,
        string token,
        string strippedToken,
        string targetResource)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return 0;
        }

        if (string.Equals(variableName, preferredName, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (string.Equals(variableName, token, StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        var strippedVariable = StripIdSuffix(variableName);
        if (!string.IsNullOrWhiteSpace(targetResource) && ResourceEquals(strippedVariable, targetResource))
        {
            return 80;
        }

        if (!string.IsNullOrWhiteSpace(strippedToken) && ResourceEquals(strippedVariable, strippedToken))
        {
            return 70;
        }

        if (string.Equals(variableName, "entityId", StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        if (string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return variableName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ? 10 : 0;
    }

    private static void EnsureDependency(TestCase testCase, Guid dependencyTestCaseId)
    {
        if (dependencyTestCaseId == Guid.Empty || dependencyTestCaseId == testCase.Id)
        {
            return;
        }

        if (testCase.Dependencies.Any(x => x.DependsOnTestCaseId == dependencyTestCaseId))
        {
            return;
        }

        testCase.Dependencies.Add(new TestCaseDependency
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCase.Id,
            DependsOnTestCaseId = dependencyTestCaseId,
        });
    }

    private static List<string> ExtractRouteTokens(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new List<string>();
        }

        return RouteTokenRegex.Matches(path)
            .Select(x => x.Groups[1].Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> DeserializeDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts);
            return result != null
                ? new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryExtractPlaceholderVariable(string value, out string variableName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var match = PlaceholderRegex.Match(value.Trim());
            if (match.Success)
            {
                variableName = match.Groups["name"].Value;
                return true;
            }
        }

        variableName = null;
        return false;
    }

    private static ProducerCandidate SelectAuthProducer(
        IReadOnlyList<ProducerCandidate> dependencyCandidates,
        IReadOnlyList<ProducerCandidate> allCandidates,
        Guid currentTestCaseId,
        bool preferRegister)
    {
        var preferred = dependencyCandidates.Count > 0
            ? dependencyCandidates
            : allCandidates.Where(x => x.TestCaseId != currentTestCaseId).ToList();

        return preferred
            .Where(IsAuthLikeProducer)
            .OrderByDescending(x => ScoreAuthProducer(x, preferRegister))
            .ThenBy(x => x.OrderIndex)
            .FirstOrDefault();
    }

    private static bool IsAuthLikeProducer(ProducerCandidate producer)
    {
        return IsAuthLikeEndpoint(producer?.OrderItem);
    }

    private static bool IsAuthLikeEndpoint(ApiOrderItemModel endpoint)
    {
        var signature = $"{endpoint?.HttpMethod} {endpoint?.Path}";
        return signature.Contains("/auth", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("login", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signin", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("register", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signup", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegisterLikeEndpoint(ApiOrderItemModel endpoint)
    {
        var signature = $"{endpoint?.HttpMethod} {endpoint?.Path}";
        return signature.Contains("register", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signup", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreAuthProducer(ProducerCandidate producer, bool preferRegister)
    {
        var signature = $"{producer.OrderItem?.HttpMethod} {producer.OrderItem?.Path}".ToLowerInvariant();
        var score = 0;

        if (preferRegister)
        {
            if (signature.Contains("register", StringComparison.OrdinalIgnoreCase) ||
                signature.Contains("signup", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }
        else
        {
            if (signature.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                signature.Contains("signin", StringComparison.OrdinalIgnoreCase) ||
                signature.Contains("/token", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
        }

        if (producer.TestType == TestType.HappyPath)
        {
            score += 20;
        }

        score += GetMethodPriority(producer.OrderItem?.HttpMethod);
        return score;
    }

    private static bool IsCredentialField(string propertyName)
    {
        return string.Equals(propertyName, "email", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, "password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSetPlaceholderValue(JsonNode currentValue, string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        var raw = currentValue?.ToJsonString(JsonOpts)?.Trim('"');
        if (TryExtractPlaceholderVariable(raw, out var placeholder))
        {
            return !string.Equals(placeholder, variableName, StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(raw) || raw.Contains("example.com", StringComparison.OrdinalIgnoreCase) || raw == "Test123!";
    }

    private static bool ShouldReplaceIdentifierValue(string currentValue)
    {
        if (TryExtractPlaceholderVariable(currentValue, out _))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return true;
        }

        if (currentValue == "0" || currentValue == "1")
        {
            return true;
        }

        if (Guid.TryParse(currentValue, out var guidValue))
        {
            return guidValue == Guid.Empty || currentValue.StartsWith("00000000", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool ShouldReplaceRouteParamValue(TestCase testCase, string token, string currentValue)
    {
        if (testCase == null || !IsHappyPathTestCase(testCase))
        {
            return false;
        }

        if (!IsIdentifierToken(token))
        {
            return false;
        }

        return ShouldReplaceIdentifierValue(currentValue);
    }

    private static bool IsHappyPathTestCase(TestCase testCase)
    {
        return testCase.TestType == TestType.HappyPath;
    }

    private static bool IsIdentifierToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && (string.Equals(token, "id", StringComparison.OrdinalIgnoreCase)
                || token.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                || token.EndsWith("Ids", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetMethodPriority(string method)
    {
        return method?.Trim().ToUpperInvariant() switch
        {
            "POST" => 40,
            "GET" => 20,
            "PUT" => 10,
            "PATCH" => 10,
            _ => 0,
        };
    }

    private static string BuildPreferredVariableName(string token, string path)
    {
        if (!string.IsNullOrWhiteSpace(token) &&
            !string.Equals(token, "id", StringComparison.OrdinalIgnoreCase))
        {
            return token;
        }

        var resource = ResolveTargetResourceSegment(path, token);
        if (string.IsNullOrWhiteSpace(resource))
        {
            return token;
        }

        var identifier = ToCamelIdentifier(resource);
        return string.IsNullOrWhiteSpace(identifier) ? token : $"{identifier}Id";
    }

    private static string ResolveTargetResourceSegment(string path, string token)
    {
        var segments = (path ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        for (int i = 0; i < segments.Count; i++)
        {
            if (!string.Equals(segments[i], $"{{{token}}}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (int j = i - 1; j >= 0; j--)
            {
                var cleaned = CleanSegment(segments[j]);
                if (string.IsNullOrWhiteSpace(cleaned) ||
                    cleaned.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                    IsVersionPrefix(cleaned))
                {
                    continue;
                }

                return Singularize(cleaned);
            }
        }

        var strippedToken = StripIdSuffix(token);
        if (!string.IsNullOrWhiteSpace(strippedToken))
        {
            return Singularize(strippedToken);
        }

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            var cleaned = CleanSegment(segments[i]);
            if (string.IsNullOrWhiteSpace(cleaned) ||
                cleaned.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                IsVersionPrefix(cleaned))
            {
                continue;
            }

            if (!cleaned.StartsWith("{", StringComparison.Ordinal) && !cleaned.EndsWith("}", StringComparison.Ordinal))
            {
                return Singularize(cleaned);
            }
        }

        return null;
    }

    private static List<string> ExtractStaticResourceSegments(string path)
    {
        return (path ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanSegment)
            .Where(x =>
                !string.IsNullOrWhiteSpace(x) &&
                !x.Equals("api", StringComparison.OrdinalIgnoreCase) &&
                !IsVersionPrefix(x) &&
                !x.StartsWith("{", StringComparison.Ordinal) &&
                !x.EndsWith("}", StringComparison.Ordinal))
            .Select(Singularize)
            .ToList();
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

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            value.Length > 1)
        {
            return value[..^1];
        }

        return value;
    }

    private static bool ResourceEquals(string left, string right)
    {
        return string.Equals(Singularize(left), Singularize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVersionPrefix(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 3 &&
            value.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
            value.Skip(1).All(char.IsDigit);
    }

    private static string CleanSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return segment;
        }

        return segment.Trim().Trim('/').Trim();
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

    private sealed class ProducerCandidate
    {
        public ProducerCandidate(
            TestCase sourceTestCase,
            Guid endpointId,
            ApiOrderItemModel orderItem,
            int orderIndex,
            TestType testType,
            ICollection<TestCaseVariable> variables,
            bool isExisting)
        {
            SourceTestCase = sourceTestCase;
            TestCaseId = sourceTestCase.Id;
            EndpointId = endpointId;
            OrderItem = orderItem;
            OrderIndex = orderIndex;
            TestType = testType;
            Variables = variables ?? new List<TestCaseVariable>();
            IsExisting = isExisting;
        }

        public TestCase SourceTestCase { get; }
        public Guid TestCaseId { get; }
        public Guid EndpointId { get; }
        public ApiOrderItemModel OrderItem { get; }
        public int OrderIndex { get; }
        public TestType TestType { get; }
        public ICollection<TestCaseVariable> Variables { get; }
        public bool IsExisting { get; }
    }
}

public sealed class GeneratedTestCaseEnrichmentResult
{
    public static readonly GeneratedTestCaseEnrichmentResult Empty = new(Array.Empty<TestCaseVariable>());

    public GeneratedTestCaseEnrichmentResult(IReadOnlyList<TestCaseVariable> existingProducerVariablesToPersist)
    {
        ExistingProducerVariablesToPersist = existingProducerVariablesToPersist ?? Array.Empty<TestCaseVariable>();
    }

    public IReadOnlyList<TestCaseVariable> ExistingProducerVariablesToPersist { get; }
}
