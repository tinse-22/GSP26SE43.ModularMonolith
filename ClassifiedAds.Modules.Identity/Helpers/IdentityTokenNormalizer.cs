using System;
using System.Web;

namespace ClassifiedAds.Modules.Identity.Helpers;

public static class IdentityTokenNormalizer
{
    public static string Normalize(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        var normalized = token.Trim();

        // Support tokens that were URL-encoded once (or accidentally encoded multiple times).
        for (var i = 0; i < 3 && normalized.Contains('%'); i++)
        {
            var decoded = HttpUtility.UrlDecode(normalized);
            if (string.IsNullOrEmpty(decoded) || string.Equals(decoded, normalized, StringComparison.Ordinal))
            {
                break;
            }

            normalized = decoded;
        }

        // Recover tokens where '+' became space after query/form parsing.
        if (normalized.Contains(' '))
        {
            normalized = normalized.Replace(' ', '+');
        }

        return normalized;
    }
}
