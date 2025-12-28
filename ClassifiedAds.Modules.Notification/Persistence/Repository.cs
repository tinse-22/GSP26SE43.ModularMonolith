using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.Notification.Persistence;

public class Repository<T, TKey> : DbContextRepository<NotificationDbContext, T, TKey>
where T : Entity<TKey>, IAggregateRoot
{
    public Repository(NotificationDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
