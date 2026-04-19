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

    /// <summary>
    /// When true, enables <c>POST /api/auth/dev-confirm-email</c> which confirms
    /// a user's email without a token. Intended for automated test environments only.
    /// Must remain <c>false</c> in production.
    /// </summary>
    public bool AllowDevEmailConfirmation { get; set; }
}
