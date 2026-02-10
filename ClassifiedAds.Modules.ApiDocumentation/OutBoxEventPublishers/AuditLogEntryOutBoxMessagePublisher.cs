using ClassifiedAds.Contracts.AuditLog.Services;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.OutboxMessagePublishers;

public class AuditLogEntryOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly IAuditLogService _externalAuditLogService;

    public AuditLogEntryOutboxMessagePublisher(IAuditLogService externalAuditLogService)
    {
        _externalAuditLogService = externalAuditLogService;
    }

    public static string[] CanHandleEventTypes()
    {
        return new[] { EventTypeConstants.AuditLogEntryCreated };
    }

    public static string CanHandleEventSource()
    {
        return typeof(PublishEventsCommand).Assembly.GetName().Name;
    }

    public async Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        if (outbox.EventType != EventTypeConstants.AuditLogEntryCreated)
        {
            return;
        }

        var logEntry = JsonSerializer.Deserialize<Contracts.AuditLog.DTOs.AuditLogEntryDTO>(outbox.Payload);
        await _externalAuditLogService.AddAsync(logEntry, outbox.Id);
    }
}
