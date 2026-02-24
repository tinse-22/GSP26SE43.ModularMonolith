using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Dependency-aware topological sort with fan-out ranking and deterministic tie-breaking.
/// Source: KAT paper (arXiv:2407.10227) Section 4.3 - Sequence Generation.
///
/// Algorithm:
/// 1. Build adjacency list from dependency edges (respecting confidence threshold).
/// 2. Compute in-degree for each node.
/// 3. Modified Kahn's algorithm:
///    a. Pick nodes with in-degree 0 (ready to execute).
///    b. Among candidates, rank by:
///       i.   Auth-related operations first.
///       ii.  Fan-out (dependent count) descending - KAT enhancement.
///       iii. HTTP method weight (POST first, DELETE last).
///       iv.  Path alphabetical.
///       v.   OperationId for absolute determinism.
///    c. If no candidates (cycle), break cycle by selecting the node with
///       lowest in-degree and highest fan-out.
/// 4. Annotate each result with reason codes and dependency metadata.
/// </summary>
public class DependencyAwareTopologicalSorter : IDependencyAwareTopologicalSorter
{
    private static readonly IReadOnlyDictionary<string, int> MethodWeights =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["POST"] = 1,
            ["PUT"] = 2,
            ["PATCH"] = 3,
            ["GET"] = 4,
            ["DELETE"] = 5,
            ["OPTIONS"] = 6,
            ["HEAD"] = 7,
        };

    /// <summary>
    /// Minimum confidence for an edge to be included in the graph.
    /// Edges below this threshold are recorded but don't enforce ordering.
    /// </summary>
    private const double MinEdgeConfidence = 0.5;

    /// <inheritdoc />
    public IReadOnlyList<SortedOperationResult> Sort(
        IReadOnlyCollection<SortableOperation> operations,
        IReadOnlyCollection<DependencyEdge> dependencyEdges)
    {
        if (operations == null || operations.Count == 0)
        {
            return Array.Empty<SortedOperationResult>();
        }

        var edges = dependencyEdges ?? Array.Empty<DependencyEdge>();

        // Deduplicate operations.
        var operationById = operations
            .GroupBy(o => o.OperationId)
            .ToDictionary(g => g.Key, g => g.First());

        var operationIds = operationById.Keys.ToHashSet();

        // Build adjacency lists: dependencies (who I depend on) and dependents (who depends on me).
        var dependenciesMap = new Dictionary<Guid, HashSet<Guid>>();
        var dependentsMap = new Dictionary<Guid, HashSet<Guid>>();
        var edgesBySourceTarget = new Dictionary<(Guid, Guid), List<DependencyEdge>>();
        var allEdgesBySource = new Dictionary<Guid, List<DependencyEdge>>();

        foreach (var opId in operationIds)
        {
            dependenciesMap[opId] = new HashSet<Guid>();
            dependentsMap[opId] = new HashSet<Guid>();
            allEdgesBySource[opId] = new List<DependencyEdge>();
        }

        foreach (var edge in edges)
        {
            // Only include edges between operations in our set.
            if (!operationIds.Contains(edge.SourceOperationId)
                || !operationIds.Contains(edge.TargetOperationId))
            {
                continue;
            }

            // Skip self-dependencies.
            if (edge.SourceOperationId == edge.TargetOperationId)
            {
                continue;
            }

            // Record all edges for metadata.
            var key = (edge.SourceOperationId, edge.TargetOperationId);
            if (!edgesBySourceTarget.TryGetValue(key, out var edgeList))
            {
                edgeList = new List<DependencyEdge>();
                edgesBySourceTarget[key] = edgeList;
            }

            edgeList.Add(edge);

            if (!allEdgesBySource.TryGetValue(edge.SourceOperationId, out var sourceEdges))
            {
                sourceEdges = new List<DependencyEdge>();
                allEdgesBySource[edge.SourceOperationId] = sourceEdges;
            }

            sourceEdges.Add(edge);

            // Only enforce ordering for high-confidence edges.
            if (edge.Confidence < MinEdgeConfidence)
            {
                continue;
            }

            dependenciesMap[edge.SourceOperationId].Add(edge.TargetOperationId);
            dependentsMap[edge.TargetOperationId].Add(edge.SourceOperationId);
        }

        // Compute in-degrees.
        var inDegree = operationIds.ToDictionary(
            id => id,
            id => dependenciesMap[id].Count);

        // Compute fan-out (number of dependents) - KAT enhancement.
        var fanOut = operationIds.ToDictionary(
            id => id,
            id => dependentsMap[id].Count);

        // Modified Kahn's algorithm.
        var sorted = new List<SortedOperationResult>(operationIds.Count);
        var visited = new HashSet<Guid>();
        var available = new HashSet<Guid>(
            operationIds.Where(id => inDegree[id] == 0));

        while (sorted.Count < operationIds.Count)
        {
            bool isCycleBreak = false;

            if (available.Count == 0)
            {
                // Cycle detected. Break by selecting the node with lowest in-degree and highest fan-out.
                var cycleBreaker = operationIds
                    .Where(id => !visited.Contains(id))
                    .OrderBy(id => inDegree[id])
                    .ThenByDescending(id => fanOut[id])
                    .ThenBy(id => operationById[id].IsAuthRelated ? 0 : 1)
                    .ThenBy(id => GetMethodWeight(operationById[id].HttpMethod))
                    .ThenBy(id => operationById[id].Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => id)
                    .First();

                available.Add(cycleBreaker);
                isCycleBreak = true;
            }

            // Select the best candidate using enhanced tie-breaking.
            var selectedId = available
                .OrderBy(id => operationById[id].IsAuthRelated ? 0 : 1)        // Auth first
                .ThenByDescending(id => fanOut[id])                              // Fan-out ranking (KAT)
                .ThenBy(id => inDegree[id])                                      // Lower in-degree preferred
                .ThenBy(id => GetMethodWeight(operationById[id].HttpMethod))     // POST before GET before DELETE
                .ThenBy(id => operationById[id].Path, StringComparer.OrdinalIgnoreCase) // Path alphabetical
                .ThenBy(id => id)                                                // Guid for absolute determinism
                .First();

            available.Remove(selectedId);

            if (!visited.Add(selectedId))
            {
                continue;
            }

            // Build result with metadata.
            var result = new SortedOperationResult
            {
                OperationId = selectedId,
                OrderIndex = sorted.Count + 1,
                FanOut = fanOut[selectedId],
                IsCycleBreak = isCycleBreak,
                Dependencies = dependenciesMap[selectedId].OrderBy(x => x).ToList(),
                DependencyEdges = allEdgesBySource.TryGetValue(selectedId, out var opEdges)
                    ? opEdges.ToList()
                    : new List<DependencyEdge>(),
                ReasonCodes = BuildReasonCodes(
                    operationById[selectedId],
                    dependenciesMap[selectedId],
                    dependentsMap[selectedId],
                    isCycleBreak,
                    fanOut[selectedId]),
            };

            sorted.Add(result);

            // Update in-degrees for dependents.
            foreach (var dependentId in dependentsMap[selectedId])
            {
                if (visited.Contains(dependentId))
                {
                    continue;
                }

                inDegree[dependentId] = Math.Max(0, inDegree[dependentId] - 1);
                if (inDegree[dependentId] == 0)
                {
                    available.Add(dependentId);
                }
            }
        }

        return sorted;
    }

    private static int GetMethodWeight(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return int.MaxValue;
        }

        return MethodWeights.TryGetValue(method.Trim(), out var weight) ? weight : int.MaxValue;
    }

    private static List<string> BuildReasonCodes(
        SortableOperation operation,
        IReadOnlyCollection<Guid> dependencies,
        IReadOnlyCollection<Guid> dependents,
        bool isCycleBreak,
        int fanOutCount)
    {
        var reasons = new List<string>();

        if (operation.IsAuthRelated)
        {
            reasons.Add("AUTH_FIRST");
        }

        if (dependencies.Count > 0)
        {
            reasons.Add("DEPENDENCY_FIRST");
        }

        if (dependents.Count > 0)
        {
            reasons.Add("PRODUCER_FIRST");
        }

        if (fanOutCount > 2)
        {
            reasons.Add("HIGH_FAN_OUT");
        }

        if (isCycleBreak)
        {
            reasons.Add("CYCLE_BREAK_FALLBACK");
        }

        reasons.Add("DETERMINISTIC_TIE_BREAK");

        return reasons;
    }
}
