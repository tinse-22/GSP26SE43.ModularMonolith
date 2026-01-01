using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace ClassifiedAds.IntegrationTests.Infrastructure;

/// <summary>
/// Shared fixture that manages PostgreSQL container lifecycle for all integration tests.
/// Implements IAsyncLifetime for proper async setup/teardown with xUnit.
/// </summary>
public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlContainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("ClassifiedAds_Test")
            .WithUsername("postgres")
            .WithPassword("postgres123!@#")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();
    }

    /// <summary>
    /// Gets the connection string to the running PostgreSQL container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Starts the PostgreSQL container.
    /// Called automatically by xUnit before any tests in the collection run.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the PostgreSQL container.
    /// Called automatically by xUnit after all tests in the collection complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
