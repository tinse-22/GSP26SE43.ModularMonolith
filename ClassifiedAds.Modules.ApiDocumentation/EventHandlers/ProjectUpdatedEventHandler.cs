using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.ExtensionMethods;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Constants;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.EventHandlers;

public class ProjectUpdatedEventHandler : IDomainEventHandler<EntityUpdatedEvent<Project>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRepository<AuditLogEntry, Guid> _auditLogRepository;
    private readonly IRepository<OutboxMessage, Guid> _outboxMessageRepository;

    public ProjectUpdatedEventHandler(ICurrentUser currentUser,
        IRepository<AuditLogEntry, Guid> auditLogRepository,
        IRepository<OutboxMessage, Guid> outboxMessageRepository)
    {
        _currentUser = currentUser;
        _auditLogRepository = auditLogRepository;
        _outboxMessageRepository = outboxMessageRepository;
    }

    public async Task HandleAsync(EntityUpdatedEvent<Project> domainEvent, CancellationToken cancellationToken = default)
    {
        var entity = domainEvent.Entity;
        var eventType = entity.Status == ProjectStatus.Archived
            ? EventTypeConstants.ProjectArchived
            : EventTypeConstants.ProjectUpdated;

        var action = entity.Status == ProjectStatus.Archived
            ? "ARCHIVED_PROJECT"
            : "UPDATED_PROJECT";

        var auditLog = new AuditLogEntry
        {
            UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty,
            CreatedDateTime = domainEvent.EventDateTime,
            Action = action,
            ObjectId = entity.Id.ToString(),
            Log = entity.AsJsonString(),
        };

        await _auditLogRepository.AddOrUpdateAsync(auditLog, cancellationToken);

        await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
        {
            EventType = EventTypeConstants.AuditLogEntryCreated,
            TriggeredById = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty,
            CreatedDateTime = auditLog.CreatedDateTime,
            ObjectId = auditLog.Id.ToString(),
            Payload = auditLog.AsJsonString(),
        }, cancellationToken);

        await _outboxMessageRepository.AddOrUpdateAsync(new OutboxMessage
        {
            EventType = eventType,
            TriggeredById = _currentUser.IsAuthenticated ? _currentUser.UserId : Guid.Empty,
            CreatedDateTime = domainEvent.EventDateTime,
            ObjectId = entity.Id.ToString(),
            Payload = entity.AsJsonString(),
        }, cancellationToken);

        await _auditLogRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
