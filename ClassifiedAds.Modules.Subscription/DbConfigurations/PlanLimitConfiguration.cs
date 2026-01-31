using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class PlanLimitConfiguration : IEntityTypeConfiguration<PlanLimit>
{
    public void Configure(EntityTypeBuilder<PlanLimit> builder)
    {
        builder.ToTable("PlanLimits");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasIndex(x => x.PlanId);
        builder.HasIndex(x => new { x.PlanId, x.LimitType }).IsUnique();

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
