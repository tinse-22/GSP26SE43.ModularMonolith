using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Persistence;

public class Repository<T, TKey> : DbContextRepository<ApiDocumentationDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(ApiDocumentationDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
