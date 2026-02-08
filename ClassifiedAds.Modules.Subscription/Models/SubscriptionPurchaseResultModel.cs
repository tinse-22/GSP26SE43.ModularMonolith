using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class SubscriptionPurchaseResultModel
{
    public bool RequiresPayment { get; set; }

    public Guid? PaymentIntentId { get; set; }

    public SubscriptionModel Subscription { get; set; }
}