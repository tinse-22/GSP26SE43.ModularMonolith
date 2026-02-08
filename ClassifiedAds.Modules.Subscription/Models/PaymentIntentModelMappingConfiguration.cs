using ClassifiedAds.Modules.Subscription.Entities;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.Subscription.Models;

public static class PaymentIntentModelMappingConfiguration
{
    public static PaymentIntentModel ToModel(this PaymentIntent entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new PaymentIntentModel
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Purpose = entity.Purpose,
            PlanId = entity.PlanId,
            PlanName = entity.Plan?.DisplayName ?? entity.Plan?.Name,
            BillingCycle = entity.BillingCycle,
            SubscriptionId = entity.SubscriptionId,
            Status = entity.Status,
            CheckoutUrl = entity.CheckoutUrl,
            ExpiresAt = entity.ExpiresAt,
            OrderCode = entity.OrderCode,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
        };
    }

    public static List<PaymentIntentModel> ToModels(this IEnumerable<PaymentIntent> entities)
    {
        return entities?.Select(x => x.ToModel()).ToList() ?? new List<PaymentIntentModel>();
    }
}