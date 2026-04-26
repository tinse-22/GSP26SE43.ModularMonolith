using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Domain.Infrastructure.Messaging;

public static class SpecOutboxMessagePublisherHelper
{
    public static async Task HandleSpecUploadedAsync(
        PublishingOutboxMessage outbox,
        Dispatcher dispatcher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (outbox == null)
        {
            throw new ArgumentNullException(nameof(outbox));
        }

        if (dispatcher == null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (!string.Equals(outbox.EventType, EventTypeConstants.SpecUploaded, StringComparison.Ordinal))
        {
            return;
        }

        logger.LogInformation(
            "Recorded spec event {EventType} (OutboxId: {OutboxId}) for cross-module integration.",
            outbox.EventType,
            outbox.Id);

        Guid specId;

        try
        {
            using var doc = JsonDocument.Parse(outbox.Payload);
            var root = doc.RootElement;

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
                logger.LogWarning("SPEC_UPLOADED event payload does not contain SpecId. OutboxId={OutboxId}", outbox.Id);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize SPEC_UPLOADED payload. OutboxId={OutboxId}", outbox.Id);
            return;
        }

        logger.LogInformation("Dispatching ParseUploadedSpecificationCommand for SpecId={SpecId}", specId);

        await dispatcher.DispatchAsync(
            new ParseUploadedSpecificationCommand { SpecificationId = specId },
            cancellationToken);
    }
}
