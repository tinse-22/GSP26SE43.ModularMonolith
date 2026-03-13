namespace ClassifiedAds.Modules.Identity.ConfigurationOptions;

public class IdentityServerOptions
{
    public string Authority { get; set; }

    public string ApiName { get; set; }

    public bool RequireHttpsMetadata { get; set; }

    /// <summary>
    /// Frontend application URL for generating email confirmation links
    /// </summary>
    public string FrontendUrl { get; set; }
}
