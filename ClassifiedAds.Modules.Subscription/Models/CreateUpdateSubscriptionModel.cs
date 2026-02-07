using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class CreateUpdateSubscriptionModel
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid PlanId { get; set; }

    public BillingCycle? BillingCycle { get; set; }

    public bool IsTrial { get; set; }

    [Range(1, 365)]
    public int TrialDays { get; set; } = 14;

    public bool AutoRenew { get; set; } = true;

    public DateOnly? StartDate { get; set; }

    [StringLength(200)]
    public string ExternalSubId { get; set; }

    [StringLength(200)]
    public string ExternalCustId { get; set; }

    [StringLength(500)]
    public string ChangeReason { get; set; }
}
