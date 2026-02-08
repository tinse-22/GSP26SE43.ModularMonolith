using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Constants;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.OutboxMessagePublishers;

public class PaymentSubscriptionOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly ILogger<PaymentSubscriptionOutboxMessagePublisher> _logger;

    public PaymentSubscriptionOutboxMessagePublisher(ILogger<PaymentSubscriptionOutboxMessagePublisher> logger)
    {
        _logger = logger;
    }

    public static string[] CanHandleEventTypes()
    {
        return new[]
        {
            EventTypeConstants.PaymentIntentCreated,
            EventTypeConstants.PaymentCheckoutLinkCreated,
            EventTypeConstants.PaymentCheckoutReconcileRequested,
            EventTypeConstants.PaymentIntentStatusChanged,
            EventTypeConstants.PaymentTransactionCreated,
            EventTypeConstants.SubscriptionChanged,
        };
    }

    public static string CanHandleEventSource()
    {
        return typeof(PublishEventsCommand).Assembly.GetName().Name;
    }

    public Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processed Subscription payment/subscription outbox event {EventType} (OutboxId: {OutboxId}).",
            outbox.EventType,
            outbox.Id);

        return Task.CompletedTask;
    }
}
