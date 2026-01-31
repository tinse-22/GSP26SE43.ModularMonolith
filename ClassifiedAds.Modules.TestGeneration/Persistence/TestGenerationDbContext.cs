using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Persistence;

public class TestGenerationDbContext : DbContextUnitOfWork<TestGenerationDbContext>
{
    public TestGenerationDbContext(DbContextOptions<TestGenerationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestSuite> TestSuites { get; set; }
    public DbSet<TestCase> TestCases { get; set; }
    public DbSet<TestCaseRequest> TestCaseRequests { get; set; }
    public DbSet<TestCaseExpectation> TestCaseExpectations { get; set; }
    public DbSet<TestCaseVariable> TestCaseVariables { get; set; }
    public DbSet<TestDataSet> TestDataSets { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("testgen");
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
