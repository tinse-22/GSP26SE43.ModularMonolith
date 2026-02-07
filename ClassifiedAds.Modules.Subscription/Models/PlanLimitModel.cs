using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PlanLimitModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Loại giới hạn là bắt buộc.")]
    public string LimitType { get; set; }

    public int? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }
}
