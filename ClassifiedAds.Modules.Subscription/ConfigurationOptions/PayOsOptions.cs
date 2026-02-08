namespace ClassifiedAds.Modules.Subscription.ConfigurationOptions;

public class PayOsOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";

    public string ReturnUrl { get; set; } = string.Empty;

    public string WebhookUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; }

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public int IntentExpirationMinutes { get; set; } = 15;
}