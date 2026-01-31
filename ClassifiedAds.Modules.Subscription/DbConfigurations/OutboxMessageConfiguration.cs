using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(x => new { x.Published, x.CreatedDateTime });
        builder.HasIndex(x => x.CreatedDateTime);
    }
}

public class ArchivedOutboxMessageConfiguration : IEntityTypeConfiguration<ArchivedOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ArchivedOutboxMessage> builder)
    {
        builder.ToTable("ArchivedOutboxMessages");
        builder.HasIndex(x => x.CreatedDateTime);
    }
}
