using System;

namespace ClassifiedAds.Modules.Notification.EmailQueue;

/// <summary>
/// Represents a single email job in the in-memory Channel queue.
/// Carries the database entity Id so the worker can reload from DB (idempotent).
/// </summary>
public sealed class EmailQueueItem
{
    /// <summary>
    /// Primary key of the <see cref="Entities.EmailMessage"/> row.
    /// The worker will reload the entity from DB to guarantee consistency.
    /// </summary>
    public Guid EmailMessageId { get; init; }

    /// <summary>
    /// Monotonically increasing attempt number (starts at 1).
    /// Used by the worker to apply exponential backoff delays.
    /// </summary>
    public int Attempt { get; init; } = 1;

    /// <summary>
    /// UTC timestamp when this item was first enqueued.
    /// Useful for metrics / dead-letter age tracking.
    /// </summary>
    public DateTimeOffset EnqueuedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
