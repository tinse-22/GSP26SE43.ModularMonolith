using System.Collections.Generic;

namespace ClassifiedAds.Modules.Subscription.Models;

public class CreateUpdatePlanModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public decimal? PriceMonthly { get; set; }

    public decimal? PriceYearly { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public List<PlanLimitModel> Limits { get; set; } = [];
}
