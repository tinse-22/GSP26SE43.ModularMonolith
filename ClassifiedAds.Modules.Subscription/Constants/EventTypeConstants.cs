namespace ClassifiedAds.Modules.Subscription.Constants;

internal class EventTypeConstants
{
    public const string PlanCreated = "PLAN_CREATED";
    public const string PlanUpdated = "PLAN_UPDATED";
    public const string PlanDeleted = "PLAN_DELETED";

    public const string AuditLogEntryCreated = "AUDIT_LOG_ENTRY_CREATED";

    public const string PaymentIntentCreated = "PAYMENT_INTENT_CREATED";
    public const string PaymentCheckoutLinkCreated = "PAYMENT_CHECKOUT_LINK_CREATED";
    public const string PaymentCheckoutReconcileRequested = "PAYMENT_CHECKOUT_RECONCILE_REQUESTED";
    public const string PaymentIntentStatusChanged = "PAYMENT_INTENT_STATUS_CHANGED";
    public const string PaymentTransactionCreated = "PAYMENT_TRANSACTION_CREATED";
    public const string SubscriptionChanged = "SUBSCRIPTION_CHANGED";
}
