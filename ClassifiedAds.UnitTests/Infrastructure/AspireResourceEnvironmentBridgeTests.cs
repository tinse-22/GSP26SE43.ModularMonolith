using ClassifiedAds.Infrastructure.Configuration;

namespace ClassifiedAds.UnitTests.Infrastructure;

[CollectionDefinition(EnvironmentVariableCollection.Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection : ICollectionFixture<EnvironmentVariableFixture>
{
    public const string Name = "Environment variables";
}

public sealed class EnvironmentVariableFixture
{
}

[Collection(EnvironmentVariableCollection.Name)]
public class AspireResourceEnvironmentBridgeTests : IDisposable
{
    private static readonly string[] EnvironmentKeys =
    [
        "ConnectionStrings__redis",
        "REDIS_URL",
        "REDIS_HOST",
        "REDIS_PORT",
        "REDIS_PASSWORD",
        "REDIS_SSL",
        "Caching__Distributed__Redis__Configuration",
        "RABBITMQ_HOST",
        "RABBITMQ_PORT",
        "RABBITMQ_USERNAME",
        "RABBITMQ_PASSWORD",
        "Messaging__RabbitMQ__HostName",
        "Messaging__RabbitMQ__Port",
        "Messaging__RabbitMQ__UserName",
        "Messaging__RabbitMQ__Password",
    ];

    private readonly Dictionary<string, string?> _snapshot;

    public AspireResourceEnvironmentBridgeTests()
    {
        _snapshot = EnvironmentKeys.ToDictionary(x => x, Environment.GetEnvironmentVariable);

        foreach (var key in EnvironmentKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public void Dispose()
    {
        foreach (var item in _snapshot)
        {
            Environment.SetEnvironmentVariable(item.Key, item.Value);
        }
    }

    [Fact]
    public void Apply_WhenRedisUrlExists_ShouldMapItToDistributedRedisConfiguration()
    {
        Environment.SetEnvironmentVariable("REDIS_URL", "rediss://default:secret@redis.example.com:6379");

        AspireResourceEnvironmentBridge.Apply();

        Environment.GetEnvironmentVariable("Caching__Distributed__Redis__Configuration")
            .Should().Be("rediss://default:secret@redis.example.com:6379");
    }

    [Fact]
    public void Apply_WhenAspireRedisConnectionStringExists_ShouldPreferItOverRedisUrl()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__redis", "localhost:6380,password=aspire");
        Environment.SetEnvironmentVariable("REDIS_URL", "rediss://default:secret@redis.example.com:6379");

        AspireResourceEnvironmentBridge.Apply();

        Environment.GetEnvironmentVariable("Caching__Distributed__Redis__Configuration")
            .Should().Be("localhost:6380,password=aspire");
    }

    [Fact]
    public void Apply_WhenRedisUrlIsMissing_ShouldKeepHostPortFallback()
    {
        Environment.SetEnvironmentVariable("REDIS_HOST", "redis");
        Environment.SetEnvironmentVariable("REDIS_PORT", "6379");
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", "secret");
        Environment.SetEnvironmentVariable("REDIS_SSL", "true");

        AspireResourceEnvironmentBridge.Apply();

        Environment.GetEnvironmentVariable("Caching__Distributed__Redis__Configuration")
            .Should().Be("redis:6379,password=secret,ssl=true");
    }
}
