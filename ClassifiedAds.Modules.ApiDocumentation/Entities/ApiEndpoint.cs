using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Individual API endpoint definition.
/// </summary>
public class ApiEndpoint : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// API specification this endpoint belongs to.
    /// </summary>
    public Guid ApiSpecId { get; set; }

    /// <summary>
    /// HTTP method: GET, POST, PUT, DELETE, PATCH.
    /// </summary>
    public HttpMethod HttpMethod { get; set; }

    /// <summary>
    /// Endpoint path (e.g., /api/users/{id}).
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Operation ID from OpenAPI spec.
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// Short summary of the endpoint.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Detailed description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Tags for categorization (stored as JSON array).
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Whether this endpoint is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; }

    // Navigation properties
    public ApiSpecification ApiSpecification { get; set; }
    public ICollection<EndpointParameter> Parameters { get; set; } = new List<EndpointParameter>();
    public ICollection<EndpointResponse> Responses { get; set; } = new List<EndpointResponse>();
    public ICollection<EndpointSecurityReq> SecurityRequirements { get; set; } = new List<EndpointSecurityReq>();
}

public enum HttpMethod
{
    GET = 0,
    POST = 1,
    PUT = 2,
    DELETE = 3,
    PATCH = 4,
    HEAD = 5,
    OPTIONS = 6
}
