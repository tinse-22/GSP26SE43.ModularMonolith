using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class SrsRequirementConfiguration : IEntityTypeConfiguration<SrsRequirement>
{
    public void Configure(EntityTypeBuilder<SrsRequirement> builder)
    {
        builder.ToTable("SrsRequirements");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RequirementCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.RequirementType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // JSON columns — each is a jsonb array
        builder.Property(x => x.TestableConstraints)
            .HasColumnType("jsonb");

        builder.Property(x => x.Assumptions)
            .HasColumnType("jsonb");

        builder.Property(x => x.Ambiguities)
            .HasColumnType("jsonb");

        // Phase 1.5: refined output after clarification round
        builder.Property(x => x.RefinedConstraints)
            .HasColumnType("jsonb");

        builder.Property(x => x.MappedEndpointPath)
            .HasMaxLength(500);

        builder.Property(x => x.RefinementRound)
            .HasDefaultValue(0);

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.SrsDocumentId);
        builder.HasIndex(x => x.EndpointId);
        builder.HasIndex(x => x.ReviewedById);
        builder.HasIndex(x => new { x.SrsDocumentId, x.DisplayOrder });
        builder.HasIndex(x => new { x.SrsDocumentId, x.RequirementCode }).IsUnique();

        // FK to SrsDocument
        builder.HasOne(x => x.SrsDocument)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.SrsDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
