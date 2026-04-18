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

    /// <summary>
    /// Parameter names from this operation (path, query, body parameters).
    /// Used by SemanticTokenMatcher (SPDG paper) for semantic token dependency analysis.
    /// Optional: only populated when advanced dependency analysis is needed.
    /// </summary>
    public IReadOnlyCollection<string> ParameterNames { get; set; }

    /// <summary>
    /// Required path-parameter names for this operation.
    /// Used by generation/execution validation to guarantee route contract fidelity.
    /// </summary>
    public IReadOnlyCollection<string> RequiredPathParameterNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Required query-parameter names for this operation.
    /// Used by generation/execution validation to guarantee query contract fidelity.
    /// </summary>
    public IReadOnlyCollection<string> RequiredQueryParameterNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this operation requires a request body.
    /// Used by generation/execution validation to guarantee body contract fidelity.
    /// </summary>
    public bool HasRequiredRequestBody { get; set; }

    /// <summary>
    /// Location-aware parameter descriptors from the API spec.
    /// Used by prompt/context mappers to preserve true parameter semantics.
    /// </summary>
    public IReadOnlyCollection<ApiEndpointParameterDescriptorDto> Parameters { get; set; } = Array.Empty<ApiEndpointParameterDescriptorDto>();
}

public class ApiEndpointParameterDescriptorDto
{
    public string Name { get; set; }

    public string Location { get; set; }

    public bool IsRequired { get; set; }

    public string DataType { get; set; }

    public string Format { get; set; }

    public string Schema { get; set; }

    public string DefaultValue { get; set; }

    public string Examples { get; set; }
}
