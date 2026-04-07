using System;
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
        var configuration = options?.Configuration;
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return configuration;
        }

        if (ContainsOption(configuration, "abortConnect") || ContainsOption(configuration, "abortOnConnectFail"))
        {
            return configuration;
        }

        return $"{configuration},abortConnect=false";
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
