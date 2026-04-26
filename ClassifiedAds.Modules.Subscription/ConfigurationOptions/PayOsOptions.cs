namespace ClassifiedAds.Modules.Subscription.ConfigurationOptions;

public class PayOsOptions
{
    private string _secretKey = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string SecretKey
    {
        get => _secretKey;
        set => _secretKey = value ?? string.Empty;
    }

    public string ChecksumKey
    {
        get => _secretKey;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _secretKey = value;
            }
        }
    }

    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";

    public string ReturnUrl { get; set; } = string.Empty;

    public string WebhookUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; }

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public int IntentExpirationMinutes { get; set; } = 15;

    public int CheckoutReconcileIntervalSeconds { get; set; } = 30;

    public int CheckoutReconcileBatchSize { get; set; } = 50;

    public int CheckoutReconcileLookbackHours { get; set; } = 24;
}
