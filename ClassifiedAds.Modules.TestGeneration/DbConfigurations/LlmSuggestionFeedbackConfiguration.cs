using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class LlmSuggestionFeedbackConfiguration : IEntityTypeConfiguration<LlmSuggestionFeedback>
{
    public void Configure(EntityTypeBuilder<LlmSuggestionFeedback> builder)
    {
        builder.ToTable("LlmSuggestionFeedbacks");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.FeedbackSignal)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnType("text");

        builder.HasIndex(x => new { x.SuggestionId, x.UserId })
            .IsUnique();

        builder.HasIndex(x => new { x.TestSuiteId, x.EndpointId });
        builder.HasIndex(x => x.FeedbackSignal);

        builder.HasOne(x => x.Suggestion)
            .WithMany()
            .HasForeignKey(x => x.SuggestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
