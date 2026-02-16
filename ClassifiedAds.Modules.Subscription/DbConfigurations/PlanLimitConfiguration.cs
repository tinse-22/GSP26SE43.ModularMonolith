using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace ClassifiedAds.Modules.Subscription.DbConfigurations;

public class PlanLimitConfiguration : IEntityTypeConfiguration<PlanLimit>
{
    private static readonly DateTimeOffset Epoch = new(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);

    private static readonly Guid FreePlanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProPlanId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid EnterprisePlanId = Guid.Parse("10000000-0000-0000-0000-000000000003");

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

        builder.HasData(
            // ── Free Plan Limits ──
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), PlanId = FreePlanId, LimitType = LimitType.MaxProjects, LimitValue = 1, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), PlanId = FreePlanId, LimitType = LimitType.MaxEndpointsPerProject, LimitValue = 10, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), PlanId = FreePlanId, LimitType = LimitType.MaxTestCasesPerSuite, LimitValue = 20, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), PlanId = FreePlanId, LimitType = LimitType.MaxTestRunsPerMonth, LimitValue = 50, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), PlanId = FreePlanId, LimitType = LimitType.MaxConcurrentRuns, LimitValue = 1, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), PlanId = FreePlanId, LimitType = LimitType.RetentionDays, LimitValue = 7, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), PlanId = FreePlanId, LimitType = LimitType.MaxLlmCallsPerMonth, LimitValue = 10, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000008"), PlanId = FreePlanId, LimitType = LimitType.MaxStorageMB, LimitValue = 100, IsUnlimited = false, CreatedDateTime = Epoch },

            // ── Pro Plan Limits ──
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000011"), PlanId = ProPlanId, LimitType = LimitType.MaxProjects, LimitValue = 10, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000012"), PlanId = ProPlanId, LimitType = LimitType.MaxEndpointsPerProject, LimitValue = 50, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000013"), PlanId = ProPlanId, LimitType = LimitType.MaxTestCasesPerSuite, LimitValue = 100, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000014"), PlanId = ProPlanId, LimitType = LimitType.MaxTestRunsPerMonth, LimitValue = 500, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000015"), PlanId = ProPlanId, LimitType = LimitType.MaxConcurrentRuns, LimitValue = 3, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000016"), PlanId = ProPlanId, LimitType = LimitType.RetentionDays, LimitValue = 30, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000017"), PlanId = ProPlanId, LimitType = LimitType.MaxLlmCallsPerMonth, LimitValue = 100, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000018"), PlanId = ProPlanId, LimitType = LimitType.MaxStorageMB, LimitValue = 1000, IsUnlimited = false, CreatedDateTime = Epoch },

            // ── Enterprise Plan Limits ──
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000021"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxProjects, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000022"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxEndpointsPerProject, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000023"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxTestCasesPerSuite, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000024"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxTestRunsPerMonth, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000025"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxConcurrentRuns, LimitValue = 10, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000026"), PlanId = EnterprisePlanId, LimitType = LimitType.RetentionDays, LimitValue = 365, IsUnlimited = false, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000027"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxLlmCallsPerMonth, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch },
            new PlanLimit { Id = Guid.Parse("20000000-0000-0000-0000-000000000028"), PlanId = EnterprisePlanId, LimitType = LimitType.MaxStorageMB, LimitValue = null, IsUnlimited = true, CreatedDateTime = Epoch });
    }
}
