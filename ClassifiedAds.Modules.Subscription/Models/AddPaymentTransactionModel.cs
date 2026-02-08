using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class AddPaymentTransactionModel
{
    [StringLength(200)]
    public string ProviderRef { get; set; }

    [StringLength(200)]
    public string ExternalTxnId { get; set; }
}
