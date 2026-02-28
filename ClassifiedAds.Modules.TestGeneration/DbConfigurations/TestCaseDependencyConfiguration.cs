using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseDependencyConfiguration : IEntityTypeConfiguration<Entities.TestCaseDependency>
{
    public void Configure(EntityTypeBuilder<Entities.TestCaseDependency> builder)
    {
        builder.ToTable("TestCaseDependencies");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => new { x.TestCaseId, x.DependsOnTestCaseId }).IsUnique();
        builder.HasIndex(x => x.DependsOnTestCaseId);

        builder.HasOne(x => x.TestCase)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOnTestCase)
            .WithMany()
            .HasForeignKey(x => x.DependsOnTestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
