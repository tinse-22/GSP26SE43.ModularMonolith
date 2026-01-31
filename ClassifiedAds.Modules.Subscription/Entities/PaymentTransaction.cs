using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Payment transaction record.
/// </summary>
public class PaymentTransaction : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User who made the payment.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Associated subscription.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD", "VND").
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Payment status: Pending, Succeeded, Failed, Refunded.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Payment method (e.g., "card", "bank_transfer").
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Stripe payment intent ID.
    /// </summary>
    public string ExternalTxnId { get; set; }

    /// <summary>
    /// Invoice URL from payment provider.
    /// </summary>
    public string InvoiceUrl { get; set; }

    /// <summary>
    /// Reason for failure if status is Failed.
    /// </summary>
    public string FailureReason { get; set; }

    // Navigation properties
    public UserSubscription Subscription { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}
