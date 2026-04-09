using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestGenerationJobConfiguration : IEntityTypeConfiguration<TestGenerationJob>
{
    public void Configure(EntityTypeBuilder<TestGenerationJob> builder)
    {
        builder.ToTable("TestGenerationJobs");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.QueuedAt)
            .IsRequired();

        builder.Property(x => x.WebhookName)
            .HasMaxLength(100);

        builder.Property(x => x.WebhookUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CallbackUrl)
            .HasMaxLength(500);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.ErrorDetails)
            .HasColumnType("text");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.QueuedAt);
        builder.HasIndex(x => x.TriggeredById);

        // Composite index for finding latest job per suite
        builder.HasIndex(x => new { x.TestSuiteId, x.QueuedAt });

        // Foreign key
        builder.HasOne(x => x.TestSuite)
            .WithMany()
            .HasForeignKey(x => x.TestSuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
