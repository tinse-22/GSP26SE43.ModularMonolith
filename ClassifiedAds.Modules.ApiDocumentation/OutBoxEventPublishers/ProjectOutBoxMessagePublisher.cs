using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.OutboxMessagePublishers;

public class ProjectOutboxMessagePublisher : IOutboxMessagePublisher
{
    private readonly ILogger<ProjectOutboxMessagePublisher> _logger;

    public ProjectOutboxMessagePublisher(ILogger<ProjectOutboxMessagePublisher> logger)
    {
        _logger = logger;
    }

    public static string[] CanHandleEventTypes()
    {
        return new[]
        {
            EventTypeConstants.ProjectCreated,
            EventTypeConstants.ProjectUpdated,
            EventTypeConstants.ProjectDeleted,
            EventTypeConstants.ProjectArchived,
        };
    }

    public static string CanHandleEventSource()
    {
        return typeof(PublishEventsCommand).Assembly.GetName().Name;
    }

    public Task HandleAsync(PublishingOutboxMessage outbox, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recorded project event {EventType} (OutboxId: {OutboxId}) for cross-module integration.",
            outbox.EventType,
            outbox.Id);

        return Task.CompletedTask;
    }
}
