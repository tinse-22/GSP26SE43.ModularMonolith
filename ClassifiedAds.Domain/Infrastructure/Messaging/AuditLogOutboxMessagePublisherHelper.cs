using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.Contracts.AuditLog.Services;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Domain.Infrastructure.Messaging;

public static class AuditLogOutboxMessagePublisherHelper
{
    public static async Task HandleAuditLogEntryCreatedAsync(
        PublishingOutboxMessage outbox,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken = default)
    {
        if (outbox == null)
        {
            throw new ArgumentNullException(nameof(outbox));
        }

        if (auditLogService == null)
        {
            throw new ArgumentNullException(nameof(auditLogService));
        }

        if (string.IsNullOrWhiteSpace(outbox.Payload))
        {
            return;
        }

        var logEntry = JsonSerializer.Deserialize<AuditLogEntryDTO>(outbox.Payload);
        if (logEntry == null)
        {
            return;
        }

        await auditLogService.AddAsync(logEntry, outbox.Id);
    }
}
