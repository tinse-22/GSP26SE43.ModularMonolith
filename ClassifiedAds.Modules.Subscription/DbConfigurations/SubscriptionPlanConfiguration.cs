using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

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

        builder.HasData(
            new SubscriptionPlan
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Name = "Free",
                DisplayName = "Free",
                Description = "Basic plan for individuals getting started",
                PriceMonthly = 0m,
                PriceYearly = 0m,
                Currency = "VND",
                IsActive = true,
                SortOrder = 1,
                CreatedDateTime = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
            },
            new SubscriptionPlan
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Name = "Pro",
                DisplayName = "Professional",
                Description = "Professional plan for growing teams",
                PriceMonthly = 299000m,
                PriceYearly = 2990000m,
                Currency = "VND",
                IsActive = true,
                SortOrder = 2,
                CreatedDateTime = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
            },
            new SubscriptionPlan
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                Name = "Enterprise",
                DisplayName = "Enterprise",
                Description = "Enterprise plan for large organizations",
                PriceMonthly = 999000m,
                PriceYearly = 9990000m,
                Currency = "VND",
                IsActive = true,
                SortOrder = 3,
                CreatedDateTime = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
            });
    }
}
