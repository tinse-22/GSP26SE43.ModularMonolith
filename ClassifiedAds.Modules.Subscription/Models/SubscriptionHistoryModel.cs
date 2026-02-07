using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class SubscriptionHistoryModel
{
    public Guid Id { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid? OldPlanId { get; set; }

    public string OldPlanName { get; set; }

    public Guid NewPlanId { get; set; }

    public string NewPlanName { get; set; }

    public ChangeType ChangeType { get; set; }

    public string ChangeReason { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }
}
