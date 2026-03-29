using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Persistence;

public class PasswordHistoryRepository : Repository<PasswordHistory, Guid>, IPasswordHistoryRepository
{
    public PasswordHistoryRepository(IdentityDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }

    public async Task<IReadOnlyList<PasswordHistory>> GetRecentByUserIdAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryableSet()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedDateTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task AddPasswordHistoryAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        var entry = new PasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PasswordHash = passwordHash,
        };

        await AddOrUpdateAsync(entry, cancellationToken);
    }

    public async Task<int> CleanupOldEntriesAsync(
        Guid userId,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        // Get IDs of entries to keep
        var idsToKeep = await GetQueryableSet()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedDateTime)
            .Take(keepCount)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // Delete entries not in the keep list
        var entriesToDelete = await GetQueryableSet()
            .Where(x => x.UserId == userId && !idsToKeep.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var entry in entriesToDelete)
        {
            Delete(entry);
        }

        await UnitOfWork.SaveChangesAsync(cancellationToken);

        return entriesToDelete.Count;
    }

    public async Task<DateTimeOffset?> GetLastPasswordChangeDateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryableSet()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedDateTime)
            .Select(x => (DateTimeOffset?)x.CreatedDateTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
