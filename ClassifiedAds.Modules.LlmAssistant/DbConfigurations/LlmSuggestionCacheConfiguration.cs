using ClassifiedAds.Modules.LlmAssistant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.LlmAssistant.DbConfigurations;

public class LlmSuggestionCacheConfiguration : IEntityTypeConfiguration<LlmSuggestionCache>
{
    public void Configure(EntityTypeBuilder<LlmSuggestionCache> builder)
    {
        builder.ToTable("LlmSuggestionCaches");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.CacheKey).HasMaxLength(500);
        builder.Property(x => x.Suggestions).HasColumnType("jsonb");

        builder.HasIndex(x => x.EndpointId);
        builder.HasIndex(x => x.CacheKey);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
