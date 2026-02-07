using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class CancelSubscriptionModel
{
    public DateOnly? EffectiveDate { get; set; }

    [StringLength(500)]
    public string ChangeReason { get; set; }
}
