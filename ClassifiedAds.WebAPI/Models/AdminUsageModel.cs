using System.Collections.Generic;

namespace ClassifiedAds.WebAPI.Models;

public class AdminUsageModel
{
    public string From { get; set; }

    public string To { get; set; }

    public List<UsagePointModel> Points { get; set; } = new();

    public UsageTotalsModel Totals { get; set; } = new();

    public UsageTopUsersModel TopUsers { get; set; } = new();
}

public class UsagePointModel
{
    public string Period { get; set; }

    public int ProjectCount { get; set; }

    public int TestRunCount { get; set; }

    public int LlmCallCount { get; set; }

    public decimal StorageUsedMB { get; set; }
}

public class UsageTotalsModel
{
    public int ProjectCount { get; set; }

    public int TestRunCount { get; set; }

    public int LlmCallCount { get; set; }

    public decimal StorageUsedMB { get; set; }
}

public class UsageTopUsersModel
{
    public List<TopUserMetricModel> Projects { get; set; } = new();

    public List<TopUserMetricModel> TestRuns { get; set; } = new();

    public List<TopUserMetricModel> LlmCalls { get; set; } = new();

    public List<TopUserMetricModel> Storage { get; set; } = new();
}
