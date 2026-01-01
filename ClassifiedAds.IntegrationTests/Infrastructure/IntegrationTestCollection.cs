namespace ClassifiedAds.IntegrationTests.Infrastructure;

/// <summary>
/// Collection definition for integration tests that share the PostgreSQL container.
/// All test classes decorated with [Collection("IntegrationTests")] will share
/// the same PostgreSqlContainerFixture instance.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
