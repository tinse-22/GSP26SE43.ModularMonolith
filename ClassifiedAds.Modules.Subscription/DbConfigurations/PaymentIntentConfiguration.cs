using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("PaymentIntents");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BillingCycle).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(500);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PlanId);
        builder.HasIndex(x => x.SubscriptionId);
        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.OrderCode)
            .IsUnique()
            .HasFilter("\"OrderCode\" IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.CreatedDateTime });
        builder.HasIndex(x => new { x.Status, x.Purpose });

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}