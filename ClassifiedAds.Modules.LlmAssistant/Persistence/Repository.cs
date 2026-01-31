using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.Persistence.PostgreSQL;

namespace ClassifiedAds.Modules.LlmAssistant.Persistence;

public class Repository<T, TKey> : DbContextRepository<LlmAssistantDbContext, T, TKey>
    where T : Entity<TKey>, IAggregateRoot
{
    public Repository(LlmAssistantDbContext dbContext, IDateTimeProvider dateTimeProvider)
        : base(dbContext, dateTimeProvider)
    {
    }
}
