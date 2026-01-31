using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.Subscription.Persistence;

public class Repository<T, TKey> : DbContextRepository<SubscriptionDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(SubscriptionDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
