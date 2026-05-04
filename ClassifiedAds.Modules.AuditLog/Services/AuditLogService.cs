using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.Contracts.AuditLog.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.AuditLog.Entities;
using ClassifiedAds.Modules.AuditLog.Persistence;
using ClassifiedAds.Modules.AuditLog.Queries;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.AuditLog.Services;

public class AuditLogService : CrudService<AuditLogEntry>, IAuditLogService
{
    private readonly AuditLogDbContext _dbContext;

    public AuditLogService(
        IRepository<AuditLogEntry, Guid> repository,
        Dispatcher dispatcher,
        AuditLogDbContext dbContext)
        : base(repository, dispatcher)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLogEntryDTO dto, string requestId)
    {
        const string requestType = "ADD_AUDIT_LOG_ENTRY";

        await _dbContext.ExecuteInTransactionAsync(async ct =>
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

            await _dbContext.SaveChangesAsync(ct);
        });
    }

    /// <summary>
    /// Atomically inserts an idempotency key using INSERT … ON CONFLICT DO NOTHING.
    /// Returns true if the row was newly inserted (first caller wins),
    /// false if the key already existed (duplicate suppressed at the DB level).
    /// This eliminates the TOCTOU race of a SELECT-then-INSERT pattern and never
    /// raises a 23505 duplicate-key exception.
    /// </summary>
    private async Task<bool> TryInsertIdempotentRequestAsync(string requestType, string requestId, CancellationToken ct)
    {
        var createdDateTime = DateTimeOffset.UtcNow;

        var rowsAffected = await _dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO auditlog."IdempotentRequests" ("RequestType", "RequestId", "CreatedDateTime")
            VALUES ({requestType}, {requestId}, {createdDateTime})
            ON CONFLICT ("RequestType", "RequestId") DO NOTHING
            """,
            ct);

        // 1 = row inserted (this request owns the idempotency slot)
        // 0 = conflict suppressed (another concurrent request already inserted this key)
        return rowsAffected > 0;
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
