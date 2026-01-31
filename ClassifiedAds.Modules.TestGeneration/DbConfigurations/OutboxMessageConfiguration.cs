using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<Entities.OutboxMessage>
{
    public void Configure(EntityTypeBuilder<Entities.OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
    }
}
