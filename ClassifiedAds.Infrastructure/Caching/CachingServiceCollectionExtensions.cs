using System;
using System.Collections.Generic;
using ClassifiedAds.Infrastructure.Caching;

namespace Microsoft.Extensions.DependencyInjection;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddCaches(this IServiceCollection services, CachingOptions options = null)
    {
        services.AddMemoryCache(opt =>
        {
            opt.SizeLimit = options?.InMemory?.SizeLimit;
        });

        var distributedProvider = options?.Distributed?.Provider;

        if (distributedProvider == "InMemory")
        {
            services.AddDistributedMemoryCache(opt =>
            {
                opt.SizeLimit = options?.Distributed?.InMemory?.SizeLimit;
            });
        }
        else if (distributedProvider == "Redis")
        {
            services.AddDistributedRedisCache(opt =>
            {
                opt.Configuration = BuildRedisConfiguration(options.Distributed.Redis);
                opt.InstanceName = options.Distributed.Redis.InstanceName;
            });
        }

        services.AddHybridCache(options =>
        {
        });

        return services;
    }

    private static string BuildRedisConfiguration(RedisOptions options)
    {
        var configuration = options?.Configuration?.Trim();
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return configuration;
        }

        configuration = NormalizeRedisConfiguration(configuration);

        if (ContainsOption(configuration, "abortConnect") || ContainsOption(configuration, "abortOnConnectFail"))
        {
            return configuration;
        }

        return $"{configuration},abortConnect=false";
    }

    private static string NormalizeRedisConfiguration(string configuration)
    {
        if (!Uri.TryCreate(configuration, UriKind.Absolute, out var redisUri) ||
            (!string.Equals(redisUri.Scheme, "redis", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(redisUri.Scheme, "rediss", StringComparison.OrdinalIgnoreCase)))
        {
            return configuration;
        }

        var segments = new List<string>
        {
            $"{redisUri.Host}:{(redisUri.IsDefaultPort ? 6379 : redisUri.Port)}"
        };

        var password = ResolveRedisPassword(redisUri);
        if (!string.IsNullOrWhiteSpace(password))
        {
            segments.Add($"password={password}");
        }

        if (string.Equals(redisUri.Scheme, "rediss", StringComparison.OrdinalIgnoreCase))
        {
            segments.Add("ssl=true");
        }

        var database = ResolveRedisDatabase(redisUri);
        if (database.HasValue)
        {
            segments.Add($"defaultDatabase={database.Value}");
        }

        return string.Join(",", segments);
    }

    private static string ResolveRedisPassword(Uri redisUri)
    {
        if (string.IsNullOrWhiteSpace(redisUri.UserInfo))
        {
            return string.Empty;
        }

        var separatorIndex = redisUri.UserInfo.IndexOf(':');
        var password = separatorIndex >= 0
            ? redisUri.UserInfo.Substring(separatorIndex + 1)
            : redisUri.UserInfo;

        return Uri.UnescapeDataString(password);
    }

    private static int? ResolveRedisDatabase(Uri redisUri)
    {
        var path = redisUri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return int.TryParse(path, out var database) && database >= 0
            ? database
            : null;
    }

    private static bool ContainsOption(string configuration, string optionName)
    {
        foreach (var segment in configuration.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Trim().StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
