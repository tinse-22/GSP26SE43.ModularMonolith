using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.IntegrationEvents;

public abstract class SubscriptionOutboxEventBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string Version { get; set; } = "1.0";
}

public class PaymentIntentCreatedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid IntentId { get; set; }

    public Guid UserId { get; set; }

    public Guid PlanId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }

    public PaymentPurpose Purpose { get; set; }

    public BillingCycle BillingCycle { get; set; }

    public PaymentIntentStatus Status { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public long? OrderCode { get; set; }
}

public class PaymentCheckoutLinkCreatedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid IntentId { get; set; }

    public Guid UserId { get; set; }

    public long OrderCode { get; set; }

    public string CheckoutUrl { get; set; }

    public PaymentIntentStatus Status { get; set; }
}

public class PaymentCheckoutReconcileRequestedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid IntentId { get; set; }

    public Guid UserId { get; set; }

    public long? OrderCode { get; set; }

    public string Reason { get; set; }
}

public class PaymentIntentStatusChangedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid IntentId { get; set; }

    public Guid UserId { get; set; }

    public long? OrderCode { get; set; }

    public PaymentIntentStatus OldStatus { get; set; }

    public PaymentIntentStatus NewStatus { get; set; }

    public string Provider { get; set; }

    public string ProviderStatus { get; set; }

    public string Reason { get; set; }
}

public class PaymentTransactionCreatedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid TransactionId { get; set; }

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

    public string FailureReason { get; set; }
}

public class SubscriptionChangedOutboxEvent : SubscriptionOutboxEventBase
{
    public Guid SubscriptionId { get; set; }

    public Guid UserId { get; set; }

    public Guid? OldPlanId { get; set; }

    public Guid NewPlanId { get; set; }

    public SubscriptionStatus? OldStatus { get; set; }

    public SubscriptionStatus NewStatus { get; set; }

    public BillingCycle? BillingCycle { get; set; }

    public ChangeType? ChangeType { get; set; }

    public string ChangeReason { get; set; }

    public DateOnly? EffectiveDate { get; set; }
}
