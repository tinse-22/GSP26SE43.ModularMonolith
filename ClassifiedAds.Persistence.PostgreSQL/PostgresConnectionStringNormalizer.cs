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

        // Supabase session pooler + Npgsql 10.x has a known incompatibility:
        // Supavisor recycles backend connections periodically, disposing the
        // connector's ManualResetEventSlim. This causes ObjectDisposedException
        // during multi-module operations (uploads, transactions across modules).
        //
        // With Pooling=false, each query opens a fresh TCP/SSL connection and
        // closes it immediately. No lingering pooled connectors to get poisoned.
        // Trade-off: ~50-100ms extra latency per query (TCP+SSL handshake).
        //
        // Note: even Pooling=false does NOT fully prevent MRES during sustained
        // multi-module bursts against Supabase session pooler. For reliable
        // operation, use local PostgreSQL (StandaloneDB mode) or Supabase
        // direct connection (db.*.supabase.co, IPv6 required).
        builder.Pooling = false;

        // Supavisor may not support DISCARD ALL correctly; skip the
        // session-reset command if a pool connector is ever reused.
        builder.NoResetOnClose = true;

        // KeepAlive is not useful with Pooling=false (no idle connections).
        builder.KeepAlive = 0;

        return builder.ConnectionString;
    }

    private static bool IsSupabasePooler(string? host)
    {
        return !string.IsNullOrWhiteSpace(host)
            && host.Contains(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase);
    }
}
