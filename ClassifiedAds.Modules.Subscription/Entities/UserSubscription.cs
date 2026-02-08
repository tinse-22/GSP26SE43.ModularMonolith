using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// User's active subscription.
/// </summary>
public class UserSubscription : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User who owns this subscription.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Subscription plan.
    /// </summary>
    public Guid PlanId { get; set; }

    /// <summary>
    /// Subscription status: Trial, Active, PastDue, Cancelled, Expired.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// Billing cycle: Monthly, Yearly.
    /// </summary>
    public BillingCycle? BillingCycle { get; set; }

    /// <summary>
    /// Subscription start date.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Subscription end date (null for free plan).
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Next billing date.
    /// </summary>
    public DateOnly? NextBillingDate { get; set; }

    /// <summary>
    /// When trial period ends.
    /// </summary>
    public DateTimeOffset? TrialEndsAt { get; set; }

    /// <summary>
    /// When subscription was cancelled.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// Whether subscription auto-renews.
    /// </summary>
    public bool AutoRenew { get; set; }

    /// <summary>
    /// Stripe subscription ID.
    /// </summary>
    public string ExternalSubId { get; set; }

    /// <summary>
    /// Stripe customer ID.
    /// </summary>
    public string ExternalCustId { get; set; }

    // ── Price / plan snapshot at time of purchase ──

    /// <summary>
    /// Monthly price locked at the time of purchase / last renewal.
    /// </summary>
    public decimal? SnapshotPriceMonthly { get; set; }

    /// <summary>
    /// Yearly price locked at the time of purchase / last renewal.
    /// </summary>
    public decimal? SnapshotPriceYearly { get; set; }

    /// <summary>
    /// Currency locked at the time of purchase / last renewal.
    /// </summary>
    public string SnapshotCurrency { get; set; }

    /// <summary>
    /// Plan display name locked at the time of purchase / last renewal.
    /// </summary>
    public string SnapshotPlanName { get; set; }

    // Navigation properties
    public SubscriptionPlan Plan { get; set; }
}

public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
    Expired = 4
}

public enum BillingCycle
{
    Monthly = 0,
    Yearly = 1
}
