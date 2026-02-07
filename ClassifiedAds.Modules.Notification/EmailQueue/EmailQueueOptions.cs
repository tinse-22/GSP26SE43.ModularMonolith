namespace ClassifiedAds.Modules.Notification.EmailQueue;

/// <summary>
/// Configuration knobs for the in-memory email Channel queue + worker.
/// Bound from <c>Modules:Notification:EmailQueue</c> in appsettings.
/// </summary>
public sealed class EmailQueueOptions
{
    /// <summary>
    /// Bounded channel capacity. 
    /// For a small project (≤10 concurrent emails) 64 is more than enough.
    /// </summary>
    public int ChannelCapacity { get; set; } = 64;

    /// <summary>
    /// Max number of emails the worker processes concurrently.
    /// Keep low to avoid SMTP rate-limits.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 3;

    /// <summary>
    /// Maximum retry attempts per email (including first try).
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay (seconds) for exponential backoff between retries.
    /// Actual delay = BaseDelaySeconds * 2^(attempt-1), capped at MaxDelaySeconds.
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay cap (seconds) for backoff.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 120;

    /// <summary>
    /// Per-email send timeout (seconds). Prevents hanging SMTP connections.
    /// </summary>
    public int SendTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Circuit breaker — how many consecutive failures before opening the circuit.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker — how long (seconds) the circuit stays open before half-open probe.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Interval (seconds) for the DB-sweep that picks up orphaned/unsent emails
    /// not in the Channel (e.g. after app restart).
    /// </summary>
    public int DbSweepIntervalSeconds { get; set; } = 30;
}
