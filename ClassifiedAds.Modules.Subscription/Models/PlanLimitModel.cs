using System;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PlanLimitModel
{
    public Guid? Id { get; set; }

    public string LimitType { get; set; }

    public int? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }
}
