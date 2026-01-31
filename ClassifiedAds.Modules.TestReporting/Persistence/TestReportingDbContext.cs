using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Persistence;

public class TestReportingDbContext : DbContextUnitOfWork<TestReportingDbContext>
{
    public TestReportingDbContext(DbContextOptions<TestReportingDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestReport> TestReports { get; set; }

    public DbSet<CoverageMetric> CoverageMetrics { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("testreporting");
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
