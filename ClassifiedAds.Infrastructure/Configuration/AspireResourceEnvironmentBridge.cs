using System;

namespace ClassifiedAds.Infrastructure.Configuration;

public static class AspireResourceEnvironmentBridge
{
    public static void Apply()
    {
        ApplyRabbitMq();
        ApplyRedis();
    }

    private static void ApplyRabbitMq()
    {
        var rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
        var rabbitMqPort = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        var rabbitMqUserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME");
        var rabbitMqPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");

        if (string.IsNullOrWhiteSpace(rabbitMqHost) || string.IsNullOrWhiteSpace(rabbitMqPort))
        {
            return;
        }

        SetIfMissing("Messaging__RabbitMQ__HostName", rabbitMqHost);
        SetIfMissing("Messaging__RabbitMQ__Port", rabbitMqPort);
        SetIfMissing("Messaging__RabbitMQ__UserName", rabbitMqUserName);
        SetIfMissing("Messaging__RabbitMQ__Password", rabbitMqPassword);
    }

    private static void ApplyRedis()
    {
        var redisConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            // Prefer Aspire-provided connection string (host/port/password/ssl) over any stale local value.
            Environment.SetEnvironmentVariable("Caching__Distributed__Redis__Configuration", redisConnectionString);
            return;
        }

        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            // Standalone hosts load REDIS_URL from .env; map it to the cache option consumed by AddDistributedRedisCache.
            Environment.SetEnvironmentVariable("Caching__Distributed__Redis__Configuration", redisUrl);
            return;
        }

        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");
        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        var redisSsl = Environment.GetEnvironmentVariable("REDIS_SSL");

        if (string.IsNullOrWhiteSpace(redisHost) || string.IsNullOrWhiteSpace(redisPort))
        {
            return;
        }

        var configuration = $"{redisHost}:{redisPort}";
        if (!string.IsNullOrWhiteSpace(redisPassword))
        {
            configuration += $",password={redisPassword}";
        }

        if (!string.IsNullOrWhiteSpace(redisSsl))
        {
            configuration += $",ssl={redisSsl}";
        }

        Environment.SetEnvironmentVariable("Caching__Distributed__Redis__Configuration", configuration);
    }

    private static void SetIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
