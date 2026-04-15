using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Enhanced API test order algorithm using paper-based algorithms.
///
/// Pipeline (KAT paper + SPDG paper):
/// 1. Collect pre-computed dependencies from ApiEndpointMetadataService (Rules 1-4).
/// 2. Run SchemaRelationshipAnalyzer for Schema-Schema transitive dependencies (KAT Section 4.2).
/// 3. Run SchemaRelationshipAnalyzer for fuzzy schema name matching.
/// 4. Run SemanticTokenMatcher for parameter-to-path token matching (SPDG Section 3.2).
/// 5. Combine all dependency edges.
/// 6. Run DependencyAwareTopologicalSorter with fan-out ranking (KAT Section 4.3).
/// 7. Map results to ApiOrderItemModel.
/// </summary>
public class ApiTestOrderAlgorithm : IApiTestOrderAlgorithm
{
    private readonly ISchemaRelationshipAnalyzer _schemaAnalyzer;
    private readonly ISemanticTokenMatcher _semanticTokenMatcher;
    private readonly IDependencyAwareTopologicalSorter _topologicalSorter;

    /// <summary>
    /// Minimum match score from SemanticTokenMatcher to create a dependency edge.
    /// Score >= 0.8 means high confidence (exact, plural/singular, or abbreviation match).
    /// </summary>
    private const double MinSemanticMatchScore = 0.80;

    public ApiTestOrderAlgorithm()
        : this(new SchemaRelationshipAnalyzer(), new SemanticTokenMatcher(), new DependencyAwareTopologicalSorter())
    {
    }

    public ApiTestOrderAlgorithm(
        ISchemaRelationshipAnalyzer schemaAnalyzer,
        ISemanticTokenMatcher semanticTokenMatcher,
        IDependencyAwareTopologicalSorter topologicalSorter)
    {
        _schemaAnalyzer = schemaAnalyzer;
        _semanticTokenMatcher = semanticTokenMatcher;
        _topologicalSorter = topologicalSorter;
    }

    public IReadOnlyList<ApiOrderItemModel> BuildProposalOrder(IReadOnlyCollection<ApiEndpointMetadataDto> endpoints)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return Array.Empty<ApiOrderItemModel>();
        }

        var deduplicatedEndpoints = endpoints
            .GroupBy(x => x.EndpointId)
            .Select(x => x.First())
            .ToList();

        // Step 1: Collect all dependency edges.
        var allEdges = new List<DependencyEdge>();

        // Step 1a: Pre-computed dependencies from ApiEndpointMetadataService (Rules 1-4).
        foreach (var endpoint in deduplicatedEndpoints)
        {
            if (endpoint.DependsOnEndpointIds == null || endpoint.DependsOnEndpointIds.Count == 0)
            {
                continue;
            }

            foreach (var depId in endpoint.DependsOnEndpointIds)
            {
                if (depId == Guid.Empty || depId == endpoint.EndpointId)
                {
                    continue;
                }

                allEdges.Add(new DependencyEdge
                {
                    SourceOperationId = endpoint.EndpointId,
                    TargetOperationId = depId,
                    Type = DependencyEdgeType.OperationSchema,
                    Reason = "Pre-computed dependency from ApiEndpointMetadataService (Rules 1-4).",
                    Confidence = 1.0,
                });
            }
        }

        // Step 1b: Schema-Schema transitive dependencies (KAT paper).
        var schemaEdges = FindSchemaSchemaEdges(deduplicatedEndpoints);
        allEdges.AddRange(schemaEdges);

        // Step 1c: Semantic token dependencies (SPDG paper).
        var semanticEdges = FindSemanticTokenEdges(deduplicatedEndpoints);
        allEdges.AddRange(semanticEdges);

        // Step 2: Build sortable operations.
        var sortableOps = deduplicatedEndpoints
            .Select(e => new SortableOperation
            {
                OperationId = e.EndpointId,
                HttpMethod = e.HttpMethod,
                Path = e.Path,
                IsAuthRelated = e.IsAuthRelated,
            })
            .ToList();

        // Step 3: Run enhanced topological sort with fan-out ranking.
        var sortedResults = _topologicalSorter.Sort(sortableOps, allEdges);

        // Step 4: Map to ApiOrderItemModel.
        var endpointMap = deduplicatedEndpoints.ToDictionary(e => e.EndpointId);

        return sortedResults
            .Select(result =>
            {
                var endpoint = endpointMap[result.OperationId];
                return new ApiOrderItemModel
                {
                    EndpointId = result.OperationId,
                    HttpMethod = endpoint.HttpMethod,
                    Path = endpoint.Path,
                    OrderIndex = result.OrderIndex,
                    DependsOnEndpointIds = result.Dependencies,
                    ReasonCodes = result.ReasonCodes,
                    IsAuthRelated = endpoint.IsAuthRelated,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Use SchemaRelationshipAnalyzer to find additional Schema-Schema dependencies.
    /// This is the key algorithm from KAT paper Section 4.2.
    /// </summary>
    private IReadOnlyCollection<DependencyEdge> FindSchemaSchemaEdges(
        IReadOnlyList<ApiEndpointMetadataDto> endpoints)
    {
        var edges = new List<DependencyEdge>();

        // Build parameter and response schema ref maps.
        var paramSchemaRefs = new Dictionary<Guid, IReadOnlyCollection<string>>();
        var respSchemaRefs = new Dictionary<Guid, IReadOnlyCollection<string>>();
        var allSchemaPayloads = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var paramRefs = endpoint.ParameterSchemaRefs ?? Array.Empty<string>();
            var respRefs = endpoint.ResponseSchemaRefs ?? Array.Empty<string>();

            if (paramRefs.Count > 0)
            {
                paramSchemaRefs[endpoint.EndpointId] = paramRefs;
            }

            if (respRefs.Count > 0)
            {
                respSchemaRefs[endpoint.EndpointId] = respRefs;
            }

            // Collect all schema payloads for graph building.
            if (endpoint.ParameterSchemaPayloads != null)
            {
                allSchemaPayloads.AddRange(endpoint.ParameterSchemaPayloads);
            }

            if (endpoint.ResponseSchemaPayloads != null)
            {
                allSchemaPayloads.AddRange(endpoint.ResponseSchemaPayloads);
            }
        }

        if (paramSchemaRefs.Count == 0 || respSchemaRefs.Count == 0)
        {
            return edges;
        }

        // Build schema reference graph and compute transitive closure.
        if (allSchemaPayloads.Count > 0)
        {
            var directGraph = _schemaAnalyzer.BuildSchemaReferenceGraphLegacy(allSchemaPayloads);
            if (directGraph.Count > 0)
            {
                var transitiveGraph = _schemaAnalyzer.ComputeTransitiveClosure(directGraph);
                var transitiveEdges = _schemaAnalyzer.FindTransitiveSchemaDependencies(
                    paramSchemaRefs, respSchemaRefs, transitiveGraph);
                edges.AddRange(transitiveEdges);
            }
        }

        // Fuzzy schema name matching.
        var fuzzyEdges = _schemaAnalyzer.FindFuzzySchemaNameDependencies(paramSchemaRefs, respSchemaRefs);
        edges.AddRange(fuzzyEdges);

        // Filter to only edges between endpoints in our set.
        var endpointIds = endpoints.Select(e => e.EndpointId).ToHashSet();
        return edges
            .Where(e => endpointIds.Contains(e.SourceOperationId) && endpointIds.Contains(e.TargetOperationId))
            .ToList();
    }

    /// <summary>
    /// Use SemanticTokenMatcher to find dependencies based on parameter-to-path token matching.
    /// This is the key algorithm from SPDG paper (arXiv:2411.07098) Section 3.2.
    ///
    /// Algorithm:
    /// 1. For each consumer endpoint, extract parameter tokens (from ParameterNames and path params).
    /// 2. For each producer endpoint (POST/PUT), extract resource tokens from path segments.
    /// 3. Match consumer parameter tokens against producer resource tokens.
    /// 4. If match score >= MinSemanticMatchScore, create a SemanticToken dependency edge.
    /// </summary>
    private IReadOnlyCollection<DependencyEdge> FindSemanticTokenEdges(
        IReadOnlyList<ApiEndpointMetadataDto> endpoints)
    {
        var edges = new List<DependencyEdge>();
        var seenPairs = new HashSet<(Guid, Guid)>();

        // Identify producer endpoints (POST/PUT typically create/update resources).
        var producerEndpoints = endpoints
            .Where(e => e.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) == true
                        || e.HttpMethod?.Equals("PUT", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (producerEndpoints.Count == 0)
        {
            return edges;
        }

        // Build producer token map: endpointId → resource tokens from path.
        var producerTokens = new Dictionary<Guid, IReadOnlyCollection<string>>();
        foreach (var producer in producerEndpoints)
        {
            var tokens = ExtractResourceTokensFromPath(producer.Path);
            if (tokens.Count > 0)
            {
                producerTokens[producer.EndpointId] = tokens;
            }
        }

        // For each consumer endpoint, find matching producers.
        foreach (var consumer in endpoints)
        {
            var consumerParams = ExtractParameterTokens(consumer);
            if (consumerParams.Count == 0)
            {
                continue;
            }

            foreach (var (producerId, resourceTokens) in producerTokens)
            {
                // Skip self-dependency.
                if (producerId == consumer.EndpointId)
                {
                    continue;
                }

                // Skip if already seen (from other dependency rules).
                var pair = (consumer.EndpointId, producerId);
                if (seenPairs.Contains(pair))
                {
                    continue;
                }

                // Find matches between consumer parameter tokens and producer resource tokens.
                var matches = _semanticTokenMatcher.FindMatches(
                    consumerParams,
                    resourceTokens,
                    MinSemanticMatchScore);

                if (matches.Count == 0)
                {
                    continue;
                }

                // Use the best match for the edge reason.
                var bestMatch = matches.First();
                seenPairs.Add(pair);

                edges.Add(new DependencyEdge
                {
                    SourceOperationId = consumer.EndpointId,
                    TargetOperationId = producerId,
                    Type = DependencyEdgeType.SemanticToken,
                    Reason = $"Parameter token '{bestMatch.SourceToken}' matches resource token '{bestMatch.MatchedToken}' (score: {bestMatch.Score:F2}, type: {bestMatch.MatchType}).",
                    Confidence = bestMatch.Score,
                });
            }
        }

        return edges;
    }

    /// <summary>
    /// Extract parameter tokens from an endpoint for semantic matching.
    /// Sources:
    /// 1. ParameterNames from DTO (if populated).
    /// 2. Path parameters extracted from URL pattern (e.g., {userId} → userId).
    /// 3. ParameterSchemaRefs (schema names often indicate parameter concepts).
    /// </summary>
    private static IReadOnlyCollection<string> ExtractParameterTokens(ApiEndpointMetadataDto endpoint)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Source 1: Explicit parameter names.
        if (endpoint.ParameterNames != null)
        {
            foreach (var name in endpoint.ParameterNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                tokens.Add(name.Trim());

                // Also add without common suffixes like "Id", "Ids".
                var stripped = StripIdSuffix(name.Trim());
                if (!string.IsNullOrWhiteSpace(stripped) && stripped.Length >= 2)
                {
                    tokens.Add(stripped);
                }
            }
        }

        // Source 2: Path parameters from URL pattern.
        if (!string.IsNullOrWhiteSpace(endpoint.Path))
        {
            var pathParams = ExtractPathParameters(endpoint.Path);
            foreach (var param in pathParams)
            {
                tokens.Add(param);

                var stripped = StripIdSuffix(param);
                if (!string.IsNullOrWhiteSpace(stripped) && stripped.Length >= 2)
                {
                    tokens.Add(stripped);
                }
            }
        }

        // Source 3: Parameter schema refs (extract meaningful base names).
        if (endpoint.ParameterSchemaRefs != null)
        {
            foreach (var schemaRef in endpoint.ParameterSchemaRefs.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                // Extract base name from schema ref (e.g., "CreateUserRequest" → "User").
                var baseName = ExtractSchemaBaseName(schemaRef);
                if (!string.IsNullOrWhiteSpace(baseName) && baseName.Length >= 2)
                {
                    tokens.Add(baseName);
                }
            }
        }

        return tokens.ToList();
    }

    /// <summary>
    /// Extract resource tokens from a path for semantic matching.
    /// E.g., "/api/v1/users/{id}/orders" → ["users", "orders"].
    /// </summary>
    private static IReadOnlyCollection<string> ExtractResourceTokensFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            // Skip path parameters, version prefixes, and "api".
            if (segment.Contains('{') || IsVersionPrefix(segment)
                || segment.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip too short segments.
            if (segment.Length < 2)
            {
                continue;
            }

            tokens.Add(segment);

            // Also add singular form if it looks like a plural.
            var singular = Singularize(segment);
            if (!string.Equals(singular, segment, StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(singular);
            }
        }

        return tokens.ToList();
    }

    /// <summary>
    /// Extract path parameters from URL pattern.
    /// E.g., "/users/{userId}/orders/{orderId}" → ["userId", "orderId"].
    /// </summary>
    private static IReadOnlyCollection<string> ExtractPathParameters(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var parameters = new List<string>();
        var startIndex = 0;

        while ((startIndex = path.IndexOf('{', startIndex)) >= 0)
        {
            var endIndex = path.IndexOf('}', startIndex);
            if (endIndex < 0)
            {
                break;
            }

            var param = path.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            if (!string.IsNullOrWhiteSpace(param))
            {
                parameters.Add(param);
            }

            startIndex = endIndex + 1;
        }

        return parameters;
    }

    /// <summary>
    /// Strip common ID suffixes from parameter names.
    /// E.g., "userId" → "user", "categoryIds" → "category".
    /// </summary>
    private static string StripIdSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
        {
            return name[..^3];
        }

        if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && name.Length > 2)
        {
            return name[..^2];
        }

        return name;
    }

    /// <summary>
    /// Check if a path segment is a version prefix (e.g., "v1", "v2").
    /// </summary>
    private static bool IsVersionPrefix(string segment)
    {
        return segment.Length <= 3
               && segment.StartsWith('v')
               && segment.Length > 1
               && char.IsDigit(segment[1]);
    }

    /// <summary>
    /// Simple singularization for common plural patterns.
    /// </summary>
    private static string Singularize(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 3)
        {
            return word;
        }

        var lower = word.ToLowerInvariant();

        // -ies → -y (categories → category).
        if (lower.EndsWith("ies", StringComparison.Ordinal) && lower.Length > 4)
        {
            return word[..^3] + "y";
        }

        // -es → remove -es for -ses, -xes, -ches, -shes.
        if ((lower.EndsWith("ses", StringComparison.Ordinal)
             || lower.EndsWith("xes", StringComparison.Ordinal)
             || lower.EndsWith("ches", StringComparison.Ordinal)
             || lower.EndsWith("shes", StringComparison.Ordinal))
            && lower.Length > 4)
        {
            return word[..^2];
        }

        // -s → remove -s.
        if (lower.EndsWith('s') && !lower.EndsWith("ss", StringComparison.Ordinal)
            && !lower.EndsWith("us", StringComparison.Ordinal))
        {
            return word[..^1];
        }

        return word;
    }

    /// <summary>
    /// Extract meaningful base name from a schema name.
    /// E.g., "CreateUserRequest" → "User", "UserResponse" → "User".
    /// </summary>
    private static string ExtractSchemaBaseName(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        var name = schemaName.Trim();

        // Strip common suffixes.
        string[] suffixes = { "Request", "Response", "Dto", "DTO", "Model", "Input", "Output", "Command", "Query" };
        foreach (var suffix in suffixes.OrderByDescending(s => s.Length))
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        // Strip common prefixes.
        string[] prefixes = { "Create", "Update", "Delete", "Get", "List", "Add", "Remove" };
        foreach (var prefix in prefixes.OrderByDescending(p => p.Length))
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        return name.Length >= 2 ? name : null;
    }
}
