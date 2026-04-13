namespace ClassifiedAds.Modules.Identity.IdentityProviders.Google;

public class GoogleOptions
{
    public bool Enabled { get; set; }

    /// <summary>
    /// OAuth 2.0 Client ID from Google Cloud Console.
    /// </summary>
    public string ClientId { get; set; }
}
