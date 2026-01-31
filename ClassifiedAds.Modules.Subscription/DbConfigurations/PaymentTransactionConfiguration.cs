using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.ExternalTxnId).HasMaxLength(200);
        builder.Property(x => x.InvoiceUrl).HasMaxLength(500);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.SubscriptionId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
