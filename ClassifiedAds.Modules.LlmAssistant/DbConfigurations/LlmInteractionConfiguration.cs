using ClassifiedAds.Modules.LlmAssistant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.LlmAssistant.DbConfigurations;

public class LlmInteractionConfiguration : IEntityTypeConfiguration<LlmInteraction>
{
    public void Configure(EntityTypeBuilder<LlmInteraction> builder)
    {
        builder.ToTable("LlmInteractions");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ModelUsed).HasMaxLength(100);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.InteractionType);
        builder.HasIndex(x => x.CreatedDateTime);
    }
}
