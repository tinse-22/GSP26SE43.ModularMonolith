using System;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms.Models;

/// <summary>
/// Represents a directed dependency edge between two API operations.
/// Source: KAT paper (arXiv:2407.10227) - Operation Dependency Graph (ODG).
/// Each edge records WHY operation A depends on operation B.
/// </summary>
public class DependencyEdge
{
    /// <summary>
    /// The consumer operation that DEPENDS ON the target.
    /// </summary>
    public Guid SourceOperationId { get; set; }

    /// <summary>
    /// The producer operation that MUST execute before the source.
    /// </summary>
    public Guid TargetOperationId { get; set; }

    /// <summary>
    /// Classification of this dependency per KAT/SPDG taxonomy.
    /// </summary>
    public DependencyEdgeType Type { get; set; }

    /// <summary>
    /// Human-readable reason for this dependency.
    /// Example: "Parameter 'userId' schema ref 'User' → produced by POST /users response".
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Confidence score [0.0 - 1.0]. Rule-based = 1.0, semantic/fuzzy = lower.
    /// Used for ranking when multiple candidates exist.
    /// </summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Taxonomy of dependency types based on KAT + SPDG papers.
/// </summary>
public enum DependencyEdgeType
{
    /// <summary>
    /// KAT heuristic: endpoint with {id} path parameter depends on POST to same resource.
    /// Example: GET /users/{id} → POST /users.
    /// </summary>
    PathBased = 0,

    /// <summary>
    /// KAT Operation-Schema: parameter schema $ref matches response schema $ref of another operation.
    /// Example: PUT /orders/{id} param schema refs "Order" → POST /orders response produces "Order".
    /// </summary>
    OperationSchema = 1,

    /// <summary>
    /// KAT Schema-Schema: transitive dependency through schema internal $ref chains.
    /// Example: param refs "OrderRequest" which internally refs "OrderItem" → produced by POST /items.
    /// </summary>
    SchemaSchema = 2,

    /// <summary>
    /// SPDG semantic token: parameter name tokens match resource path tokens of a producer.
    /// Example: parameter "categoryId" → POST /categories produces "category" resource.
    /// </summary>
    SemanticToken = 3,

    /// <summary>
    /// KAT auth bootstrap: secured endpoint must first call auth/token endpoint.
    /// </summary>
    AuthBootstrap = 4,
}
