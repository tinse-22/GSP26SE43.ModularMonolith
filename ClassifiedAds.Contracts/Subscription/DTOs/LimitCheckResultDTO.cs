namespace ClassifiedAds.Contracts.Subscription.DTOs;

public class LimitCheckResultDTO
{
    public bool IsAllowed { get; set; }

    public int? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }

    public decimal CurrentUsage { get; set; }

    public string DenialReason { get; set; }
}
