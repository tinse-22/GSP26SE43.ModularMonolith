using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Analyzes schema-to-schema relationships to discover transitive dependencies.
/// Source: KAT paper (arXiv:2407.10227) Section 4.2 - Schema-Schema Dependencies.
///
/// Current code (Rule 2 in ApiEndpointMetadataService) only detects DIRECT matches:
///   parameter schema ref == response schema ref → dependency.
///
/// This analyzer adds TRANSITIVE resolution:
///   If parameter refs schema A, and A internally refs schema B,
///   and B is produced by another operation → transitive dependency.
/// </summary>
public interface ISchemaRelationshipAnalyzer
{
    /// <summary>
    /// Build a directed graph of schema-name → set of schema-names it references.
    /// Extracts all $ref from each schema payload and maps them by name.
    /// </summary>
    /// <param name="schemaPayloads">All schema JSON payloads from all endpoints (parameters + responses).</param>
    /// <returns>Map: schema name → referenced schema names (direct only).</returns>
    IReadOnlyDictionary<string, HashSet<string>> BuildSchemaReferenceGraph(
        IReadOnlyCollection<string> schemaPayloads);

    /// <summary>
    /// Compute transitive closure of a schema reference graph.
    /// If A → B → C, the closure includes A → {B, C}.
    /// </summary>
    IReadOnlyDictionary<string, HashSet<string>> ComputeTransitiveClosure(
        IReadOnlyDictionary<string, HashSet<string>> directReferences);

    /// <summary>
    /// Find additional operation dependencies through schema-schema chains.
    /// For each operation's parameter schema refs, walk the transitive closure
    /// and find operations that produce those schemas in their responses.
    /// </summary>
    /// <param name="operationParameterSchemaRefs">Map: operationId → parameter schema ref names.</param>
    /// <param name="operationResponseSchemaRefs">Map: operationId → response schema ref names.</param>
    /// <param name="transitiveSchemaGraph">Transitive closure of schema references.</param>
    /// <returns>Additional dependency edges of type SchemaSchema.</returns>
    IReadOnlyCollection<DependencyEdge> FindTransitiveSchemaDependencies(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationParameterSchemaRefs,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationResponseSchemaRefs,
        IReadOnlyDictionary<string, HashSet<string>> transitiveSchemaGraph);

    /// <summary>
    /// Find additional dependencies through fuzzy schema name matching.
    /// Matches schema names that share a common base (e.g., "CreateUserRequest" and "UserResponse"
    /// both contain "User" → potential relationship).
    /// </summary>
    IReadOnlyCollection<DependencyEdge> FindFuzzySchemaNameDependencies(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationParameterSchemaRefs,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationResponseSchemaRefs);
}
