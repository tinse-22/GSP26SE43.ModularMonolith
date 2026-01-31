using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Security scheme definition for an API specification.
/// </summary>
public class SecurityScheme : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// API specification this scheme belongs to.
    /// </summary>
    public Guid ApiSpecId { get; set; }

    /// <summary>
    /// Scheme name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Scheme type: http, apiKey, oauth2, openIdConnect.
    /// </summary>
    public SchemeType Type { get; set; }

    /// <summary>
    /// HTTP auth scheme (bearer, basic).
    /// </summary>
    public string Scheme { get; set; }

    /// <summary>
    /// Bearer token format (e.g., JWT).
    /// </summary>
    public string BearerFormat { get; set; }

    /// <summary>
    /// Location for API key: header, query, cookie.
    /// </summary>
    public ApiKeyLocation? In { get; set; }

    /// <summary>
    /// Parameter name for API key.
    /// </summary>
    public string ParameterName { get; set; }

    /// <summary>
    /// Additional configuration as JSON (OAuth2 flows, etc.).
    /// </summary>
    public string Configuration { get; set; }

    // Navigation properties
    public ApiSpecification ApiSpecification { get; set; }
}

public enum SchemeType
{
    Http = 0,
    ApiKey = 1,
    OAuth2 = 2,
    OpenIdConnect = 3
}

public enum ApiKeyLocation
{
    Header = 0,
    Query = 1,
    Cookie = 2
}
