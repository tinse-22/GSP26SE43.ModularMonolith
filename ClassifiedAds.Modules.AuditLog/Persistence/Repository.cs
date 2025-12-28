using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.AuditLog.Persistence;

public class Repository<T, TKey> : DbContextRepository<AuditLogDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(AuditLogDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
