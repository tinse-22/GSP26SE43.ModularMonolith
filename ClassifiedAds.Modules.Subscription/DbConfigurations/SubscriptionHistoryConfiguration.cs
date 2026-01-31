using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("SubscriptionHistories");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.SubscriptionId);

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.OldPlan)
            .WithMany()
            .HasForeignKey(x => x.OldPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NewPlan)
            .WithMany()
            .HasForeignKey(x => x.NewPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
