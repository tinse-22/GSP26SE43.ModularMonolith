using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestExecution.Entities;

/// <summary>
/// Test run execution record (summary stored in PostgreSQL, details in Redis).
/// </summary>
public class TestRun : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test suite being executed.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// Environment used for execution.
    /// </summary>
    public Guid EnvironmentId { get; set; }

    /// <summary>
    /// User who triggered the test run.
    /// </summary>
    public Guid TriggeredById { get; set; }

    /// <summary>
    /// Auto-increment run number per suite.
    /// </summary>
    public int RunNumber { get; set; }

    /// <summary>
    /// Execution status: Pending, Running, Completed, Failed, Cancelled.
    /// </summary>
    public TestRunStatus Status { get; set; }

    /// <summary>
    /// When the test run started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the test run completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Total number of tests in this run.
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Number of passed tests.
    /// </summary>
    public int PassedCount { get; set; }

    /// <summary>
    /// Number of failed tests.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Number of skipped tests.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Redis key to fetch detailed results.
    /// </summary>
    public string RedisKey { get; set; }

    /// <summary>
    /// When Redis data expires.
    /// </summary>
    public DateTimeOffset? ResultsExpireAt { get; set; }
}

public enum TestRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
