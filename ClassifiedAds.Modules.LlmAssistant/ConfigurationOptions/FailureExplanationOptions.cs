namespace ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;

public class FailureExplanationOptions
{
    public string Provider { get; set; } = "N8n";

    public string Model { get; set; } = "gpt-4.1-mini";

    public int TimeoutSeconds { get; set; } = 300;

    public int CacheTtlHours { get; set; } = 24;

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string WebhookPath { get; set; } = "explain-failure";
}
