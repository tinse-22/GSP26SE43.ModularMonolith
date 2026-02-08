using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PaymentIntentModel
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public PaymentPurpose Purpose { get; set; }

    public Guid PlanId { get; set; }

    public string PlanName { get; set; }

    public BillingCycle BillingCycle { get; set; }

    public Guid? SubscriptionId { get; set; }

    public PaymentIntentStatus Status { get; set; }

    public string CheckoutUrl { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public long? OrderCode { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}