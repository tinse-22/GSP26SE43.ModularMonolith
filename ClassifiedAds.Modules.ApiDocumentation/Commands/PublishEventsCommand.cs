using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

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
        var events = await GetPendingEventsAsync(cancellationToken);

        foreach (var eventLog in events)
        {
            var outbox = new PublishingOutboxMessage
            {
                Id = eventLog.Id.ToString(),
                EventType = eventLog.EventType,
                EventSource = typeof(PublishEventsCommand).Assembly.GetName().Name,
                Payload = eventLog.Payload,
                ActivityId = eventLog.ActivityId,
            };

            try
            {
                await _messageBus.SendAsync(outbox, cancellationToken);

                eventLog.Published = true;
                eventLog.UpdatedDateTime = _dateTimeProvider.OffsetUtcNow;
                await _outboxMessageRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No publisher registered"))
            {
                _logger.LogError(ex,
                    "Cannot publish outbox event {EventType} (ID: {OutboxId}) because no matching publisher was registered. Event will be retried.",
                    eventLog.EventType, eventLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error while publishing outbox event {EventType} (ID: {OutboxId})",
                    eventLog.EventType, eventLog.Id);
                throw;
            }
        }

        command.SentEventsCount = events.Count(x => x.Published);
    }

    private async Task<List<OutboxMessage>> GetPendingEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _outboxMessageRepository.GetQueryableSet()
                .Where(x => !x.Published)
                .OrderBy(x => x.CreatedDateTime)
                .Take(50)
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning(ex,
                "Skipping ApiDocumentation outbox publishing because table apidoc.\"OutboxMessages\" does not exist. Run database migrations.");
            return new List<OutboxMessage>();
        }
        catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
        {
            _logger.LogWarning(ex,
                "Skipping ApiDocumentation outbox publishing because querying apidoc.\"OutboxMessages\" timed out. The worker will retry on the next iteration.");
            return new List<OutboxMessage>();
        }
    }

    private static bool IsTransientDatabaseTimeout(Exception exception)
    {
        return HasException<TimeoutException>(exception)
            && (exception is InvalidOperationException || HasException<NpgsqlException>(exception));
    }

    private static bool HasException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is TException)
            {
                return true;
            }
        }

        return false;
    }
}
