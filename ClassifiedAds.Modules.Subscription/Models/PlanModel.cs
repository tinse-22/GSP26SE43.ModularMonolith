using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PlanModel
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public decimal? PriceMonthly { get; set; }

    public decimal? PriceYearly { get; set; }

    public string Currency { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public List<PlanLimitModel> Limits { get; set; } = new List<PlanLimitModel>();
}
