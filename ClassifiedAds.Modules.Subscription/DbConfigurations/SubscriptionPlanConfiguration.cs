using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.PriceMonthly).HasPrecision(10, 2);
        builder.Property(x => x.PriceYearly).HasPrecision(10, 2);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
