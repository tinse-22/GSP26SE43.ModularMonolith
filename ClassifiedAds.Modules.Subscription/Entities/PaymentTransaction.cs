using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Payment transaction record.
/// </summary>
public class PaymentTransaction : Entity<Guid>, IAggregateRoot
{
    public Guid UserId { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid? PaymentIntentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public PaymentStatus Status { get; set; }

    public string PaymentMethod { get; set; }

    public string Provider { get; set; }

    public string ProviderRef { get; set; }

    public string ExternalTxnId { get; set; }

    public string InvoiceUrl { get; set; }

    public string FailureReason { get; set; }

    public UserSubscription Subscription { get; set; }

    public PaymentIntent PaymentIntent { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
}