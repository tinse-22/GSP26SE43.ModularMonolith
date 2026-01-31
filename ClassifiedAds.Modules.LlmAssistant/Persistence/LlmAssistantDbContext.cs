using ClassifiedAds.Modules.LlmAssistant.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Persistence;

public class LlmAssistantDbContext : DbContextUnitOfWork<LlmAssistantDbContext>
{
    public LlmAssistantDbContext(DbContextOptions<LlmAssistantDbContext> options)
        : base(options)
    {
    }

    public DbSet<LlmInteraction> LlmInteractions { get; set; }

    public DbSet<LlmSuggestionCache> LlmSuggestionCaches { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("llmassistant");
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges()
    {
        SetOutboxActivityId();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetOutboxActivityId();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void SetOutboxActivityId()
    {
        var entities = ChangeTracker.Entries<OutboxMessage>();
        foreach (var entity in entities.Where(e => e.State == EntityState.Added))
        {
            var outbox = entity.Entity;

            if (string.IsNullOrWhiteSpace(outbox.ActivityId))
            {
                outbox.ActivityId = System.Diagnostics.Activity.Current?.Id;
            }
        }
    }
}
