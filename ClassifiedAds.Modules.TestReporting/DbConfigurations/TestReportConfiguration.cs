using ClassifiedAds.Modules.TestReporting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestReporting.DbConfigurations;

public class TestReportConfiguration : IEntityTypeConfiguration<TestReport>
{
    public void Configure(EntityTypeBuilder<TestReport> builder)
    {
        builder.ToTable("TestReports");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.TestRunId);
        builder.HasIndex(x => x.GeneratedById);
        builder.HasIndex(x => x.FileId);
    }
}
