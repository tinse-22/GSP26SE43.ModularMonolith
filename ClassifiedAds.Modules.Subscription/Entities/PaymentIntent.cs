using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Tracks the lifecycle of a payment from creation to completion.
/// </summary>
public class PaymentIntent : Entity<Guid>, IAggregateRoot
{
    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentPurpose Purpose { get; set; }

    public Guid PlanId { get; set; }

    public BillingCycle BillingCycle { get; set; }

    public Guid? SubscriptionId { get; set; }

    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.RequiresPayment;

    public string CheckoutUrl { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public long? OrderCode { get; set; }

    public SubscriptionPlan Plan { get; set; }

    public UserSubscription Subscription { get; set; }
}

public enum PaymentPurpose
{
    SubscriptionPurchase = 0,
    SubscriptionUpgrade = 1,
    SubscriptionRenewal = 2,
}

public enum PaymentIntentStatus
{
    RequiresPayment = 0,
    Processing = 1,
    Succeeded = 2,
    Canceled = 3,
    Expired = 4,
}