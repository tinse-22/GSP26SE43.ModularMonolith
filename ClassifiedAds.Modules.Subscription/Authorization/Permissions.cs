namespace ClassifiedAds.Modules.Subscription.Authorization;

public static class Permissions
{
    public const string GetPlans = "Permission:GetPlans";
    public const string AddPlan = "Permission:AddPlan";
    public const string UpdatePlan = "Permission:UpdatePlan";
    public const string DeletePlan = "Permission:DeletePlan";
    public const string GetPlanAuditLogs = "Permission:GetPlanAuditLogs";

    public const string GetSubscription = "Permission:GetSubscription";
    public const string GetCurrentSubscription = "Permission:GetCurrentSubscription";
    public const string AddSubscription = "Permission:AddSubscription";
    public const string UpdateSubscription = "Permission:UpdateSubscription";
    public const string CancelSubscription = "Permission:CancelSubscription";
    public const string GetSubscriptionHistory = "Permission:GetSubscriptionHistory";
    public const string GetPaymentTransactions = "Permission:GetPaymentTransactions";
    public const string AddPaymentTransaction = "Permission:AddPaymentTransaction";
    public const string GetUsageTracking = "Permission:GetUsageTracking";
    public const string UpdateUsageTracking = "Permission:UpdateUsageTracking";

    public const string CreateSubscriptionPayment = "Permission:CreateSubscriptionPayment";
    public const string GetPaymentIntent = "Permission:GetPaymentIntent";
    public const string CreatePayOsCheckout = "Permission:CreatePayOsCheckout";
    public const string SyncPayment = "Permission:SyncPayment";
}
