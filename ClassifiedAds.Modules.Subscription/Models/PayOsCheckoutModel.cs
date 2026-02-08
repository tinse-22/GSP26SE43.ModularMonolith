using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PayOsCheckoutRequestModel
{
    [Required]
    public Guid IntentId { get; set; }

    [StringLength(500)]
    public string ReturnUrl { get; set; }
}

public class PayOsCheckoutResponseModel
{
    public string CheckoutUrl { get; set; }

    public long OrderCode { get; set; }
}