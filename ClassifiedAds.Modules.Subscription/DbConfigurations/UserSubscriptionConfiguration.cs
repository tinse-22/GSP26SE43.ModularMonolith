using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("UserSubscriptions");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ExternalSubId).HasMaxLength(200);
        builder.Property(x => x.ExternalCustId).HasMaxLength(200);

        builder.Property(x => x.SnapshotPriceMonthly).HasPrecision(10, 2);
        builder.Property(x => x.SnapshotPriceYearly).HasPrecision(10, 2);
        builder.Property(x => x.SnapshotCurrency).HasMaxLength(3);
        builder.Property(x => x.SnapshotPlanName).HasMaxLength(200);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PlanId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
