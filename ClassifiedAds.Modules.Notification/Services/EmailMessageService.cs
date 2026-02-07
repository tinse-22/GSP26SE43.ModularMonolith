using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Modules.Notification.EmailQueue;
using ClassifiedAds.Modules.Notification.Entities;
using ClassifiedAds.Modules.Notification.Persistence;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.Services;

public class EmailMessageService : CrudService<EmailMessage>, IEmailMessageService
{
    private readonly IEmailQueueWriter _queueWriter;
    private readonly ILogger<EmailMessageService> _logger;

    public EmailMessageService(
        IEmailMessageRepository repository,
        Dispatcher dispatcher,
        IEmailQueueWriter queueWriter,
        ILogger<EmailMessageService> logger)
        : base(repository, dispatcher)
    {
        _queueWriter = queueWriter;
        _logger = logger;
    }

    public async Task CreateEmailMessageAsync(EmailMessageDTO emailMessage)
    {
        // 1. Persist to database first (durability guarantee)
        var entity = new EmailMessage
        {
            From = emailMessage.From,
            Tos = emailMessage.Tos,
            CCs = emailMessage.CCs,
            BCCs = emailMessage.BCCs,
            Subject = emailMessage.Subject,
            Body = emailMessage.Body,
        };

        await AddOrUpdateAsync(entity);

        // 2. Enqueue to in-memory Channel for fast async processing
        try
        {
            var enqueued = await _queueWriter.EnqueueAsync(new EmailQueueItem
            {
                EmailMessageId = entity.Id,
                Attempt = 1,
                EnqueuedAtUtc = DateTimeOffset.UtcNow,
            });

            if (enqueued)
            {
                _logger.LogDebug("Email {EmailId} persisted and enqueued for delivery.", entity.Id);
            }
            else
            {
                _logger.LogWarning("Email {EmailId} persisted but channel is closed. DB sweep will pick it up.", entity.Id);
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: the DB sweep worker will find it and enqueue later
            _logger.LogWarning(ex, "Failed to enqueue email {EmailId}. DB sweep will recover.", entity.Id);
        }
    }
}
