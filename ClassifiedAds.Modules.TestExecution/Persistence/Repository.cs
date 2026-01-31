using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.TestExecution.Persistence;

public class Repository<T, TKey> : DbContextRepository<TestExecutionDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(TestExecutionDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
