using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.Contracts.AuditLog.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.AuditLog.Entities;
using ClassifiedAds.Modules.AuditLog.Queries;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.AuditLog.Services;

public class AuditLogService : CrudService<AuditLogEntry>, IAuditLogService
{
    private readonly IRepository<IdempotentRequest, Guid> _idempotentRequestRepository;

    public AuditLogService(IRepository<AuditLogEntry, Guid> repository,
        Dispatcher dispatcher,
        IRepository<IdempotentRequest, Guid> idempotentRequestRepository)
        : base(repository, dispatcher)
    {
        _idempotentRequestRepository = idempotentRequestRepository;
    }

    public async Task AddAsync(AuditLogEntryDTO dto, string requestId)
    {
        var requestType = "ADD_AUDIT_LOG_ENTRY";
        var uow = _idempotentRequestRepository.UnitOfWork;

        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                var inserted = await TryInsertIdempotentRequestAsync(requestType, requestId, ct);
                if (!inserted)
                {
                    return;
                }

                await AddOrUpdateAsync(new AuditLogEntry
                {
                    UserId = dto.UserId,
                    CreatedDateTime = dto.CreatedDateTime,
                    Action = dto.Action,
                    ObjectId = dto.ObjectId,
                    Log = dto.Log,
                }, ct);

                await uow.SaveChangesAsync(ct);
            });
        }
        catch (DbUpdateException ex) when (IsDuplicateIdempotentRequestException(ex))
        {
            return;
        }
    }

    private async Task<bool> TryInsertIdempotentRequestAsync(string requestType, string requestId, CancellationToken cancellationToken)
    {
        var existingRequest = await _idempotentRequestRepository.FirstOrDefaultAsync(
            _idempotentRequestRepository.GetQueryableSet()
                .Where(x => x.RequestType == requestType && x.RequestId == requestId));

        if (existingRequest != null)
        {
            return false;
        }

        await _idempotentRequestRepository.AddAsync(new IdempotentRequest
        {
            RequestType = requestType,
            RequestId = requestId,
        }, cancellationToken);

        await _idempotentRequestRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool IsDuplicateIdempotentRequestException(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(
                postgresException.ConstraintName,
                "IX_IdempotentRequests_RequestType_RequestId",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<AuditLogEntryDTO>> GetAuditLogEntriesAsync(AuditLogEntryQueryOptions query)
    {
        var logs = await _dispatcher.DispatchAsync(new GetAuditEntriesQuery
        {
            UserId = query.UserId,
            ObjectId = query.ObjectId,
            AsNoTracking = query.AsNoTracking,
        });

        return logs;
    }
}
