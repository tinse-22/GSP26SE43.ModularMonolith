using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Constants;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.OutboxMessagePublishers;

public class PlanOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly ILogger<PlanOutboxMessagePublisher> _logger;

    public PlanOutboxMessagePublisher(ILogger<PlanOutboxMessagePublisher> logger)
    {
        _logger = logger;
    }

    public static string[] CanHandleEventTypes()
    {
        return new[]
        {
            EventTypeConstants.PlanCreated,
            EventTypeConstants.PlanUpdated,
            EventTypeConstants.PlanDeleted,
        };
    }

    public static string CanHandleEventSource()
    {
        return typeof(PublishEventsCommand).Assembly.GetName().Name;
    }

    public Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recorded plan event {EventType} (OutboxId: {OutboxId}) for cross-module integration.",
            outbox.EventType,
            outbox.Id);

        return Task.CompletedTask;
    }
}
