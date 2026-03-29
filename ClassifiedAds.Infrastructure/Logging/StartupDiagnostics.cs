using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace ClassifiedAds.Infrastructure.Logging;

public static class StartupDiagnostics
{
    public static void LogDatabaseTarget(string hostName, IConfiguration configuration, bool isRunningInContainer)
    {
        var connectionString = configuration.GetConnectionString("Default");
        var runtimeMode = isRunningInContainer ? "container" : "local-process";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine($"[{hostName}] RuntimeMode={runtimeMode} ConnectionStrings:Default is missing");
            return;
        }

        Console.WriteLine($"[{hostName}] RuntimeMode={runtimeMode} DBTarget={BuildConnectionSummary(connectionString)}");
    }

    public static void LogCacheTarget(string hostName, IConfiguration configuration)
    {
        var provider = configuration["Caching:Distributed:Provider"];

        if (string.IsNullOrWhiteSpace(provider))
        {
            Console.WriteLine($"[{hostName}] CacheProvider=missing");
            return;
        }

        if (string.Equals(provider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisConfiguration = configuration["Caching:Distributed:Redis:Configuration"];
            var instanceName = configuration["Caching:Distributed:Redis:InstanceName"];
            Console.WriteLine($"[{hostName}] CacheProvider=Redis RedisTarget={redisConfiguration ?? "missing"} InstanceName={instanceName ?? "missing"}");
            return;
        }

        Console.WriteLine($"[{hostName}] CacheProvider={provider}");
    }

    private static string BuildConnectionSummary(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString,
            };

            var host = GetFirstValue(builder, "Host", "Server", "Data Source");
            var port = GetFirstValue(builder, "Port");
            var database = GetFirstValue(builder, "Database", "Initial Catalog");

            if (string.IsNullOrWhiteSpace(host) && string.IsNullOrWhiteSpace(database))
            {
                return "unrecognized-connection-string";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(host))
            {
                parts.Add($"Host={host}");
            }

            if (!string.IsNullOrWhiteSpace(port))
            {
                parts.Add($"Port={port}");
            }

            if (!string.IsNullOrWhiteSpace(database))
            {
                parts.Add($"Database={database}");
            }

            return string.Join(";", parts);
        }
        catch
        {
            return "invalid-connection-string";
        }
    }

    private static string GetFirstValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value != null)
            {
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }
}
