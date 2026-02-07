using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PaymentTransactionModel
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid SubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public PaymentStatus Status { get; set; }

    public string PaymentMethod { get; set; }

    public string ExternalTxnId { get; set; }

    public string InvoiceUrl { get; set; }

    public string FailureReason { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
