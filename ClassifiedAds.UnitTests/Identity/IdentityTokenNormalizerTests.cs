using ClassifiedAds.Modules.Identity.Helpers;
using Xunit;
using FluentAssertions;

namespace ClassifiedAds.UnitTests.Identity;

public class IdentityTokenNormalizerTests
{
    [Fact]
    public void Normalize_Should_KeepRawTokenUnchanged()
    {
        // Arrange
        const string token = "CfDJ8DtLXWw+abc/def==";

        // Act
        var result = IdentityTokenNormalizer.Normalize(token);

        // Assert
        result.Should().Be(token);
    }

    [Fact]
    public void Normalize_Should_DecodeUrlEncodedToken()
    {
        // Arrange
        const string token = "CfDJ8DtLXWw%2Babc%2Fdef%3D%3D";

        // Act
        var result = IdentityTokenNormalizer.Normalize(token);

        // Assert
        result.Should().Be("CfDJ8DtLXWw+abc/def==");
    }

    [Fact]
    public void Normalize_Should_DecodeDoubleEncodedToken()
    {
        // Arrange
        const string token = "CfDJ8DtLXWw%252Babc%252Fdef%253D%253D";

        // Act
        var result = IdentityTokenNormalizer.Normalize(token);

        // Assert
        result.Should().Be("CfDJ8DtLXWw+abc/def==");
    }

    [Fact]
    public void Normalize_Should_ConvertSpacesToPlus()
    {
        // Arrange
        const string token = "CfDJ8DtLXWw abc/def==";

        // Act
        var result = IdentityTokenNormalizer.Normalize(token);

        // Assert
        result.Should().Be("CfDJ8DtLXWw+abc/def==");
    }

    [Fact]
    public void Normalize_Should_TrimToken()
    {
        // Arrange
        const string token = "  CfDJ8DtLXWw%2Babc%2Fdef%3D%3D  ";

        // Act
        var result = IdentityTokenNormalizer.Normalize(token);

        // Assert
        result.Should().Be("CfDJ8DtLXWw+abc/def==");
    }
}
