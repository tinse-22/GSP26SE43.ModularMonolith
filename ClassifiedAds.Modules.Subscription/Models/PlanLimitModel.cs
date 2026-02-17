using ClassifiedAds.Contracts.Subscription.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PlanLimitModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Loại giới hạn là bắt buộc.")]
    [JsonConverter(typeof(LimitTypeJsonConverter))]
    public LimitType? LimitType { get; set; }

    public int? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }
}
