using ClassifiedAds.Modules.Subscription.Entities;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class CreateSubscriptionPaymentModel
{
    [Required]
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
}