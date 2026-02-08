using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class SubscriptionModel
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PlanId { get; set; }

    public string PlanName { get; set; }

    public string PlanDisplayName { get; set; }

    public SubscriptionStatus Status { get; set; }

    public BillingCycle? BillingCycle { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateOnly? NextBillingDate { get; set; }

    public DateTimeOffset? TrialEndsAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public bool AutoRenew { get; set; }

    public string ExternalSubId { get; set; }

    public string ExternalCustId { get; set; }

    /// <summary>Monthly price locked at the time of purchase / last renewal.</summary>
    public decimal? SnapshotPriceMonthly { get; set; }

    public decimal? SnapshotPriceYearly { get; set; }

    public string SnapshotCurrency { get; set; }

    public string SnapshotPlanName { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
