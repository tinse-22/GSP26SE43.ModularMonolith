using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;

namespace ClassifiedAds.UnitTests.TestExecution;

public class ExecutionAuthConfigServiceTests
{
    private readonly ExecutionAuthConfigService _service = new();

    [Fact]
    public void MaskAuthConfig_Should_MaskSensitiveFields()
    {
        // Arrange
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            Token = "real-secret-token",
        };

        // Act
        var masked = _service.MaskAuthConfig(config);

        // Assert
        masked.Token.Should().Be("******");
        masked.AuthType.Should().Be(AuthType.BearerToken);
    }

    [Fact]
    public void MaskAuthConfig_Should_MaskBasicCredentials()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.Basic,
            Username = "user",
            Password = "secret",
        };

        var masked = _service.MaskAuthConfig(config);

        masked.Username.Should().Be("user"); // username not masked
        masked.Password.Should().Be("******");
    }

    [Fact]
    public void MaskAuthConfig_Should_MaskApiKeyValue()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.ApiKey,
            ApiKeyName = "X-API-Key",
            ApiKeyValue = "secret-key",
        };

        var masked = _service.MaskAuthConfig(config);

        masked.ApiKeyName.Should().Be("X-API-Key");
        masked.ApiKeyValue.Should().Be("******");
    }

    [Fact]
    public void MaskAuthConfig_Should_MaskOAuth2ClientSecret()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.OAuth2ClientCredentials,
            TokenUrl = "https://auth.example.com/token",
            ClientId = "my-client",
            ClientSecret = "super-secret",
        };

        var masked = _service.MaskAuthConfig(config);

        masked.TokenUrl.Should().Be("https://auth.example.com/token");
        masked.ClientId.Should().Be("my-client");
        masked.ClientSecret.Should().Be("******");
    }

    [Fact]
    public void MaskAuthConfig_Null_Should_ReturnNull()
    {
        var result = _service.MaskAuthConfig(null);
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateAuthConfig_BearerToken_Should_ThrowWhenTokenMissing()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            Token = "",
        };

        var act = () => _service.ValidateAuthConfig(config);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateAuthConfig_Basic_Should_ThrowWhenUsernameMissing()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.Basic,
            Username = "",
            Password = "pass",
        };

        var act = () => _service.ValidateAuthConfig(config);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ValidateAuthConfig_None_Should_NotThrow()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.None,
        };

        var act = () => _service.ValidateAuthConfig(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void SerializeDeserialize_Should_Roundtrip()
    {
        var config = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            Token = "test-token",
        };

        var json = _service.SerializeAuthConfig(config);
        var deserialized = _service.DeserializeAuthConfig(json);

        deserialized.AuthType.Should().Be(AuthType.BearerToken);
        deserialized.Token.Should().Be("test-token");
    }
}
