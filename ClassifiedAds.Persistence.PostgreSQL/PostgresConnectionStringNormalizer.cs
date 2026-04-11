using Npgsql;
using System;

namespace ClassifiedAds.Persistence.PostgreSQL;

public static class PostgresConnectionStringNormalizer
{
    public static string NormalizeForSupabasePooler(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!IsSupabasePooler(builder.Host))
        {
            return builder.ConnectionString;
        }

        // Supabase already sits behind Supavisor; disabling client pooling avoids
        // reusing connector state that may have been disposed underneath Npgsql.
        builder.Pooling = false;
        builder.NoResetOnClose = false;

        return builder.ConnectionString;
    }

    private static bool IsSupabasePooler(string? host)
    {
        return !string.IsNullOrWhiteSpace(host)
            && host.Contains(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase);
    }
}
