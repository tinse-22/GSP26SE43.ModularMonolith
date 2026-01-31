using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class UsageTrackingConfiguration : IEntityTypeConfiguration<UsageTracking>
{
    public void Configure(EntityTypeBuilder<UsageTracking> builder)
    {
        builder.ToTable("UsageTrackings");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.StorageUsedMB).HasPrecision(10, 2);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.PeriodStart, x.PeriodEnd }).IsUnique();
    }
}
