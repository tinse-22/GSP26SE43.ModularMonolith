using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Product.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Product.Commands;

public class PublishEventsCommand : ICommand
{
    public int SentEventsCount { get; set; }
}

public class PublishEventsCommandHandler : ICommandHandler<PublishEventsCommand>
{
    private readonly ILogger<PublishEventsCommandHandler> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRepository<OutboxMessage, Guid> _outboxMessageRepository;
    private readonly IMessageBus _messageBus;

    public PublishEventsCommandHandler(ILogger<PublishEventsCommandHandler> logger,
        IDateTimeProvider dateTimeProvider,
        IRepository<OutboxMessage, Guid> outboxMessageRepository,
        IMessageBus messageBus)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _outboxMessageRepository = outboxMessageRepository;
        _messageBus = messageBus;
    }

    public async Task HandleAsync(PublishEventsCommand command, CancellationToken cancellationToken = default)
    {
        var events = _outboxMessageRepository.GetQueryableSet()
            .Where(x => !x.Published)
            .OrderBy(x => x.CreatedDateTime)
            .Take(50)
            .ToList();

        foreach (var eventLog in events)
        {
            var outbox = new PublishingOutboxMessage
            {
                Id = eventLog.Id.ToString(),
                EventType = eventLog.EventType,
                EventSource = typeof(PublishEventsCommand).Assembly.GetName().Name,
                Payload = eventLog.Payload,
                ActivityId = eventLog.ActivityId
            };

            try
            {
                await _messageBus.SendAsync(outbox, cancellationToken);

                eventLog.Published = true;
                eventLog.UpdatedDateTime = _dateTimeProvider.OffsetNow;
                await _outboxMessageRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No publisher registered"))
            {
                _logger.LogError(ex, 
                    "Failed to publish outbox event {EventType} (ID: {OutboxId}). No publisher registered. Event will be retried.",
                    eventLog.EventType, eventLog.Id);
                // Do NOT mark as Published - let it retry
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Unexpected error publishing outbox event {EventType} (ID: {OutboxId})",
                    eventLog.EventType, eventLog.Id);
                throw; // Re-throw to fail the batch and retry later
            }
        }

        command.SentEventsCount = events.Count(x => x.Published);
    }
}