using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Analyzes schema-to-schema relationships to discover transitive dependencies.
/// Source: KAT paper (arXiv:2407.10227) Section 4.2 - Schema-Schema Dependencies.
///
/// Algorithm overview:
/// 1) Extract all $ref from each schema payload → direct reference graph.
/// 2) Compute transitive closure via BFS/DFS.
/// 3) For each consumer operation's parameter schema refs, walk closure to find
///    producer operations whose response schemas are transitively referenced.
/// 4) Fuzzy name matching catches related schemas (e.g., CreateUserRequest → User).
/// </summary>
public class SchemaRelationshipAnalyzer : ISchemaRelationshipAnalyzer
{
    private static readonly Regex SchemaRefRegex = new(
        @"#/(?:components/schemas|definitions)/(?<name>[A-Za-z0-9_.\-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Common suffixes stripped for fuzzy schema name matching.
    /// "CreateUserRequest" → base "User", "UserResponse" → base "User".
    /// </summary>
    private static readonly string[] SchemaNameSuffixes =
    {
        "Request", "Response", "Dto", "DTO", "Model", "Input", "Output",
        "Command", "Query", "Event", "Payload", "Body", "Result",
        "Create", "Update", "Patch", "Delete",
        "ListResponse", "ListResult", "PagedResult",
    };

    private static readonly string[] SchemaNamePrefixes =
    {
        "Create", "Update", "Patch", "Delete", "Add", "Remove",
        "Get", "List", "Search", "Find", "Fetch",
    };

    /// <inheritdoc />
    public IReadOnlyDictionary<string, HashSet<string>> BuildSchemaReferenceGraph(
        IReadOnlyCollection<string> schemaPayloads)
    {
        if (schemaPayloads == null || schemaPayloads.Count == 0)
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        // Step 1: Collect all unique schema names mentioned across all payloads.
        var allSchemaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var payloadsBySchemaName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var payload in schemaPayloads.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var refsInPayload = ExtractSchemaRefsFromPayload(payload);
            foreach (var refName in refsInPayload)
            {
                allSchemaNames.Add(refName);
            }

            // If the payload is a top-level $ref (e.g., {"$ref": "#/components/schemas/User"}),
            // the schema name IS the ref. Otherwise, we try to identify which schema this payload defines.
            if (refsInPayload.Count == 1 && IsBareSingleRef(payload))
            {
                // This payload is just a pointer; the ref name is the schema it references.
                continue;
            }

            // For payloads that contain the actual definition (inline schemas),
            // map each referenced schema to this payload for later analysis.
            foreach (var refName in refsInPayload)
            {
                if (!payloadsBySchemaName.TryGetValue(refName, out var payloads))
                {
                    payloads = new List<string>();
                    payloadsBySchemaName[refName] = payloads;
                }

                payloads.Add(payload);
            }
        }

        // Step 2: Build the reference graph.
        // For each schema name, find all OTHER schema names that appear in payloads alongside it.
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var schemaName in allSchemaNames)
        {
            if (!graph.ContainsKey(schemaName))
            {
                graph[schemaName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // For each payload, all schema refs in it are co-referenced.
        // If payload contains refs to {A, B, C}, then A→{B,C}, B→{A,C}, C→{A,B}.
        foreach (var payload in schemaPayloads.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var refsInPayload = ExtractSchemaRefsFromPayload(payload);
            if (refsInPayload.Count <= 1)
            {
                continue;
            }

            var refList = refsInPayload.ToList();
            for (int i = 0; i < refList.Count; i++)
            {
                if (!graph.TryGetValue(refList[i], out var edges))
                {
                    edges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    graph[refList[i]] = edges;
                }

                for (int j = 0; j < refList.Count; j++)
                {
                    if (i != j)
                    {
                        edges.Add(refList[j]);
                    }
                }
            }
        }

        return graph;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, HashSet<string>> ComputeTransitiveClosure(
        IReadOnlyDictionary<string, HashSet<string>> directReferences)
    {
        if (directReferences == null || directReferences.Count == 0)
        {
            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var closure = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in directReferences)
        {
            closure[kvp.Key] = new HashSet<string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        // Warshall's algorithm for transitive closure.
        var allNodes = closure.Keys.ToList();
        foreach (var k in allNodes)
        {
            foreach (var i in allNodes)
            {
                if (!closure.TryGetValue(i, out var iEdges) || !iEdges.Contains(k))
                {
                    continue;
                }

                if (!closure.TryGetValue(k, out var kEdges))
                {
                    continue;
                }

                foreach (var j in kEdges)
                {
                    if (!string.Equals(i, j, StringComparison.OrdinalIgnoreCase))
                    {
                        iEdges.Add(j);
                    }
                }
            }
        }

        return closure;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DependencyEdge> FindTransitiveSchemaDependencies(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationParameterSchemaRefs,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationResponseSchemaRefs,
        IReadOnlyDictionary<string, HashSet<string>> transitiveSchemaGraph)
    {
        if (operationParameterSchemaRefs == null || operationResponseSchemaRefs == null
            || transitiveSchemaGraph == null || transitiveSchemaGraph.Count == 0)
        {
            return Array.Empty<DependencyEdge>();
        }

        // Build reverse map: schema name → operations that PRODUCE it (have it in response).
        var producersBySchemaName = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (operationId, responseRefs) in operationResponseSchemaRefs)
        {
            foreach (var schemaRef in responseRefs)
            {
                if (!producersBySchemaName.TryGetValue(schemaRef, out var producers))
                {
                    producers = new HashSet<Guid>();
                    producersBySchemaName[schemaRef] = producers;
                }

                producers.Add(operationId);
            }
        }

        var edges = new List<DependencyEdge>();
        var seenPairs = new HashSet<(Guid, Guid)>();

        foreach (var (consumerId, paramRefs) in operationParameterSchemaRefs)
        {
            foreach (var paramRef in paramRefs)
            {
                // Get transitive closure of this schema ref.
                if (!transitiveSchemaGraph.TryGetValue(paramRef, out var transitiveRefs))
                {
                    continue;
                }

                foreach (var transitiveRef in transitiveRefs)
                {
                    if (!producersBySchemaName.TryGetValue(transitiveRef, out var producers))
                    {
                        continue;
                    }

                    foreach (var producerId in producers)
                    {
                        if (producerId == consumerId)
                        {
                            continue;
                        }

                        var pair = (consumerId, producerId);
                        if (!seenPairs.Add(pair))
                        {
                            continue;
                        }

                        edges.Add(new DependencyEdge
                        {
                            SourceOperationId = consumerId,
                            TargetOperationId = producerId,
                            Type = DependencyEdgeType.SchemaSchema,
                            Reason = $"Parameter schema '{paramRef}' transitively references '{transitiveRef}' produced by target operation.",
                            Confidence = 0.85,
                        });
                    }
                }
            }
        }

        return edges;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DependencyEdge> FindFuzzySchemaNameDependencies(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationParameterSchemaRefs,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> operationResponseSchemaRefs)
    {
        if (operationParameterSchemaRefs == null || operationResponseSchemaRefs == null)
        {
            return Array.Empty<DependencyEdge>();
        }

        // Build maps: base name → operations that consume/produce schemas with that base.
        var consumersByBaseName = new Dictionary<string, HashSet<(Guid OperationId, string OriginalName)>>(StringComparer.OrdinalIgnoreCase);
        var producersByBaseName = new Dictionary<string, HashSet<(Guid OperationId, string OriginalName)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (opId, refs) in operationParameterSchemaRefs)
        {
            foreach (var schemaRef in refs)
            {
                var baseName = ExtractSchemaBaseName(schemaRef);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    continue;
                }

                if (!consumersByBaseName.TryGetValue(baseName, out var consumers))
                {
                    consumers = new HashSet<(Guid, string)>();
                    consumersByBaseName[baseName] = consumers;
                }

                consumers.Add((opId, schemaRef));
            }
        }

        foreach (var (opId, refs) in operationResponseSchemaRefs)
        {
            foreach (var schemaRef in refs)
            {
                var baseName = ExtractSchemaBaseName(schemaRef);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    continue;
                }

                if (!producersByBaseName.TryGetValue(baseName, out var producers))
                {
                    producers = new HashSet<(Guid, string)>();
                    producersByBaseName[baseName] = producers;
                }

                producers.Add((opId, schemaRef));
            }
        }

        // Match consumers to producers by base name.
        var edges = new List<DependencyEdge>();
        var seenPairs = new HashSet<(Guid, Guid)>();

        foreach (var (baseName, consumers) in consumersByBaseName)
        {
            if (!producersByBaseName.TryGetValue(baseName, out var producers))
            {
                continue;
            }

            foreach (var (consumerId, consumerSchema) in consumers)
            {
                foreach (var (producerId, producerSchema) in producers)
                {
                    if (consumerId == producerId)
                    {
                        continue;
                    }

                    // Skip if exact match (already caught by Rule 2 / OperationSchema).
                    if (string.Equals(consumerSchema, producerSchema, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var pair = (consumerId, producerId);
                    if (!seenPairs.Add(pair))
                    {
                        continue;
                    }

                    edges.Add(new DependencyEdge
                    {
                        SourceOperationId = consumerId,
                        TargetOperationId = producerId,
                        Type = DependencyEdgeType.SchemaSchema,
                        Reason = $"Parameter schema '{consumerSchema}' and response schema '{producerSchema}' share base name '{baseName}' (fuzzy match).",
                        Confidence = 0.65,
                    });
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Extract all $ref schema names from a JSON payload.
    /// </summary>
    internal static HashSet<string> ExtractSchemaRefsFromPayload(string payload)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return refs;
        }

        foreach (Match match in SchemaRefRegex.Matches(payload))
        {
            var name = match.Groups["name"].Value?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                refs.Add(name);
            }
        }

        return refs;
    }

    /// <summary>
    /// Extract the meaningful base name from a schema name by stripping common prefixes/suffixes.
    /// "CreateUserRequest" → "User", "UserResponse" → "User", "OrderItemDto" → "OrderItem".
    /// </summary>
    internal static string ExtractSchemaBaseName(string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        var name = schemaName.Trim();

        // Strip suffixes (longest first for greedy match).
        var sortedSuffixes = SchemaNameSuffixes
            .OrderByDescending(s => s.Length)
            .ToList();

        foreach (var suffix in sortedSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        // Strip prefixes.
        var sortedPrefixes = SchemaNamePrefixes
            .OrderByDescending(s => s.Length)
            .ToList();

        foreach (var prefix in sortedPrefixes)
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        // Must be at least 2 chars to be meaningful.
        return name.Length >= 2 ? name : null;
    }

    /// <summary>
    /// Check if a payload is a bare single $ref (e.g., {"$ref": "#/components/schemas/User"}).
    /// </summary>
    private static bool IsBareSingleRef(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
               && trimmed.Contains("\"$ref\"", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("\"properties\"", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("\"items\"", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("\"allOf\"", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("\"anyOf\"", StringComparison.OrdinalIgnoreCase)
               && !trimmed.Contains("\"oneOf\"", StringComparison.OrdinalIgnoreCase);
    }
}
