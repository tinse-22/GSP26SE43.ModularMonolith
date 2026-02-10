using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.OutboxMessagePublishers;

public class SpecOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly ILogger<SpecOutboxMessagePublisher> _logger;

    public SpecOutboxMessagePublisher(ILogger<SpecOutboxMessagePublisher> logger)
    {
        _logger = logger;
    }

    public static string[] CanHandleEventTypes()
    {
        return new[]
        {
            EventTypeConstants.SpecUploaded,
            EventTypeConstants.SpecActivated,
            EventTypeConstants.SpecDeactivated,
            EventTypeConstants.SpecDeleted,
        };
    }

    public static string CanHandleEventSource()
    {
        return typeof(PublishEventsCommand).Assembly.GetName().Name;
    }

    public Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recorded spec event {EventType} (OutboxId: {OutboxId}) for cross-module integration.",
            outbox.EventType,
            outbox.Id);

        return Task.CompletedTask;
    }
}
