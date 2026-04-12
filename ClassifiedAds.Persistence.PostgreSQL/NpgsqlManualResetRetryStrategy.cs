using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace ClassifiedAds.Persistence.PostgreSQL;

/// <summary>
/// Extends the default Npgsql retry strategy to also retry on the
/// ManualResetEventSlim ObjectDisposedException that occurs when Supabase
/// Supavisor forcibly terminates a server-side connection while Npgsql 10.x
/// holds a pooled connector — the connector's internal cancellation lock is
/// disposed, causing any subsequent command or query to throw
/// <see cref="ObjectDisposedException"/> with ObjectName
/// "System.Threading.ManualResetEventSlim".
///
/// Without this override the EF Core execution strategy treats the exception
/// as non-transient and surfaces a 503 to the caller.  With the override, the
/// strategy discards the poisoned connector, waits a short back-off, then
/// retries the entire operation on a freshly checked-out connection.
/// </summary>
public sealed class NpgsqlManualResetRetryStrategy : NpgsqlRetryingExecutionStrategy
{
    private static readonly string DiagLogPath = @"D:\GSP26SE43.ModularMonolith\logs\mres-retry.log";

    // Cached reflection fields — resolved once, reused on every retry.
    // Field names discovered via runtime introspection of Npgsql 10.0.1 + EF Core 10.0.4.
    private static readonly FieldInfo? NpgsqlConnectorField =
        typeof(NpgsqlConnection).GetField("<Connector>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? NpgsqlFullStateField =
        typeof(NpgsqlConnection).GetField("_fullState", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? EfOpenedField =
        typeof(RelationalConnection).GetField("_openedInternally", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? EfOpenedCountField =
        typeof(RelationalConnection).GetField("_openedCount", BindingFlags.Instance | BindingFlags.NonPublic);

    // RelationalConnection field holding the actual DbConnection.
    private static readonly FieldInfo? EfConnectionField =
        typeof(RelationalConnection).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? typeof(RelationalConnection).GetField("<DbConnection>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Tracks whether the last exception was a ManualResetEventSlim disposal
    /// so <see cref="GetNextDelay"/> can apply an appropriate minimum delay.
    /// Instance field (not ThreadStatic) so each EF operation gets a clean
    /// slate — EF Core creates a new strategy instance per execution.
    /// </summary>
    private bool _lastExceptionWasManualReset;

    /// <summary>
    /// Counts consecutive MRES retries for exponential backoff:
    /// 500ms → 1s → 2s → 4s → capped at maxRetryDelay.
    /// Instance field so each operation starts fresh at 500ms.
    /// </summary>
    private int _mresRetryCount;

    private static void DiagLog(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(DiagLogPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(DiagLogPath,
                $"{DateTime.UtcNow:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort diagnostic — don't throw.
        }
    }

    public NpgsqlManualResetRetryStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies)
    {
        DiagLog($"[INIT] Strategy created (simple ctor). ConnectorField={NpgsqlConnectorField != null}, EfOpened={EfOpenedField != null}, EfCount={EfOpenedCountField != null}");
    }

    public NpgsqlManualResetRetryStrategy(
        ExecutionStrategyDependencies dependencies,
        int maxRetryCount,
        TimeSpan maxRetryDelay,
        ICollection<string>? errorCodesToAdd)
        : base(dependencies, maxRetryCount, maxRetryDelay, errorCodesToAdd)
    {
        // One-time field name discovery
        if (NpgsqlConnectorField == null)
        {
            var npgsqlFields = typeof(NpgsqlConnection)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            foreach (var f in npgsqlFields)
            {
                DiagLog($"[FIELD] NpgsqlConnection.{f.Name} : {f.FieldType.Name}");
            }
        }

        if (EfOpenedField == null)
        {
            var efFields = typeof(RelationalConnection)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            foreach (var f in efFields)
            {
                DiagLog($"[FIELD] RelationalConnection.{f.Name} : {f.FieldType.Name}");
            }
        }

        DiagLog($"[INIT] ConnectorField={NpgsqlConnectorField != null}, EfOpened={EfOpenedField != null}, EfCount={EfOpenedCountField != null}");
    }

    protected override bool ShouldRetryOn(Exception? exception)
    {
        // Handle Supavisor circuit breaker as transient — these typically
        // self-heal after 30–60 seconds of cool-down.
        if (exception is Npgsql.PostgresException pgEx && pgEx.SqlState == "XX000"
            && pgEx.MessageText?.Contains("Circuit breaker", StringComparison.OrdinalIgnoreCase) == true)
        {
            _lastExceptionWasManualReset = true;
            DiagLog($"[CIRCUIT-BREAKER] {pgEx.MessageText}");
            return true;
        }

        if (exception is not null && NpgsqlTransientHelper.IsManualResetEventDisposed(exception))
        {
            _lastExceptionWasManualReset = true;

            // The DataSource pool contains poisoned connectors whose
            // internal ManualResetEventSlim was disposed by Supavisor
            // recycling the backend connection.  To recover:
            //
            //  1. Detach the broken connector from the NpgsqlConnection
            //     and mark it Closed so Npgsql doesn't touch the dead
            //     MRES during cleanup.
            //  2. Clear the DataSource pool so subsequent Open() calls
            //     create brand-new TCP connections.
            //  3. Reset EF Core's RelationalConnection bookkeeping so
            //     its next OpenAsync() actually opens the physical
            //     connection instead of just incrementing a counter.
            //  4. Return true + a real delay from GetNextDelay so the
            //     execution strategy retries the operation with a fresh
            //     connection.
            try
            {
                var ctx = Dependencies.CurrentContext?.Context;
                var db = ctx?.Database;
                var dbConn = db?.GetDbConnection() as NpgsqlConnection;

                if (dbConn != null)
                {
                    // (1) Force-close the logical connection first so EF Core
                    // does not keep treating the poisoned connector as open.
                    try
                    {
                        dbConn.Close();
                        DiagLog("[MRES-RETRY] Force-closed broken DbConnection");
                    }
                    catch (Exception closeEx)
                    {
                        DiagLog($"[MRES-RETRY] Close() error: {closeEx.GetType().Name}: {closeEx.Message}");
                    }

                    // (2) Detach the broken connector and force Closed state as
                    // a fallback in case Npgsql still reports the connection open.
                    NpgsqlConnectorField?.SetValue(dbConn, null);
                    NpgsqlFullStateField?.SetValue(dbConn, System.Data.ConnectionState.Closed);

                    // (3) Clear ALL Npgsql pools. When Supavisor recycles backend
                    // connections, connectors across MULTIPLE DataSource pools
                    // are poisoned simultaneously (one pool per module, but all
                    // share the same Supavisor backend). ClearPool(dbConn) only
                    // clears the CURRENT module's pool, leaving other modules
                    // with poisoned connectors that re-contaminate on next use.
                    // ClearAllPools is safe now because retries are bounded (max 5).
                    try
                    {
                        NpgsqlConnection.ClearAllPools();
                        DiagLog("[MRES-RETRY] All pools cleared");
                    }
                    catch (Exception clearEx)
                    {
                        DiagLog($"[MRES-RETRY] ClearAllPools error: {clearEx.GetType().Name}: {clearEx.Message}");
                    }
                }

                // (4) Reset EF Core's RelationalConnection open-count
                //     so the retry actually opens a new physical connection.
                var relConn = ctx != null
                    ? ((IInfrastructure<IServiceProvider>)ctx).Instance
                          .GetService(typeof(IRelationalConnection)) as RelationalConnection
                    : null;
                if (relConn != null)
                {
                    EfOpenedCountField?.SetValue(relConn, 0);
                    EfOpenedField?.SetValue(relConn, false);
                }

                DiagLog("[MRES-RETRY] Detached connector + reset EF state, will retry");
            }
            catch (Exception ex)
            {
                DiagLog($"[MRES-RETRY] Cleanup error: {ex.GetType().Name}: {ex.Message}");
            }

            return true;
        }

        _lastExceptionWasManualReset = false;
        return base.ShouldRetryOn(exception);
    }

    protected override TimeSpan? GetNextDelay(Exception lastException)
    {
        if (_lastExceptionWasManualReset)
        {
            // Respect MaxRetryCount — without this check the strategy
            // retried INFINITELY for MRES errors because we always
            // returned a non-null delay, bypassing the base class limit.
            var currentRetryCount = ExceptionsEncountered.Count - 1;
            if (currentRetryCount >= MaxRetryCount)
            {
                DiagLog($"[MRES-RETRY] Retry limit reached ({MaxRetryCount}), giving up");
                _mresRetryCount = 0;
                return null; // triggers RetryLimitExceededException
            }

            // Exponential backoff: 500ms → 1s → 2s → 4s → 8s
            // The pool was cleared and the connector was force-closed in
            // ShouldRetryOn, so the next attempt opens a fresh connection.
            _mresRetryCount++;
            var delaySec = Math.Min(0.5 * Math.Pow(2, _mresRetryCount - 1), 30);
            var delay = TimeSpan.FromSeconds(delaySec);
            DiagLog($"[MRES-RETRY] GetNextDelay → {delay.TotalSeconds}s (attempt #{_mresRetryCount}/{MaxRetryCount})");
            return delay;
        }

        _mresRetryCount = 0;
        return base.GetNextDelay(lastException);
    }
}
