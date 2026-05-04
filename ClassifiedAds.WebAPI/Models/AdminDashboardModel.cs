using System;
using System.Collections.Generic;

namespace ClassifiedAds.WebAPI.Models;

public class AdminDashboardModel
{
    public SubscriptionSummaryModel Subscription { get; set; } = new();

    public RevenueSummaryModel Revenue { get; set; } = new();

    public FailedTransactionSummaryModel FailedTransactions { get; set; } = new();

    public TestRunSummaryModel TestRuns { get; set; } = new();

    public UsageSummaryModel Usage { get; set; } = new();

    public List<TopUserMetricModel> TopUsersByTestRuns { get; set; } = new();

    public List<TopUserMetricModel> TopUsersByLlmCalls { get; set; } = new();

    public List<TopUserMetricModel> TopUsersByStorage { get; set; } = new();

    public List<AdminActionModel> RecentAdminActions { get; set; } = new();
}

public class SubscriptionSummaryModel
{
    public int ActiveCount { get; set; }

    public int TrialCount { get; set; }
}

public class RevenueSummaryModel
{
    public decimal Mrr { get; set; }

    public decimal Arr { get; set; }

    public string Currency { get; set; }
}

public class FailedTransactionSummaryModel
{
    public int TotalFailed { get; set; }

    public List<FailureReasonModel> TopFailureReasons { get; set; } = new();
}

public class FailureReasonModel
{
    public string Reason { get; set; }

    public int Count { get; set; }
}

public class TestRunSummaryModel
{
    public int Today { get; set; }

    public int Last7Days { get; set; }
}

public class UsageSummaryModel
{
    public int LlmCallsThisMonth { get; set; }

    public decimal StorageUsedMB { get; set; }
}

public class TopUserMetricModel
{
    public Guid UserId { get; set; }

    public string UserName { get; set; }

    public decimal Value { get; set; }
}

public class AdminActionModel
{
    public string Action { get; set; }

    public string UserName { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }
}
