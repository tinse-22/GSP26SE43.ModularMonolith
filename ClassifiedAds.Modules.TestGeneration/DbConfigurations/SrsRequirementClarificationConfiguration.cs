using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class SrsRequirementClarificationConfiguration : IEntityTypeConfiguration<SrsRequirementClarification>
{
    public void Configure(EntityTypeBuilder<SrsRequirementClarification> builder)
    {
        builder.ToTable("SrsRequirementClarifications");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AmbiguitySource)
            .HasColumnType("text");

        builder.Property(x => x.Question)
            .HasColumnType("text")
            .IsRequired();

        // JSON array of suggested options from LLM
        builder.Property(x => x.SuggestedOptions)
            .HasColumnType("jsonb");

        builder.Property(x => x.UserAnswer)
            .HasColumnType("text");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.SrsRequirementId);
        builder.HasIndex(x => new { x.SrsRequirementId, x.IsAnswered });
        builder.HasIndex(x => new { x.SrsRequirementId, x.DisplayOrder });
        builder.HasIndex(x => x.AnsweredById);

        // FK to SrsRequirement
        builder.HasOne(x => x.SrsRequirement)
            .WithMany(x => x.Clarifications)
            .HasForeignKey(x => x.SrsRequirementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
