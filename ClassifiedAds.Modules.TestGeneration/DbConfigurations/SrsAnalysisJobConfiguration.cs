using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class SrsAnalysisJobConfiguration : IEntityTypeConfiguration<SrsAnalysisJob>
{
    public void Configure(EntityTypeBuilder<SrsAnalysisJob> builder)
    {
        builder.ToTable("SrsAnalysisJobs");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.JobType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.QueuedAt)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.ErrorDetails)
            .HasColumnType("text");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.SrsDocumentId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.QueuedAt);
        builder.HasIndex(x => x.TriggeredById);
        builder.HasIndex(x => new { x.SrsDocumentId, x.QueuedAt });

        // FK to SrsDocument
        builder.HasOne(x => x.SrsDocument)
            .WithMany(x => x.AnalysisJobs)
            .HasForeignKey(x => x.SrsDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
