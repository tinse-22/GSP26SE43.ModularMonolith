using ClassifiedAds.IntegrationTests.Infrastructure;
using System.Net;

namespace ClassifiedAds.IntegrationTests.Smoke;

/// <summary>
/// Smoke tests to verify the application can start and respond to basic requests.
/// These tests are essential for CI/CD gates to catch obvious configuration issues.
/// </summary>
[Collection("IntegrationTests")]
public class ApplicationSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ApplicationSmokeTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory(_dbFixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Smoke Test 1: Verify the host starts successfully.
    /// This is the most basic test - if the application can't bootstrap, nothing else will work.
    /// </summary>
    [Fact]
    public void Host_ShouldStartSuccessfully_WhenBootstrapped()
    {
        // Arrange - Factory and client are already created in InitializeAsync

        // Act - Just verify the client is created with a valid base address
        var baseAddress = _client.BaseAddress;

        // Assert
        baseAddress.Should().NotBeNull("WebApplicationFactory should create client with valid base address");
        _factory.Should().NotBeNull("Factory should be created successfully");
    }

    /// <summary>
    /// Smoke Test 2: Verify the health/swagger endpoint is accessible.
    /// Uses Swagger root endpoint since the app doesn't have a dedicated /health endpoint.
    /// Note: Swagger is configured at RoutePrefix = string.Empty (root path shows Swagger UI).
    /// </summary>
    [Fact]
    public async Task SwaggerEndpoint_ShouldReturnSuccess_WhenCalled()
    {
        // Arrange
        // The application serves Swagger at the root path (RoutePrefix = string.Empty in Program.cs)

        // Act
        var response = await _client.GetAsync("/swagger/ClassifiedAds/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Swagger JSON endpoint should be accessible and return 200 OK");
    }

    /// <summary>
    /// Smoke Test 3: Verify protected endpoints require authentication.
    /// Tests that the [Authorize] attribute on FilesController properly enforces auth.
    /// Without valid credentials, the endpoint should return 401 Unauthorized.
    ///
    /// Endpoint tested: GET /api/files
    /// Controller: ClassifiedAds.Modules.Storage.Controllers.FilesController
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_ShouldReturn401_WhenCalledWithoutAuth()
    {
        // Arrange
        // Add header to skip test authentication handler (simulate unauthenticated request)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/files");
        request.Headers.Add("X-Skip-Auth", "true");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Protected endpoint should return 401 Unauthorized when authentication fails. " +
            "Endpoint tested: GET /api/files (FilesController with [Authorize] attribute)");
    }
}
