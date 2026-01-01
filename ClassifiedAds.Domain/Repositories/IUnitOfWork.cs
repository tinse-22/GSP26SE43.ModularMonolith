using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Domain.Repositories;

/// <summary>
/// Unit of Work abstraction providing transaction management and atomic save operations.
/// All repositories within the same scope share the same IUnitOfWork instance.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all pending changes to the database.
    /// If no explicit transaction is active, EF Core wraps this in an implicit transaction.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a transaction is already active.</exception>
    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no transaction is active.</exception>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction. Safe to call even if no transaction is active.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether an explicit transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Gets the current transaction ID, or null if no transaction is active.
    /// </summary>
    Guid? CurrentTransactionId { get; }

    /// <summary>
    /// Executes the given operation within a transaction.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    /// <param name="operation">The operation to execute within the transaction.</param>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the given operation within a transaction and returns a result.
    /// Automatically commits on success, rolls back on exception.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
