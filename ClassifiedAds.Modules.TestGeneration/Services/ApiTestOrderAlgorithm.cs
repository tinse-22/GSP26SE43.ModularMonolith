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
/// 4. Combine all dependency edges.
/// 5. Run DependencyAwareTopologicalSorter with fan-out ranking (KAT Section 4.3).
/// 6. Map results to ApiOrderItemModel.
/// </summary>
public class ApiTestOrderAlgorithm : IApiTestOrderAlgorithm
{
    private readonly ISchemaRelationshipAnalyzer _schemaAnalyzer;
    private readonly IDependencyAwareTopologicalSorter _topologicalSorter;

    public ApiTestOrderAlgorithm()
        : this(new SchemaRelationshipAnalyzer(), new DependencyAwareTopologicalSorter())
    {
    }

    public ApiTestOrderAlgorithm(
        ISchemaRelationshipAnalyzer schemaAnalyzer,
        IDependencyAwareTopologicalSorter topologicalSorter)
    {
        _schemaAnalyzer = schemaAnalyzer;
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
            var directGraph = _schemaAnalyzer.BuildSchemaReferenceGraph(allSchemaPayloads);
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
}
