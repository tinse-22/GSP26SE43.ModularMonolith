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
    /// HTTP request timeout in seconds. Default: 600 (10 minutes) for LLM/webhook calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// Faster failover budget for synchronous LLM suggestion generation.
    /// If n8n cannot produce suggestions within this window, BE falls back to local synthesis.
    /// </summary>
    public int LlmSuggestionTimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Named webhook paths appended to BaseUrl.
    /// Key = logical name (e.g. "generate-test-cases-unified"), Value = relative path (e.g. "generate-test-cases-unified").
    /// This allows adding new n8n workflows without code changes.
    /// </summary>
    public Dictionary<string, string> Webhooks { get; set; } = new Dictionary<string, string>();

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

    /// <summary>
    /// When true, generation APIs use the unified test generation webhook (callback-based)
    /// instead of per-flow synchronous webhooks.
    /// </summary>
    public bool UseDotnetIntegrationWorkflowForGeneration { get; set; }

    /// <summary>
    /// Preferred LLM model for unified test-case generation. The n8n workflow reads this
    /// from the payload so runtime config can switch to a faster model without changing n8n nodes.
    /// </summary>
    public string GenerationModel { get; set; } = "gpt-4.1-mini";

    /// <summary>
    /// Upper bound for LLM output tokens in unified test-case generation.
    /// </summary>
    public int GenerationMaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Minimum token budget sent to n8n for small suites.
    /// </summary>
    public int GenerationMinOutputTokens { get; set; } = 2048;

    /// <summary>
    /// Additional token budget per endpoint, capped by <see cref="GenerationMaxOutputTokens"/>.
    /// </summary>
    public int GenerationOutputTokensPerEndpoint { get; set; } = 768;

    /// <summary>
    /// Maximum number of request/response schema payloads sent per endpoint and schema kind.
    /// </summary>
    public int GenerationMaxSchemaPayloadCountPerKind { get; set; } = 1;

    /// <summary>
    /// Maximum characters kept for each schema payload sent to n8n.
    /// </summary>
    public int GenerationMaxSchemaPayloadLength { get; set; } = 800;

    /// <summary>
    /// Maximum characters kept for each endpoint prompt fragment sent to n8n.
    /// </summary>
    public int GenerationMaxPromptLength { get; set; } = 1200;

    /// <summary>
    /// Maximum characters kept for business context and global rules in the generation payload.
    /// </summary>
    public int GenerationMaxBusinessContextLength { get; set; } = 700;

    /// <summary>
    /// Maximum SRS requirements included in a single generation payload.
    /// </summary>
    public int GenerationMaxSrsRequirementCount { get; set; } = 15;

    /// <summary>
    /// Maximum characters kept for each text field of an SRS requirement.
    /// </summary>
    public int GenerationMaxSrsFieldLength { get; set; } = 500;
}
