using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.Subscription;

public class PayOsOptionsTests
{
    [Fact]
    public void Bind_WhenChecksumKeyProvided_ShouldPopulateSecretKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Subscription:PayOS:ChecksumKey"] = "checksum-value",
            })
            .Build();

        var options = new SubscriptionModuleOptions();

        configuration.GetSection("Modules:Subscription").Bind(options);

        options.PayOS.SecretKey.Should().Be("checksum-value");
        options.PayOS.ChecksumKey.Should().Be("checksum-value");
    }

    [Fact]
    public void Bind_WhenLegacySecretKeyProvided_ShouldRemainSupported()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Subscription:PayOS:SecretKey"] = "legacy-secret-value",
            })
            .Build();

        var options = new SubscriptionModuleOptions();

        configuration.GetSection("Modules:Subscription").Bind(options);

        options.PayOS.SecretKey.Should().Be("legacy-secret-value");
        options.PayOS.ChecksumKey.Should().Be("legacy-secret-value");
    }
}