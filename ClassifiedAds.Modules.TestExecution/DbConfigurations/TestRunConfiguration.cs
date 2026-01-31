using ClassifiedAds.Modules.TestExecution.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestExecution.DbConfigurations;

public class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("TestRuns");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RedisKey).HasMaxLength(200);

        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.EnvironmentId);
        builder.HasIndex(x => x.TriggeredById);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.TestSuiteId, x.RunNumber }).IsUnique();
    }
}
