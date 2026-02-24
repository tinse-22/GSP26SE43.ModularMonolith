using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Dependency-aware topological sort with enhanced tie-breaking.
/// Source: KAT paper (arXiv:2407.10227) Section 4.3 - Sequence Generation.
///
/// Improvements over basic Kahn's algorithm:
/// - Fan-out ranking: operations producing schemas consumed by more operations get higher priority.
/// - Dependency edge type weighting: rule-based edges outweigh fuzzy/semantic edges.
/// - Deterministic tie-breaking with multiple criteria.
/// - Cycle detection and breaking with ranking-aware fallback.
/// </summary>
public interface IDependencyAwareTopologicalSorter
{
    /// <summary>
    /// Sort operations in dependency-aware order using enhanced Kahn's algorithm.
    /// </summary>
    /// <param name="operations">Operations to sort. Each must have a unique OperationId.</param>
    /// <param name="dependencyEdges">
    /// All dependency edges. Each edge means: SourceOperationId depends on TargetOperationId.
    /// </param>
    /// <returns>
    /// Sorted operation IDs in execution order (dependencies first).
    /// Includes metadata about cycle breaks and reason codes.
    /// </returns>
    IReadOnlyList<SortedOperationResult> Sort(
        IReadOnlyCollection<SortableOperation> operations,
        IReadOnlyCollection<DependencyEdge> dependencyEdges);
}

/// <summary>
/// Input for topological sort: operation with metadata for tie-breaking.
/// </summary>
public class SortableOperation
{
    public Guid OperationId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public bool IsAuthRelated { get; set; }
}

/// <summary>
/// Output of topological sort: operation with ordering metadata.
/// </summary>
public class SortedOperationResult
{
    public Guid OperationId { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// How many other operations depend on this one (fan-out).
    /// Higher fan-out = more important to execute early.
    /// </summary>
    public int FanOut { get; set; }

    /// <summary>
    /// True if this operation's position required breaking a dependency cycle.
    /// </summary>
    public bool IsCycleBreak { get; set; }

    /// <summary>
    /// Reason codes explaining why this operation is at this position.
    /// </summary>
    public List<string> ReasonCodes { get; set; } = new();

    /// <summary>
    /// Direct dependencies (operations that must execute before this one).
    /// </summary>
    public List<Guid> Dependencies { get; set; } = new();

    /// <summary>
    /// Edges that led to the dependencies.
    /// </summary>
    public List<DependencyEdge> DependencyEdges { get; set; } = new();
}
