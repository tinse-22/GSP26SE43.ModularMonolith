using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class ExecutionAuthConfigModel
{
    public AuthType AuthType { get; set; } = AuthType.None;

    /// <summary>
    /// Custom header name for Bearer token (default: Authorization).
    /// </summary>
    public string HeaderName { get; set; }

    // BearerToken
    public string Token { get; set; }

    // Basic
    public string Username { get; set; }
    public string Password { get; set; }

    // ApiKey
    public string ApiKeyName { get; set; }
    public string ApiKeyValue { get; set; }
    public ApiKeyLocation ApiKeyLocation { get; set; } = ApiKeyLocation.Header;

    // OAuth2ClientCredentials
    public string TokenUrl { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string[] Scopes { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthType
{
    None = 0,
    BearerToken = 1,
    Basic = 2,
    ApiKey = 3,
    OAuth2ClientCredentials = 4,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiKeyLocation
{
    Header = 0,
    Query = 1,
}
