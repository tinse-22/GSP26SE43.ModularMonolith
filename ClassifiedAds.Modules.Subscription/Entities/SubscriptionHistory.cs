using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// History of subscription plan changes.
/// </summary>
public class SubscriptionHistory : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Subscription this history entry belongs to.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Previous plan (null if first subscription).
    /// </summary>
    public Guid? OldPlanId { get; set; }

    /// <summary>
    /// New plan.
    /// </summary>
    public Guid NewPlanId { get; set; }

    /// <summary>
    /// Type of change: Created, Upgraded, Downgraded, Cancelled, Reactivated.
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// Reason for change (optional).
    /// </summary>
    public string ChangeReason { get; set; }

    /// <summary>
    /// When the change takes effect.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    // Navigation properties
    public UserSubscription Subscription { get; set; }
    public SubscriptionPlan OldPlan { get; set; }
    public SubscriptionPlan NewPlan { get; set; }
}

public enum ChangeType
{
    Created = 0,
    Upgraded = 1,
    Downgraded = 2,
    Cancelled = 3,
    Reactivated = 4
}
