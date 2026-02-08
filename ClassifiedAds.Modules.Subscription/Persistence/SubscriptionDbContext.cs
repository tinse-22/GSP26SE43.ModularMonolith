using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Persistence;

public class SubscriptionDbContext : DbContextUnitOfWork<SubscriptionDbContext>
{
    public SubscriptionDbContext(DbContextOptions<SubscriptionDbContext> options)
        : base(options)
    {
    }

    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public DbSet<PlanLimit> PlanLimits { get; set; }

    public DbSet<UserSubscription> UserSubscriptions { get; set; }

    public DbSet<SubscriptionHistory> SubscriptionHistories { get; set; }

    public DbSet<UsageTracking> UsageTrackings { get; set; }

    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<PaymentIntent> PaymentIntents { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("subscription");
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
