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
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            SetIfMissing("Caching__Distributed__Provider", "Redis");
            SetIfMissing("Caching__Distributed__Redis__Configuration", redisUrl);
            return;
        }

        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");

        if (string.IsNullOrWhiteSpace(redisHost) || string.IsNullOrWhiteSpace(redisPort))
        {
            return;
        }

        SetIfMissing("Caching__Distributed__Provider", "Redis");
        SetIfMissing("Caching__Distributed__Redis__Configuration", $"{redisHost}:{redisPort}");
    }

    private static void SetIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
