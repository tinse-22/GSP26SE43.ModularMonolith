using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Usage tracking for a user within a billing period.
/// </summary>
public class UsageTracking : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User being tracked.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Start of billing period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// End of billing period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Number of projects created.
    /// </summary>
    public int ProjectCount { get; set; }

    /// <summary>
    /// Total number of endpoints across all projects.
    /// </summary>
    public int EndpointCount { get; set; }

    /// <summary>
    /// Number of test suites created.
    /// </summary>
    public int TestSuiteCount { get; set; }

    /// <summary>
    /// Number of test cases created.
    /// </summary>
    public int TestCaseCount { get; set; }

    /// <summary>
    /// Number of test runs executed.
    /// </summary>
    public int TestRunCount { get; set; }

    /// <summary>
    /// Number of LLM API calls made.
    /// </summary>
    public int LlmCallCount { get; set; }

    /// <summary>
    /// Storage used in megabytes.
    /// </summary>
    public decimal StorageUsedMB { get; set; }
}
