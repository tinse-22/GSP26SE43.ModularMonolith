using ClassifiedAds.Modules.Subscription.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Services;

public interface IPayOsService
{
    Task<string> CreatePaymentLinkAsync(PayOsCreatePaymentRequest request, CancellationToken ct = default);

    bool VerifyWebhookSignature(PayOsWebhookPayload payload, string rawBody);

    Task<PayOsGetPaymentData> GetPaymentInfoAsync(long orderCode, CancellationToken ct = default);
}