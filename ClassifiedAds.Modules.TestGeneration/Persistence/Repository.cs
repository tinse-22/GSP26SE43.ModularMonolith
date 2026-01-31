using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Persistence;

public class Repository<T, TKey> : DbContextRepository<TestGenerationDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(TestGenerationDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
