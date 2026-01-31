using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Usage limit for a subscription plan.
/// </summary>
public class PlanLimit : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Plan this limit belongs to.
    /// </summary>
    public Guid PlanId { get; set; }

    /// <summary>
    /// Type of limit being defined.
    /// </summary>
    public LimitType LimitType { get; set; }

    /// <summary>
    /// Limit value (null if unlimited).
    /// </summary>
    public int? LimitValue { get; set; }

    /// <summary>
    /// Whether this limit is unlimited.
    /// </summary>
    public bool IsUnlimited { get; set; }

    // Navigation properties
    public SubscriptionPlan Plan { get; set; }
}

public enum LimitType
{
    MaxProjects = 0,
    MaxEndpointsPerProject = 1,
    MaxTestCasesPerSuite = 2,
    MaxTestRunsPerMonth = 3,
    MaxConcurrentRuns = 4,
    RetentionDays = 5,
    MaxLlmCallsPerMonth = 6,
    MaxStorageMB = 7
}
