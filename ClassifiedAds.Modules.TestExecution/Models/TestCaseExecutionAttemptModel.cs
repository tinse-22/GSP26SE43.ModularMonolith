using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseExecutionAttemptModel
{
    public Guid ExecutionAttemptId { get; set; } = Guid.NewGuid();

    public Guid TestRunId { get; set; }

    public Guid TestCaseId { get; set; }

    public Guid? ParentAttemptId { get; set; }

    public int AttemptNumber { get; set; }

    public string RetryReason { get; set; }

    /// <summary>
    /// IDs of previously-skipped test cases that this attempt is replaying.
    /// Populated only for replay-type attempts.
    /// </summary>
    public List<Guid> ReplayedSkippedCaseIds { get; set; } = new List<Guid>();

    /// <summary>
    /// IDs of dependency test cases whose failure/skip directly caused this attempt
    /// to be skipped or retried.
    /// </summary>
    public List<Guid> DependencyRootCauseIds { get; set; } = new List<Guid>();

    public TestRunRetryPolicyModel RetryPolicy { get; set; } = new TestRunRetryPolicyModel();

    public string Status { get; set; }

    public string SkippedCause { get; set; }

    public string DependencyRootCause { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long DurationMs { get; set; }

    public List<ValidationFailureModel> FailureReasons { get; set; } = new List<ValidationFailureModel>();

    /// <summary>True when this attempt was a replay of a previously skipped case.</summary>
    public bool IsReplay => RetryReason != null
        && RetryReason.Contains("Replay", StringComparison.OrdinalIgnoreCase);
}
