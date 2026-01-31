using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.TestReporting.Persistence;

public class Repository<T, TKey> : DbContextRepository<TestReportingDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(TestReportingDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
