using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class ExecutionEnvironmentRuntimeResolverTests
{
    private readonly Mock<IExecutionAuthConfigService> _authConfigServiceMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly ExecutionEnvironmentRuntimeResolver _resolver;

    public ExecutionEnvironmentRuntimeResolverTests()
    {
        _authConfigServiceMock = new Mock<IExecutionAuthConfigService>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _resolver = new ExecutionEnvironmentRuntimeResolver(
            _httpClientFactoryMock.Object,
            _authConfigServiceMock.Object,
            new Mock<ILogger<ExecutionEnvironmentRuntimeResolver>>().Object);
    }

    [Fact]
    public async Task ResolveAsync_OAuth2_Should_RequestTokenOnce()
    {
        // Arrange
        var callCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            Interlocked.Increment(ref callCount);
            var json = JsonSerializer.Serialize(new { access_token = "oauth-token-123" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        });

        var client = new HttpClient(handler);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.OAuth2ClientCredentials,
            TokenUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scopes = new[] { "read", "write" },
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        callCount.Should().Be(1);
        resolved.DefaultHeaders.Should().ContainKey("Authorization");
        resolved.DefaultHeaders["Authorization"].Should().Be("Bearer oauth-token-123");
    }

    [Fact]
    public async Task ResolveAsync_BearerToken_Should_SetAuthorizationHeader()
    {
        // Arrange
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            Token = "my-bearer-token",
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.DefaultHeaders.Should().ContainKey("Authorization");
        resolved.DefaultHeaders["Authorization"].Should().Be("Bearer my-bearer-token");
    }

    [Fact]
    public async Task ResolveAsync_Basic_Should_SetBase64AuthorizationHeader()
    {
        // Arrange
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.Basic,
            Username = "admin",
            Password = "secret123",
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret123"));
        resolved.DefaultHeaders.Should().ContainKey("Authorization");
        resolved.DefaultHeaders["Authorization"].Should().Be($"Basic {expectedCredentials}");
    }

    [Fact]
    public async Task ResolveAsync_ApiKey_Header_Should_AddCustomHeader()
    {
        // Arrange
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.ApiKey,
            ApiKeyName = "X-API-Key",
            ApiKeyValue = "key-12345",
            ApiKeyLocation = ApiKeyLocation.Header,
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.DefaultHeaders.Should().ContainKey("X-API-Key");
        resolved.DefaultHeaders["X-API-Key"].Should().Be("key-12345");
    }

    [Fact]
    public async Task ResolveAsync_ApiKey_Query_Should_AddQueryParam()
    {
        // Arrange
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.ApiKey,
            ApiKeyName = "api_key",
            ApiKeyValue = "key-12345",
            ApiKeyLocation = ApiKeyLocation.Query,
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.DefaultQueryParams.Should().ContainKey("api_key");
        resolved.DefaultQueryParams["api_key"].Should().Be("key-12345");
        resolved.DefaultHeaders.Should().NotContainKey("api_key");
    }

    [Fact]
    public async Task ResolveAsync_None_Should_NotAddAuthHeaders()
    {
        // Arrange
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.None,
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.DefaultHeaders.Should().NotContainKey("Authorization");
        resolved.DefaultQueryParams.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_Should_DeserializeVariablesAndHeaders()
    {
        // Arrange
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>()))
            .Returns((ExecutionAuthConfigModel)null);

        var environment = CreateEnvironment(
            variables: JsonSerializer.Serialize(new Dictionary<string, string> { ["baseUrl"] = "https://api.test.com", ["version"] = "v2" }),
            headers: JsonSerializer.Serialize(new Dictionary<string, string> { ["Accept"] = "application/json", ["X-Custom"] = "value" }));

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.Variables.Count.Should().BeGreaterThanOrEqualTo(2);
        resolved.Variables["baseUrl"].Should().Be("https://api.test.com");
        resolved.Variables["version"].Should().Be("v2");
        resolved.DefaultHeaders.Should().ContainKey("Accept");
        resolved.DefaultHeaders["Accept"].Should().Be("application/json");
        resolved.DefaultHeaders["X-Custom"].Should().Be("value");
    }

    [Fact]
    public async Task ResolveAsync_Should_AddBuiltInRunVariables_WhenNotConfigured()
    {
        // Arrange
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>()))
            .Returns((ExecutionAuthConfigModel)null);

        var environment = CreateEnvironment();

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.Variables.Should().ContainKey("runId");
        resolved.Variables.Should().ContainKey("runSuffix");
        resolved.Variables.Should().ContainKey("runTimestamp");
        resolved.Variables.Should().ContainKey("runUniqueEmail");
        resolved.Variables.Should().ContainKey("testEmail");
        resolved.Variables.Should().ContainKey("runUniquePassword");
        resolved.Variables.Should().ContainKey("testPassword");
        resolved.Variables["testEmail"].Should().Be(resolved.Variables["runUniqueEmail"]);
        resolved.Variables["testPassword"].Should().Be(resolved.Variables["runUniquePassword"]);
        resolved.Variables["runUniqueEmail"].Should().MatchRegex(@"^testrun_\d{14}_[a-z0-9]{8}@example\.com$");
    }

    [Fact]
    public async Task ResolveAsync_Should_NotOverwriteConfiguredTestIdentityVariables()
    {
        // Arrange
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>()))
            .Returns((ExecutionAuthConfigModel)null);

        var environment = CreateEnvironment(
            variables: JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["testEmail"] = "qa-team@company.local",
                ["testPassword"] = "AlreadyConfigured!123",
            }));

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert
        resolved.Variables["testEmail"].Should().Be("qa-team@company.local");
        resolved.Variables["testPassword"].Should().Be("AlreadyConfigured!123");
        resolved.Variables.Should().ContainKey("runUniqueEmail");
        resolved.Variables.Should().ContainKey("runUniquePassword");
    }

    [Fact]
    public async Task ResolveAsync_RequestLevelAuth_Should_NotOverwriteExistingAuthHeader()
    {
        // Arrange — environment already has Authorization header in its Headers JSON
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            Token = "env-token",
        };
        _authConfigServiceMock.Setup(x => x.DeserializeAuthConfig(It.IsAny<string>())).Returns(authConfig);

        var environment = CreateEnvironment(
            headers: JsonSerializer.Serialize(new Dictionary<string, string> { ["Authorization"] = "Bearer explicit-override" }));

        // Act
        var resolved = await _resolver.ResolveAsync(environment);

        // Assert — ContainsKey guard means the env-token is NOT injected; explicit header wins
        resolved.DefaultHeaders["Authorization"].Should().Be("Bearer explicit-override");
    }

    #region Helpers

    private static ExecutionEnvironment CreateEnvironment(
        string variables = null,
        string headers = null,
        string authConfig = "{}")
    {
        return new ExecutionEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Name = "Test Environment",
            BaseUrl = "https://api.example.com",
            Variables = variables,
            Headers = headers,
            AuthConfig = authConfig,
        };
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    #endregion
}
