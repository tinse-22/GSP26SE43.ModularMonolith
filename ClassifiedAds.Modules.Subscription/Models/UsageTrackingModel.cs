using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class UsageTrackingModel
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public int ProjectCount { get; set; }

    public int EndpointCount { get; set; }

    public int TestSuiteCount { get; set; }

    public int TestCaseCount { get; set; }

    public int TestRunCount { get; set; }

    public int LlmCallCount { get; set; }

    public decimal StorageUsedMB { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
