using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.ApiDocumentation.DTOs;

public class ApiEndpointMetadataDto
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public bool IsAuthRelated { get; set; }

    public IReadOnlyCollection<Guid> DependsOnEndpointIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Schema $ref names extracted from this operation's parameter schemas.
    /// Used by SchemaRelationshipAnalyzer (KAT paper) for Schema-Schema dependency analysis.
    /// Optional: only populated when advanced dependency analysis is needed.
    /// </summary>
    public IReadOnlyCollection<string> ParameterSchemaRefs { get; set; }

    /// <summary>
    /// Schema $ref names extracted from this operation's success response schemas.
    /// Used by SchemaRelationshipAnalyzer (KAT paper) for Schema-Schema dependency analysis.
    /// Optional: only populated when advanced dependency analysis is needed.
    /// </summary>
    public IReadOnlyCollection<string> ResponseSchemaRefs { get; set; }

    /// <summary>
    /// Raw JSON schema payloads from this operation's parameters.
    /// Used by SchemaRelationshipAnalyzer for transitive $ref chain resolution.
    /// Optional: only populated when advanced dependency analysis is needed.
    /// </summary>
    public IReadOnlyCollection<string> ParameterSchemaPayloads { get; set; }

    /// <summary>
    /// Raw JSON schema payloads from this operation's success responses.
    /// Used by SchemaRelationshipAnalyzer for transitive $ref chain resolution.
    /// Optional: only populated when advanced dependency analysis is needed.
    /// </summary>
    public IReadOnlyCollection<string> ResponseSchemaPayloads { get; set; }
}
