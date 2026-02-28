using ClassifiedAds.Application;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.OutboxMessagePublishers;

public class SpecOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<SpecOutboxMessagePublisher> _logger;

    public SpecOutboxMessagePublisher(Dispatcher dispatcher, ILogger<SpecOutboxMessagePublisher> logger)
    {
        _dispatcher = dispatcher;
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

    public async Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recorded spec event {EventType} (OutboxId: {OutboxId}) for cross-module integration.",
            outbox.EventType,
            outbox.Id);

        if (string.Equals(outbox.EventType, EventTypeConstants.SpecUploaded, StringComparison.Ordinal))
        {
            await HandleSpecUploadedAsync(outbox, cancellationToken);
        }
    }

    private async Task HandleSpecUploadedAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken)
    {
        Guid specId;

        try
        {
            using var doc = JsonDocument.Parse(outbox.Payload);
            var root = doc.RootElement;

            // The outbox payload is the serialized ApiSpecification entity
            if (root.TryGetProperty("Id", out var idElement))
            {
                specId = idElement.GetGuid();
            }
            else if (root.TryGetProperty("id", out var idLowerElement))
            {
                specId = idLowerElement.GetGuid();
            }
            else
            {
                _logger.LogWarning("SPEC_UPLOADED event payload does not contain SpecId. OutboxId={OutboxId}", outbox.Id);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize SPEC_UPLOADED payload. OutboxId={OutboxId}", outbox.Id);
            return;
        }

        _logger.LogInformation("Dispatching ParseUploadedSpecificationCommand for SpecId={SpecId}", specId);

        await _dispatcher.DispatchAsync(
            new ParseUploadedSpecificationCommand { SpecificationId = specId },
            cancellationToken);
    }
}
