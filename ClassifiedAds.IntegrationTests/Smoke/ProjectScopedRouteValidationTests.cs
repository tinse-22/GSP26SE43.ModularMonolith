using ClassifiedAds.IntegrationTests.Infrastructure;
using System.Net;

namespace ClassifiedAds.IntegrationTests.Smoke;

[Collection("IntegrationTests")]
public class ProjectScopedRouteValidationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ProjectScopedRouteValidationTests(PostgreSqlContainerFixture dbFixture)
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

    [Theory]
    [InlineData("/api/projects/9bb92a1a-1a8d-4365-9c11-380a45b4440/test-suites")]
    [InlineData("/api/projects/9bb92a1a-1a8d-4365-9c11-380a45b4440/execution-environments")]
    public async Task ProjectScopedRoutes_ShouldStillMatch_WhenProjectIdIsMalformed(string path)
    {
        // A malformed project id should reach auth/model-binding instead of failing route matching with 404.
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Skip-Auth", "true");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
