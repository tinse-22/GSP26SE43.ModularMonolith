using Google.Apis.Auth;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.IdentityProviders.Google;

public class GoogleIdentityProvider
{
    private readonly GoogleOptions _options;

    public GoogleIdentityProvider(GoogleOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Validates a Google ID token and returns the payload.
    /// Throws <see cref="InvalidJwtException"/> if the token is invalid.
    /// </summary>
    public async Task<GoogleJsonWebSignature.Payload> VerifyIdTokenAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _options.ClientId },
        };

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}
