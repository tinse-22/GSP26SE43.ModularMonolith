using ClassifiedAds.Modules.Subscription.Entities;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class AddPaymentTransactionModel
{
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal? Amount { get; set; }

    [RegularExpression(@"^[A-Za-z]{3}$")]
    public string Currency { get; set; } = "USD";

    public PaymentStatus Status { get; set; } = PaymentStatus.Succeeded;

    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; }

    [StringLength(20)]
    public string Provider { get; set; }

    [StringLength(200)]
    public string ProviderRef { get; set; }

    [StringLength(200)]
    public string ExternalTxnId { get; set; }

    [StringLength(500)]
    public string InvoiceUrl { get; set; }

    [StringLength(1000)]
    public string FailureReason { get; set; }
}
