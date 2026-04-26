using ClassifiedAds.Modules.TestExecution.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestExecution.DbConfigurations;

public class TestCaseResultConfiguration : IEntityTypeConfiguration<TestCaseResult>
{
    public void Configure(EntityTypeBuilder<TestCaseResult> builder)
    {
        builder.ToTable("TestCaseResults");

        // UUID primary key with auto-generation
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // String properties with constraints
        builder.Property(x => x.Name).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ResolvedUrl).HasMaxLength(2000);
        builder.Property(x => x.ResponseBodyPreview).HasMaxLength(65536);

        // JSONB columns for complex structures (best practice for PostgreSQL)
        builder.Property(x => x.RequestHeaders).HasColumnType("jsonb");
        builder.Property(x => x.ResponseHeaders).HasColumnType("jsonb");
        builder.Property(x => x.FailureReasons).HasColumnType("jsonb");
        builder.Property(x => x.ExtractedVariables).HasColumnType("jsonb");
        builder.Property(x => x.DependencyIds).HasColumnType("jsonb");
        builder.Property(x => x.SkippedBecauseDependencyIds).HasColumnType("jsonb");

        // Indexes for common queries
        builder.HasIndex(x => x.TestRunId);
        builder.HasIndex(x => x.TestCaseId);
        builder.HasIndex(x => x.Status);

        // Composite index for efficient filtering
        builder.HasIndex(x => new { x.TestRunId, x.Status });
        builder.HasIndex(x => new { x.TestRunId, x.OrderIndex });

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
