namespace ClassifiedAds.Modules.Identity.ConfigurationOptions;

public class IdentityModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    public IdentityServerOptions IdentityServer { get; set; }

    public IdentityProvidersOptions Providers { get; set; }

    /// <summary>
    /// Enables local development seed/bootstrap logic for Identity roles and users.
    /// Keep this disabled by default so production hosts never run dev bootstrap data.
    /// </summary>
    public bool BootstrapDevelopmentData { get; set; }

    /// <summary>
    /// JWT configuration for token generation.
    /// </summary>
    public JwtOptions Jwt { get; set; }
}
