using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.Subscription;

public class PlanMappingTests
{
    #region ToModel Tests

    [Fact]
    public void ToModel_Should_MapAllProperties()
    {
        // Arrange
        var entity = CreateSamplePlan();
        var limits = CreateSampleLimits(entity.Id);

        // Act
        var model = entity.ToModel(limits);

        // Assert
        model.Should().NotBeNull();
        model.Id.Should().Be(entity.Id);
        model.Name.Should().Be(entity.Name);
        model.DisplayName.Should().Be(entity.DisplayName);
        model.Description.Should().Be(entity.Description);
        model.PriceMonthly.Should().Be(entity.PriceMonthly);
        model.PriceYearly.Should().Be(entity.PriceYearly);
        model.Currency.Should().Be(entity.Currency);
        model.IsActive.Should().Be(entity.IsActive);
        model.SortOrder.Should().Be(entity.SortOrder);
        model.CreatedDateTime.Should().Be(entity.CreatedDateTime);
        model.UpdatedDateTime.Should().Be(entity.UpdatedDateTime);
    }

    [Fact]
    public void ToModel_Should_MapLimits()
    {
        // Arrange
        var entity = CreateSamplePlan();
        var limits = CreateSampleLimits(entity.Id);

        // Act
        var model = entity.ToModel(limits);

        // Assert
        model.Limits.Should().HaveCount(2);
        model.Limits[0].LimitType.Should().Be("MaxProjects");
        model.Limits[0].LimitValue.Should().Be(5);
        model.Limits[0].IsUnlimited.Should().BeFalse();
        model.Limits[1].LimitType.Should().Be("MaxTestRunsPerMonth");
        model.Limits[1].LimitValue.Should().BeNull();
        model.Limits[1].IsUnlimited.Should().BeTrue();
    }

    [Fact]
    public void ToModel_Should_ReturnNull_WhenEntityIsNull()
    {
        // Arrange
        SubscriptionPlan entity = null;

        // Act
        var model = entity.ToModel();

        // Assert
        model.Should().BeNull();
    }

    [Fact]
    public void ToModel_Should_ReturnEmptyLimits_WhenLimitsIsNull()
    {
        // Arrange
        var entity = CreateSamplePlan();

        // Act
        var model = entity.ToModel(null);

        // Assert
        model.Limits.Should().NotBeNull();
        model.Limits.Should().BeEmpty();
    }

    #endregion

    #region ToModels Tests

    [Fact]
    public void ToModels_Should_MapMultipleEntities()
    {
        // Arrange
        var plan1 = CreateSamplePlan("Free", 0);
        var plan2 = CreateSamplePlan("Pro", 1);

        var limits1 = CreateSampleLimits(plan1.Id);
        var limits2 = CreateSampleLimits(plan2.Id);

        var allLimits = limits1.Concat(limits2).ToList();
        var limitsLookup = allLimits.ToLookup(l => l.PlanId);

        // Act
        var models = new[] { plan1, plan2 }.ToModels(limitsLookup).ToList();

        // Assert
        models.Should().HaveCount(2);
        models[0].Name.Should().Be("Free");
        models[1].Name.Should().Be("Pro");
        models[0].Limits.Should().HaveCount(2);
        models[1].Limits.Should().HaveCount(2);
    }

    [Fact]
    public void ToModels_Should_HandleNullLimitsLookup()
    {
        // Arrange
        var plan = CreateSamplePlan();

        // Act
        var models = new[] { plan }.ToModels(null).ToList();

        // Assert
        models.Should().HaveCount(1);
        models[0].Limits.Should().BeEmpty();
    }

    #endregion

    #region PlanLimit ToModel Tests

    [Fact]
    public void PlanLimit_ToModel_Should_MapCorrectly()
    {
        // Arrange
        var limit = new PlanLimit
        {
            Id = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            LimitType = LimitType.MaxProjects,
            LimitValue = 10,
            IsUnlimited = false,
        };

        // Act
        var model = limit.ToModel();

        // Assert
        model.Should().NotBeNull();
        model.Id.Should().Be(limit.Id);
        model.LimitType.Should().Be("MaxProjects");
        model.LimitValue.Should().Be(10);
        model.IsUnlimited.Should().BeFalse();
    }

    [Fact]
    public void PlanLimit_ToModel_Should_ReturnNull_WhenEntityIsNull()
    {
        // Arrange
        PlanLimit limit = null;

        // Act
        var model = limit.ToModel();

        // Assert
        model.Should().BeNull();
    }

    #endregion

    #region ToEntity Tests

    [Fact]
    public void ToEntity_Should_MapFromModelCorrectly()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "  Pro  ",
            DisplayName = "  Pro Plan  ",
            Description = "  Professional  ",
            PriceMonthly = 29.99m,
            PriceYearly = 299.99m,
            Currency = "usd",
            IsActive = true,
            SortOrder = 1,
        };

        // Act
        var entity = model.ToEntity();

        // Assert
        entity.Should().NotBeNull();
        entity.Name.Should().Be("Pro");
        entity.DisplayName.Should().Be("Pro Plan");
        entity.Description.Should().Be("Professional");
        entity.PriceMonthly.Should().Be(29.99m);
        entity.PriceYearly.Should().Be(299.99m);
        entity.Currency.Should().Be("USD");
        entity.IsActive.Should().BeTrue();
        entity.SortOrder.Should().Be(1);
    }

    [Fact]
    public void ToEntity_Should_DefaultCurrencyToUSD_WhenNull()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Free",
            DisplayName = "Free Plan",
            Currency = null,
        };

        // Act
        var entity = model.ToEntity();

        // Assert
        entity.Currency.Should().Be("USD");
    }

    #endregion

    #region ToLimitEntities Tests

    [Fact]
    public void ToLimitEntities_Should_MapValidLimitTypes()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Limits = new List<PlanLimitModel>
            {
                new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 10, IsUnlimited = false },
                new PlanLimitModel { LimitType = "MaxTestRunsPerMonth", IsUnlimited = true },
            },
        };

        // Act
        var entities = model.ToLimitEntities(planId);

        // Assert
        entities.Should().HaveCount(2);
        entities[0].PlanId.Should().Be(planId);
        entities[0].LimitType.Should().Be(LimitType.MaxProjects);
        entities[0].LimitValue.Should().Be(10);
        entities[0].IsUnlimited.Should().BeFalse();
        entities[1].LimitType.Should().Be(LimitType.MaxTestRunsPerMonth);
        entities[1].LimitValue.Should().BeNull();
        entities[1].IsUnlimited.Should().BeTrue();
    }

    [Fact]
    public void ToLimitEntities_Should_ThrowForInvalidLimitType()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Limits = new List<PlanLimitModel>
            {
                new PlanLimitModel { LimitType = "InvalidType", LimitValue = 10 },
            },
        };

        // Act
        var act = () => model.ToLimitEntities(planId);

        // Assert
        act.Should().Throw<ClassifiedAds.CrossCuttingConcerns.Exceptions.ValidationException>()
            .WithMessage("*InvalidType*");
    }

    [Fact]
    public void ToLimitEntities_Should_ReturnEmptyList_WhenLimitsIsNull()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Free",
            DisplayName = "Free Plan",
            Limits = null,
        };

        // Act
        var entities = model.ToLimitEntities(planId);

        // Assert
        entities.Should().NotBeNull();
        entities.Should().BeEmpty();
    }

    [Fact]
    public void ToLimitEntities_Should_NullifyLimitValue_WhenUnlimited()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Enterprise",
            DisplayName = "Enterprise Plan",
            Limits = new List<PlanLimitModel>
            {
                new PlanLimitModel { LimitType = "MaxProjects", LimitValue = 999, IsUnlimited = true },
            },
        };

        // Act
        var entities = model.ToLimitEntities(planId);

        // Assert
        entities[0].LimitValue.Should().BeNull();
        entities[0].IsUnlimited.Should().BeTrue();
    }

    [Theory]
    [InlineData("MaxProjects", LimitType.MaxProjects)]
    [InlineData("MaxEndpointsPerProject", LimitType.MaxEndpointsPerProject)]
    [InlineData("MaxTestCasesPerSuite", LimitType.MaxTestCasesPerSuite)]
    [InlineData("MaxTestRunsPerMonth", LimitType.MaxTestRunsPerMonth)]
    [InlineData("MaxConcurrentRuns", LimitType.MaxConcurrentRuns)]
    [InlineData("RetentionDays", LimitType.RetentionDays)]
    [InlineData("MaxLlmCallsPerMonth", LimitType.MaxLlmCallsPerMonth)]
    [InlineData("MaxStorageMB", LimitType.MaxStorageMB)]
    public void ToLimitEntities_Should_MapAllLimitTypes(string limitTypeName, LimitType expected)
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Limits = new List<PlanLimitModel>
            {
                new PlanLimitModel { LimitType = limitTypeName, LimitValue = 100, IsUnlimited = false },
            },
        };

        // Act
        var entities = model.ToLimitEntities(planId);

        // Assert
        entities[0].LimitType.Should().Be(expected);
    }

    #endregion

    #region Helpers

    private static SubscriptionPlan CreateSamplePlan(string name = "Pro", int sortOrder = 1)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = $"{name} Plan",
            Description = $"{name} plan description",
            PriceMonthly = 29.99m,
            PriceYearly = 299.99m,
            Currency = "USD",
            IsActive = true,
            SortOrder = sortOrder,
            CreatedDateTime = DateTimeOffset.UtcNow,
            UpdatedDateTime = DateTimeOffset.UtcNow,
        };
    }

    private static List<PlanLimit> CreateSampleLimits(Guid planId)
    {
        return new List<PlanLimit>
        {
            new PlanLimit
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                LimitType = LimitType.MaxProjects,
                LimitValue = 5,
                IsUnlimited = false,
            },
            new PlanLimit
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                LimitType = LimitType.MaxTestRunsPerMonth,
                LimitValue = null,
                IsUnlimited = true,
            },
        };
    }

    #endregion
}
