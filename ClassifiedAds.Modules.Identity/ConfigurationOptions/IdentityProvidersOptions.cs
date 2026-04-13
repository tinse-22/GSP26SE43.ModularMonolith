using ClassifiedAds.Modules.Identity.IdentityProviders.Auth0;
using ClassifiedAds.Modules.Identity.IdentityProviders.Azure;
using ClassifiedAds.Modules.Identity.IdentityProviders.Google;

namespace ClassifiedAds.Modules.Identity.ConfigurationOptions;

public class IdentityProvidersOptions
{
    public Auth0Options Auth0 { get; set; }

    public AzureAdB2COptions AzureActiveDirectoryB2C { get; set; }

    public GoogleOptions Google { get; set; }
}
