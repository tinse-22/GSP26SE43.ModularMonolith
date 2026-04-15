using System;
using System.Data.Common;

namespace ClassifiedAds.Infrastructure.Configuration;

public static class StandaloneDevelopmentDatabaseBridge
{
    private const string DefaultDatabaseHost = "127.0.0.1";
    private const string DefaultDatabaseName = "ClassifiedAds";
    private const string DefaultDatabasePort = "55432";
    private const string DefaultDatabaseUser = "postgres";

    public static void Apply(bool isRunningInContainer, string? processConnectionStringBeforeDotEnv)
    {
        if (isRunningInContainer)
        {
            return;
        }

        var databaseMode = Environment.GetEnvironmentVariable("STANDALONE_DATABASE_MODE");
        if (string.Equals(databaseMode, "external", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[StandaloneDB] Database mode: External (.env/process ConnectionStrings__Default)");
            return;
        }

        if (!string.IsNullOrWhiteSpace(processConnectionStringBeforeDotEnv))
        {
            Console.WriteLine("[StandaloneDB] Database mode: External (process-level ConnectionStrings__Default)");
            return;
        }

        var localConnectionString = BuildLocalConnectionString();
        if (string.IsNullOrWhiteSpace(localConnectionString))
        {
            if (string.Equals(databaseMode, "local", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "STANDALONE_DATABASE_MODE=local requires POSTGRES_PASSWORD. " +
                    "Optionally set POSTGRES_USER, POSTGRES_DB, POSTGRES_HOST_PORT, or POSTGRES_HOST.");
            }

            return;
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", localConnectionString);
        SetIfMissing("CheckDependency__Host", BuildLocalDependencyHost());
        Console.WriteLine($"[StandaloneDB] Database mode: Local PostgreSQL ({BuildConnectionSummary(localConnectionString)})");
    }

    private static string? BuildLocalConnectionString()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? DefaultDatabaseHost,
            ["Port"] = Environment.GetEnvironmentVariable("POSTGRES_HOST_PORT") ?? DefaultDatabasePort,
            ["Database"] = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? DefaultDatabaseName,
            ["Username"] = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? DefaultDatabaseUser,
            ["Password"] = password,
        };

        return builder.ConnectionString;
    }

    private static string BuildLocalDependencyHost()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? DefaultDatabaseHost;
        var port = Environment.GetEnvironmentVariable("POSTGRES_HOST_PORT") ?? DefaultDatabasePort;
        return $"{host}:{port}";
    }

    private static string BuildConnectionSummary(string connectionString)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString,
        };

        var host = builder.TryGetValue("Host", out var hostValue) ? Convert.ToString(hostValue) : string.Empty;
        var port = builder.TryGetValue("Port", out var portValue) ? Convert.ToString(portValue) : string.Empty;
        var database = builder.TryGetValue("Database", out var databaseValue) ? Convert.ToString(databaseValue) : string.Empty;

        return $"Host={host};Port={port};Database={database}";
    }

    private static void SetIfMissing(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
