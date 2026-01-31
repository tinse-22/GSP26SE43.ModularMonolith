using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Parameter definition for an API endpoint.
/// </summary>
public class EndpointParameter : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Endpoint this parameter belongs to.
    /// </summary>
    public Guid EndpointId { get; set; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Parameter location: Path, Query, Header, Body.
    /// </summary>
    public ParameterLocation Location { get; set; }

    /// <summary>
    /// Data type (string, integer, boolean, etc.).
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// Format (date-time, email, uuid, etc.).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Whether this parameter is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Default value if any.
    /// </summary>
    public string DefaultValue { get; set; }

    /// <summary>
    /// JSON Schema for validation.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Example values as JSON.
    /// </summary>
    public string Examples { get; set; }

    // Navigation properties
    public ApiEndpoint Endpoint { get; set; }
}

public enum ParameterLocation
{
    Path = 0,
    Query = 1,
    Header = 2,
    Body = 3,
    Cookie = 4
}
