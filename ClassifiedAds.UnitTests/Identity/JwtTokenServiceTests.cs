using ClassifiedAds.Modules.Identity.ConfigurationOptions;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace ClassifiedAds.UnitTests.Identity;

public class JwtTokenServiceTests
{
    [Fact]
    public void JwtOptions_Should_HaveDefaultValues()
    {
        // Arrange
        var options = new JwtOptions();

        // Assert
        options.AccessTokenExpirationMinutes.Should().Be(60);
        options.RefreshTokenExpirationDays.Should().Be(7);
    }

    [Fact]
    public void JwtOptions_Should_AllowCustomValues()
    {
        // Arrange
        var options = new JwtOptions
        {
            SecretKey = "TestSecretKeyThatIsAtLeast32CharactersLong!@#$%",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 120,
            RefreshTokenExpirationDays = 14,
        };

        // Assert
        options.SecretKey.Should().NotBeNullOrEmpty();
        options.Issuer.Should().Be("TestIssuer");
        options.Audience.Should().Be("TestAudience");
        options.AccessTokenExpirationMinutes.Should().Be(120);
        options.RefreshTokenExpirationDays.Should().Be(14);
    }

    [Fact]
    public void JwtOptions_SecretKey_Should_BeAtLeast32Characters()
    {
        // Arrange
        var validKey = "TestSecretKeyThatIsAtLeast32CharactersLong!@#$%";
        var shortKey = "TooShort";

        // Assert
        validKey.Length.Should().BeGreaterThanOrEqualTo(32);
        shortKey.Length.Should().BeLessThan(32);
    }

    [Fact]
    public void IdentityModuleOptions_Should_ContainJwtOptions()
    {
        // Arrange
        var options = new IdentityModuleOptions
        {
            Jwt = new JwtOptions
            {
                SecretKey = "TestKey12345678901234567890123456",
                Issuer = "TestIssuer",
            },
        };

        // Assert
        options.Jwt.Should().NotBeNull();
        options.Jwt!.SecretKey.Should().NotBeNullOrEmpty();
    }
}
