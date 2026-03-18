using ClassifiedAds.Modules.TestExecution.Entities;
using System;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestRunModel
{
    public Guid Id { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid EnvironmentId { get; set; }

    public int RunNumber { get; set; }

    public string Status { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int TotalTests { get; set; }

    public int PassedCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    public long DurationMs { get; set; }

    public DateTimeOffset? ResultsExpireAt { get; set; }

    public bool HasDetailedResults { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public static TestRunModel FromEntity(TestRun run)
    {
        return new TestRunModel
        {
            Id = run.Id,
            TestSuiteId = run.TestSuiteId,
            EnvironmentId = run.EnvironmentId,
            RunNumber = run.RunNumber,
            Status = run.Status.ToString(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            TotalTests = run.TotalTests,
            PassedCount = run.PassedCount,
            FailedCount = run.FailedCount,
            SkippedCount = run.SkippedCount,
            DurationMs = run.DurationMs,
            ResultsExpireAt = run.ResultsExpireAt,
            HasDetailedResults = run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value > DateTimeOffset.UtcNow,
            CreatedDateTime = run.CreatedDateTime,
            UpdatedDateTime = run.UpdatedDateTime,
        };
    }
}
