using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;

namespace ClassifiedAds.Persistence.PostgreSQL;

/// <summary>
/// Extension helpers that wire up <see cref="NpgsqlManualResetRetryStrategy"/>
/// as the execution strategy for all module DbContexts that talk to Supabase.
///
/// Replaces the scalar <c>EnableRetryOnFailure</c> call so that the strategy
/// also retries the ManualResetEventSlim ObjectDisposedException that occurs
/// when Supabase Supavisor recycles a pooled connector mid-request.
/// </summary>
public static class NpgsqlRetryPolicyExtensions
{
    private const int DefaultMaxRetryCount = 5;
    private static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Registers <see cref="NpgsqlManualResetRetryStrategy"/> as the execution
    /// strategy.  Call this instead of <c>sql.EnableRetryOnFailure(...)</c>
    /// inside a <c>UseNpgsql</c> options lambda.
    /// </summary>
    public static NpgsqlDbContextOptionsBuilder UseSupabaseRetryPolicy(
        this NpgsqlDbContextOptionsBuilder builder,
        int maxRetryCount = DefaultMaxRetryCount,
        TimeSpan? maxRetryDelay = null)
    {
        return builder.ExecutionStrategy(ctx =>
            new NpgsqlManualResetRetryStrategy(
                ctx,
                maxRetryCount,
                maxRetryDelay ?? DefaultMaxRetryDelay,
                errorCodesToAdd: null));
    }
}
