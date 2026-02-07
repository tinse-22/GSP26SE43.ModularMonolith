using ClassifiedAds.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Persistence.PostgreSQL;

/// <summary>
/// Base DbContext implementing IUnitOfWork with production-grade transaction management.
/// Provides explicit transaction control with safety guards against misuse.
/// </summary>
public class DbContextUnitOfWork<TDbContext> : DbContext, IUnitOfWork
    where TDbContext : DbContext
{
    private IDbContextTransaction? _dbContextTransaction;
    private readonly object _transactionLock = new();

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

        await BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            await operation(cancellationToken);
            await CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
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

        await BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
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
