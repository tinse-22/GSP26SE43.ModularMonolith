using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;

public class N8nIntegrationOptions
{
    /// <summary>
    /// Base URL of the n8n webhook endpoint (e.g. "https://tinem46.app.n8n.cloud/webhook/").
    /// All webhook paths are appended to this base URL.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key sent via the x-api-key header for n8n webhook authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds. Default: 30.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Named webhook paths appended to BaseUrl.
    /// Key = logical name (e.g. "DotnetIntegration"), Value = relative path (e.g. "dotnet-integration").
    /// This allows adding new n8n workflows without code changes.
    /// </summary>
    public Dictionary<string, string> Webhooks { get; set; } = new();

    /// <summary>
    /// Base URL of this BE instance used to build callback URLs that n8n will POST results back to.
    /// Example: "http://localhost:44312"
    /// </summary>
    public string BeBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret that BE includes in payloads to n8n and validates on incoming n8n callbacks
    /// via the "x-callback-api-key" request header.
    /// </summary>
    public string CallbackApiKey { get; set; } = string.Empty;
}
