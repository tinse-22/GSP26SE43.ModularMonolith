using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Security requirement for an API endpoint.
/// </summary>
public class EndpointSecurityReq : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Endpoint this security requirement belongs to.
    /// </summary>
    public Guid EndpointId { get; set; }

    /// <summary>
    /// Security type: Bearer, ApiKey, OAuth2, Basic.
    /// </summary>
    public SecurityType SecurityType { get; set; }

    /// <summary>
    /// Reference to the security scheme name.
    /// </summary>
    public string SchemeName { get; set; }

    /// <summary>
    /// Required OAuth2 scopes (stored as JSON array).
    /// </summary>
    public string Scopes { get; set; }

    // Navigation properties
    public ApiEndpoint Endpoint { get; set; }
}

public enum SecurityType
{
    Bearer = 0,
    ApiKey = 1,
    OAuth2 = 2,
    Basic = 3,
    OpenIdConnect = 4
}
