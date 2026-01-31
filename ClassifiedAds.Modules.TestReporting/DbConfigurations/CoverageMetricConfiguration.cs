using ClassifiedAds.Modules.TestReporting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestReporting.DbConfigurations;

public class CoverageMetricConfiguration : IEntityTypeConfiguration<CoverageMetric>
{
    public void Configure(EntityTypeBuilder<CoverageMetric> builder)
    {
        builder.ToTable("CoverageMetrics");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CoveragePercent).HasPrecision(5, 2);
        builder.Property(x => x.ByMethod).HasColumnType("jsonb");
        builder.Property(x => x.ByTag).HasColumnType("jsonb");
        builder.Property(x => x.UncoveredPaths).HasColumnType("jsonb");

        builder.HasIndex(x => x.TestRunId);
    }
}
