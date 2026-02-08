using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.Subscription.Models;

public class PayOsCreatePaymentRequest
{
    public long OrderCode { get; set; }

    public long Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;

    public string BuyerName { get; set; }

    public string BuyerEmail { get; set; }

    public string BuyerPhone { get; set; }
}

public class PayOsPaymentResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PayOsPaymentResponseData Data { get; set; }
}

public class PayOsPaymentResponseData
{
    [JsonPropertyName("checkoutUrl")]
    public string CheckoutUrl { get; set; }

    [JsonPropertyName("orderCode")]
    public long? OrderCode { get; set; }

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; }
}

public class PayOsGetPaymentResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PayOsGetPaymentData Data { get; set; }
}

public class PayOsGetPaymentData
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("reference")]
    public string Reference { get; set; }

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; set; }

    [JsonPropertyName("checkoutUrl")]
    public string CheckoutUrl { get; set; }
}

public class PayOsWebhookPayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public PayOsWebhookData Data { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class PayOsWebhookData
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; }

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "VND";

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; }

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; }

    [JsonPropertyName("counterAccountBankId")]
    public string CounterAccountBankId { get; set; }

    [JsonPropertyName("counterAccountBankName")]
    public string CounterAccountBankName { get; set; }

    [JsonPropertyName("counterAccountName")]
    public string CounterAccountName { get; set; }

    [JsonPropertyName("counterAccountNumber")]
    public string CounterAccountNumber { get; set; }

    [JsonPropertyName("virtualAccountName")]
    public string VirtualAccountName { get; set; }

    [JsonPropertyName("virtualAccountNumber")]
    public string VirtualAccountNumber { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("desc")]
    public string Desc { get; set; }
}

public enum PayOsWebhookOutcome
{
    Processed = 0,
    Ignored = 1,
}
