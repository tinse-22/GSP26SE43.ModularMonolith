using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Response definition for an API endpoint.
/// </summary>
public class EndpointResponse : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Endpoint this response belongs to.
    /// </summary>
    public Guid EndpointId { get; set; }

    /// <summary>
    /// HTTP status code (200, 400, 401, 404, 500, etc.).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Response JSON Schema.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Example responses as JSON.
    /// </summary>
    public string Examples { get; set; }

    /// <summary>
    /// Response headers as JSON.
    /// </summary>
    public string Headers { get; set; }

    // Navigation properties
    public ApiEndpoint Endpoint { get; set; }
}
