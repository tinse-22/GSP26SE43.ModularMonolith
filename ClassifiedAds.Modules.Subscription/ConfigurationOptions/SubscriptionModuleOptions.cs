namespace ClassifiedAds.Modules.Subscription.ConfigurationOptions;

public class SubscriptionModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    public PayOsOptions PayOS { get; set; } = new PayOsOptions();
}
