using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.Subscription.Models;

public static class SubscriptionModelMappingConfiguration
{
    public static SubscriptionModel ToModel(this UserSubscription entity, SubscriptionPlan plan = null)
    {
        if (entity == null)
        {
            return null;
        }

        return new SubscriptionModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            PlanId = entity.PlanId,
            PlanName = entity.SnapshotPlanName ?? plan?.Name,
            PlanDisplayName = entity.SnapshotPlanName ?? plan?.DisplayName ?? plan?.Name,
            Status = entity.Status,
            BillingCycle = entity.BillingCycle,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            NextBillingDate = entity.NextBillingDate,
            TrialEndsAt = entity.TrialEndsAt,
            CancelledAt = entity.CancelledAt,
            AutoRenew = entity.AutoRenew,
            ExternalSubId = entity.ExternalSubId,
            ExternalCustId = entity.ExternalCustId,
            SnapshotPriceMonthly = entity.SnapshotPriceMonthly,
            SnapshotPriceYearly = entity.SnapshotPriceYearly,
            SnapshotCurrency = entity.SnapshotCurrency,
            SnapshotPlanName = entity.SnapshotPlanName,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
        };
    }

    public static IEnumerable<SubscriptionModel> ToModels(
        this IEnumerable<UserSubscription> entities,
        IDictionary<Guid, SubscriptionPlan> planLookup = null)
    {
        return entities.Select(x =>
        {
            SubscriptionPlan plan = null;
            if (planLookup != null)
            {
                planLookup.TryGetValue(x.PlanId, out plan);
            }

            return x.ToModel(plan);
        });
    }

    public static SubscriptionHistoryModel ToModel(
        this SubscriptionHistory entity,
        IDictionary<Guid, SubscriptionPlan> planLookup = null)
    {
        if (entity == null)
        {
            return null;
        }

        SubscriptionPlan oldPlan = null;
        if (entity.OldPlanId.HasValue && planLookup != null)
        {
            planLookup.TryGetValue(entity.OldPlanId.Value, out oldPlan);
        }

        SubscriptionPlan newPlan = null;
        planLookup?.TryGetValue(entity.NewPlanId, out newPlan);

        return new SubscriptionHistoryModel
        {
            Id = entity.Id,
            SubscriptionId = entity.SubscriptionId,
            OldPlanId = entity.OldPlanId,
            OldPlanName = oldPlan?.DisplayName ?? oldPlan?.Name,
            NewPlanId = entity.NewPlanId,
            NewPlanName = newPlan?.DisplayName ?? newPlan?.Name,
            ChangeType = entity.ChangeType,
            ChangeReason = entity.ChangeReason,
            EffectiveDate = entity.EffectiveDate,
            CreatedDateTime = entity.CreatedDateTime,
        };
    }

    public static PaymentTransactionModel ToModel(this PaymentTransaction entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new PaymentTransactionModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            SubscriptionId = entity.SubscriptionId,
            PaymentIntentId = entity.PaymentIntentId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Status = entity.Status,
            PaymentMethod = entity.PaymentMethod,
            Provider = entity.Provider,
            ProviderRef = entity.ProviderRef,
            ExternalTxnId = entity.ExternalTxnId,
            InvoiceUrl = entity.InvoiceUrl,
            FailureReason = entity.FailureReason,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
        };
    }

    public static UsageTrackingModel ToModel(this UsageTracking entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new UsageTrackingModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            PeriodStart = entity.PeriodStart,
            PeriodEnd = entity.PeriodEnd,
            ProjectCount = entity.ProjectCount,
            EndpointCount = entity.EndpointCount,
            TestSuiteCount = entity.TestSuiteCount,
            TestCaseCount = entity.TestCaseCount,
            TestRunCount = entity.TestRunCount,
            LlmCallCount = entity.LlmCallCount,
            StorageUsedMB = entity.StorageUsedMB,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
        };
    }
}
