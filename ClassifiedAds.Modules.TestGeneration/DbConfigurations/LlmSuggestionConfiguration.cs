using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class LlmSuggestionConfiguration : IEntityTypeConfiguration<Entities.LlmSuggestion>
{
    public void Configure(EntityTypeBuilder<Entities.LlmSuggestion> builder)
    {
        builder.ToTable("LlmSuggestions");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        // String fields
        builder.Property(x => x.SuggestedName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SuggestedDescription)
            .HasColumnType("text");

        builder.Property(x => x.CacheKey)
            .HasMaxLength(100);

        builder.Property(x => x.ReviewNotes)
            .HasColumnType("text");

        builder.Property(x => x.LlmModel)
            .HasMaxLength(100);

        // Enum conversions (stored as string)
        builder.Property(x => x.SuggestionType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TestType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ReviewStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // JSON columns
        builder.Property(x => x.SuggestedRequest)
            .HasColumnType("jsonb");

        builder.Property(x => x.SuggestedExpectation)
            .HasColumnType("jsonb");

        builder.Property(x => x.SuggestedVariables)
            .HasColumnType("jsonb");

        builder.Property(x => x.SuggestedTags)
            .HasColumnType("jsonb");

        builder.Property(x => x.ModifiedContent)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(x => new { x.TestSuiteId, x.ReviewStatus });
        builder.HasIndex(x => x.EndpointId);
        builder.HasIndex(x => x.CacheKey);
        builder.HasIndex(x => x.AppliedTestCaseId);
        builder.HasIndex(x => x.ReviewedById);

        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.HasIndex(x => new { x.TestSuiteId, x.IsDeleted });
        builder.HasIndex(x => x.DeletedById);

        // FK to TestSuite (cascade delete, no reverse navigation collection)
        builder.HasOne(x => x.TestSuite)
            .WithMany()
            .HasForeignKey(x => x.TestSuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
