using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class SrsDocumentConfiguration : IEntityTypeConfiguration<SrsDocument>
{
    public void Configure(EntityTypeBuilder<SrsDocument> builder)
    {
        builder.ToTable("SrsDocuments");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Title)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AnalysisStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.RawContent)
            .HasColumnType("text");

        builder.Property(x => x.ParsedMarkdown)
            .HasColumnType("text");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        // Indexes
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.CreatedById);
        builder.HasIndex(x => new { x.ProjectId, x.IsDeleted });
        builder.HasIndex(x => new { x.ProjectId, x.AnalysisStatus });
    }
}
