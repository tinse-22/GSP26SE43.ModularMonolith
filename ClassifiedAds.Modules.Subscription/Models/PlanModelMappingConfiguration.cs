using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.Subscription.Models;

public static class PlanModelMappingConfiguration
{
    public static PlanModel ToModel(this SubscriptionPlan entity, List<PlanLimit> limits = null)
    {
        if (entity == null)
        {
            return null;
        }

        return new PlanModel
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            PriceMonthly = entity.PriceMonthly,
            PriceYearly = entity.PriceYearly,
            Currency = entity.Currency,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
            Limits = limits?.Select(l => l.ToModel()).ToList() ?? new List<PlanLimitModel>(),
        };
    }

    public static IEnumerable<PlanModel> ToModels(this IEnumerable<SubscriptionPlan> entities, ILookup<Guid, PlanLimit> limitsLookup = null)
    {
        return entities.Select(x => x.ToModel(limitsLookup?[x.Id]?.ToList()));
    }

    public static PlanLimitModel ToModel(this PlanLimit entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new PlanLimitModel
        {
            Id = entity.Id,
            LimitType = entity.LimitType,
            LimitValue = entity.LimitValue,
            IsUnlimited = entity.IsUnlimited,
        };
    }

    public static SubscriptionPlan ToEntity(this CreateUpdatePlanModel model)
    {
        return new SubscriptionPlan
        {
            Name = model.Name?.Trim(),
            DisplayName = model.DisplayName?.Trim(),
            Description = model.Description?.Trim(),
            PriceMonthly = model.PriceMonthly,
            PriceYearly = model.PriceYearly,
            Currency = model.Currency?.Trim().ToUpperInvariant() ?? "USD",
            IsActive = model.IsActive,
            SortOrder = model.SortOrder,
        };
    }

    public static List<PlanLimit> ToLimitEntities(this CreateUpdatePlanModel model, Guid planId)
    {
        return model.Limits?.Select(l =>
        {
            if (!l.LimitType.HasValue)
            {
                throw new CrossCuttingConcerns.Exceptions.ValidationException(
                    "Loại giới hạn là bắt buộc.");
            }

            return new PlanLimit
            {
                PlanId = planId,
                LimitType = l.LimitType.Value,
                LimitValue = l.IsUnlimited ? null : l.LimitValue,
                IsUnlimited = l.IsUnlimited,
            };
        }).ToList() ?? new List<PlanLimit>();
    }
}
