using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;

namespace ClassifiedAds.UnitTests.Subscription;

public class SubscriptionModelMappingTests
{
    [Fact]
    public void ToModel_Should_UseSnapshotPlanName_WhenAvailable()
    {
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SnapshotPlanName = "Pro Legacy",
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            Name = "Pro",
            DisplayName = "Pro New",
        };

        var model = subscription.ToModel(plan);

        model.PlanName.Should().Be("Pro Legacy");
        model.PlanDisplayName.Should().Be("Pro Legacy");
    }

    [Fact]
    public void ToModel_Should_FallbackToPlanNames_WhenSnapshotMissing()
    {
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SnapshotPlanName = null,
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            Name = "Pro",
            DisplayName = "Pro Display",
        };

        var model = subscription.ToModel(plan);

        model.PlanName.Should().Be("Pro");
        model.PlanDisplayName.Should().Be("Pro Display");
    }
}
