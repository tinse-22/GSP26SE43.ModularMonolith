namespace ClassifiedAds.Modules.TestGeneration.Constants;

/// <summary>
/// Logical names for n8n webhooks referenced in N8nIntegration:Webhooks configuration.
/// </summary>
public static class N8nWebhookNames
{
    /// <summary>
    /// Webhook that orchestrates LLM calls to generate happy-path test cases
    /// from approved API order + endpoint metadata + ObservationConfirmation prompts.
    /// </summary>
    public const string GenerateHappyPath = "generate-happy-path";
}
