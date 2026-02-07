using CryptographyHelper.Certificates;

namespace ClassifiedAds.WebAPI.ConfigurationOptions;

public class AuthenticationOptions
{
    public string Provider { get; set; } = "Jwt";

    public IdentityServerOptions IdentityServer { get; set; } = new();

    public JwtOptions Jwt { get; set; } = new();
}

public class IdentityServerOptions
{
    public string Authority { get; set; } = "https://localhost:44367";

    public string Audience { get; set; } = "ClassifiedAds.WebAPI";

    public bool RequireHttpsMetadata { get; set; }
}

public class JwtOptions
{
    public string IssuerUri { get; set; }

    public string Audience { get; set; }

    public CertificateOption TokenDecryptionCertificate { get; set; }

    public CertificateOption IssuerSigningCertificate { get; set; }
}
