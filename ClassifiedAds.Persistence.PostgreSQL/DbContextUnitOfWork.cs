using ClassifiedAds.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Persistence.PostgreSQL;

/// <summary>
/// Base DbContext implementing IUnitOfWork with production-grade transaction management.
/// Provides explicit transaction control with safety guards against misuse.
/// </summary>
/// <typeparam name="TDbContext">The concrete EF Core DbContext type.</typeparam>
public class DbContextUnitOfWork<TDbContext> : DbContext, IUnitOfWork
    where TDbContext : DbContext
{
    private IDbContextTransaction? _dbContextTransaction;

    public DbContextUnitOfWork(DbContextOptions<TDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _dbContextTransaction != null;

    /// <inheritdoc />
    public Guid? CurrentTransactionId => _dbContextTransaction?.TransactionId;

    /// <inheritdoc />
    public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction != null)
        {
            throw new InvalidOperationException(
                $"A transaction is already active (TransactionId: {_dbContextTransaction.TransactionId}). " +
                "Nested transactions are not supported. Commit or rollback the current transaction first.");
        }

        _dbContextTransaction = await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction == null)
        {
            throw new InvalidOperationException(
                "No active transaction to commit. Call BeginTransactionAsync first.");
        }

        try
        {
            await _dbContextTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction == null)
        {
            // Safe to call without active transaction - no-op
            return;
        }

        try
        {
            await _dbContextTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        // Wrap in execution strategy so UseSupabaseRetryPolicy works with explicit transactions.
        var strategy = Database.CreateExecutionStrategy();
        bool isRetry = false;
        bool needsPoolClear = false;
        await strategy.ExecuteAsync(async ct =>
        {
            if (isRetry)
            {
                // Clear tracked entities from the previous failed attempt so
                // the retry starts with a clean change tracker — stale Added /
                // Modified entries would otherwise conflict with fresh reads.
                ChangeTracker.Clear();

                // When the previous attempt failed with ManualResetEventSlim,
                // clear the Npgsql connection pool AGAIN right before opening
                // the new connection.  The first ClearPool (inside
                // ShouldRetryOn) removed idle connectors, but during the
                // exponential-backoff delay other concurrent requests may have
                // returned their own broken connectors to the pool.  A second
                // clear at this point evicts those late arrivals so the retry
                // gets a genuinely fresh physical connection.
                if (needsPoolClear)
                {
                    ClearConnectionPool();
                    needsPoolClear = false;
                }
            }

            isRetry = true;

            await BeginTransactionAsync(isolationLevel, ct);
            try
            {
                await operation(ct);
                await CommitTransactionAsync(ct);
            }
            catch (Exception ex)
            {
                // Swallow rollback exceptions so the *original* exception (e.g. the
                // ManualResetEventSlim ObjectDisposedException) propagates intact to
                // the execution strategy.  A broken connection is auto-rolled back
                // server-side when the connection drops, so losing the ROLLBACK ACK
                // is safe.
                try
                {
                    await RollbackTransactionAsync(CancellationToken.None);
                }
                catch
                {
                    // Intentionally swallowed — see comment above.
                }

                if (NpgsqlTransientHelper.IsManualResetEventDisposed(ex))
                {
                    needsPoolClear = true;
                }

                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        // Wrap in execution strategy so UseSupabaseRetryPolicy works with explicit transactions.
        var strategy = Database.CreateExecutionStrategy();
        bool isRetry = false;
        bool needsPoolClear = false;
        return await strategy.ExecuteAsync(async ct =>
        {
            if (isRetry)
            {
                ChangeTracker.Clear();

                if (needsPoolClear)
                {
                    ClearConnectionPool();
                    needsPoolClear = false;
                }
            }

            isRetry = true;

            await BeginTransactionAsync(isolationLevel, ct);
            try
            {
                var result = await operation(ct);
                await CommitTransactionAsync(ct);
                return result;
            }
            catch (Exception ex)
            {
                // Swallow rollback exceptions so the *original* exception (e.g. the
                // ManualResetEventSlim ObjectDisposedException) propagates intact to
                // the execution strategy.  A broken connection is auto-rolled back
                // server-side when the connection drops, so losing the ROLLBACK ACK
                // is safe.
                try
                {
                    await RollbackTransactionAsync(CancellationToken.None);
                }
                catch
                {
                    // Intentionally swallowed — see comment above.
                }

                if (NpgsqlTransientHelper.IsManualResetEventDisposed(ex))
                {
                    needsPoolClear = true;
                }

                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Best-effort clear of only the current context's Npgsql connection
    /// pool. Used before transaction retries to evict broken connectors
    /// that were returned to the pool during the backoff delay without
    /// disrupting healthy pools owned by other modules.
    /// </summary>
    private void ClearConnectionPool()
    {
        try
        {
            if (Database.GetDbConnection() is NpgsqlConnection connection)
            {
                try
                {
                    connection.Close();
                }
                catch
                {
                    // Ignore close failures for already-broken connectors.
                }

                NpgsqlConnection.ClearPool(connection);
            }
        }
        catch
        {
            // Best-effort — don't let cleanup failure block the retry.
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_dbContextTransaction != null)
        {
            await _dbContextTransaction.DisposeAsync();
            _dbContextTransaction = null;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
        await base.DisposeAsync();
    }

    public override void Dispose()
    {
        _dbContextTransaction?.Dispose();
        _dbContextTransaction = null;
        base.Dispose();
    }
}
