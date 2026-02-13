using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class GetPlanQueryHandlerTests
{
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PlanLimit, Guid>> _limitRepoMock;
    private readonly GetPlanQueryHandler _handler;

    public GetPlanQueryHandlerTests()
    {
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _limitRepoMock = new Mock<IRepository<PlanLimit, Guid>>();

        _planRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SubscriptionPlan>().AsQueryable());
        _limitRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PlanLimit>().AsQueryable());

        _handler = new GetPlanQueryHandler(_planRepoMock.Object, _limitRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnPlanModel_WhenPlanExists()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = planId,
            Name = "Pro",
            DisplayName = "Pro Plan",
            Currency = "USD",
            IsActive = true,
        };

        var limits = new List<PlanLimit>
        {
            new PlanLimit
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                LimitType = LimitType.MaxProjects,
                LimitValue = 10,
                IsUnlimited = false,
            },
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(limits);

        var query = new GetPlanQuery { Id = planId };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(planId);
        result.Name.Should().Be("Pro");
        result.Limits.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnNull_WhenPlanNotFound_AndThrowNotFoundIfNullIsFalse()
    {
        // Arrange
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        var query = new GetPlanQuery
        {
            Id = Guid.NewGuid(),
            ThrowNotFoundIfNull = false,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenPlanNotFound_AndThrowNotFoundIfNullIsTrue()
    {
        // Arrange
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        var query = new GetPlanQuery
        {
            Id = Guid.NewGuid(),
            ThrowNotFoundIfNull = true,
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnPlanWithEmptyLimits_WhenNoLimitsExist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = planId,
            Name = "Free",
            DisplayName = "Free Plan",
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(new List<PlanLimit>());

        var query = new GetPlanQuery { Id = planId };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Limits.Should().BeEmpty();
    }
}

public class GetPlansQueryHandlerTests
{
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PlanLimit, Guid>> _limitRepoMock;
    private readonly GetPlansQueryHandler _handler;

    public GetPlansQueryHandlerTests()
    {
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _limitRepoMock = new Mock<IRepository<PlanLimit, Guid>>();

        _handler = new GetPlansQueryHandler(_planRepoMock.Object, _limitRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnAllPlans_WhenNoFilters()
    {
        // Arrange
        var plans = new List<SubscriptionPlan>
        {
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Free", DisplayName = "Free", SortOrder = 0, IsActive = true },
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro", DisplayName = "Pro", SortOrder = 1, IsActive = true },
        };

        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(plans.AsQueryable());
        _planRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plans);
        _limitRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PlanLimit>().AsQueryable());
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(new List<PlanLimit>());

        var query = new GetPlansQuery();

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEmptyList_WhenNoPlansExist()
    {
        // Arrange
        _planRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SubscriptionPlan>().AsQueryable());
        _planRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(new List<SubscriptionPlan>());
        _limitRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PlanLimit>().AsQueryable());
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(new List<PlanLimit>());

        var query = new GetPlansQuery();

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_IncludeLimitsForEachPlan()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plans = new List<SubscriptionPlan>
        {
            new SubscriptionPlan { Id = planId, Name = "Pro", DisplayName = "Pro", SortOrder = 0 },
        };

        var limits = new List<PlanLimit>
        {
            new PlanLimit { Id = Guid.NewGuid(), PlanId = planId, LimitType = LimitType.MaxProjects, LimitValue = 10 },
            new PlanLimit { Id = Guid.NewGuid(), PlanId = planId, LimitType = LimitType.MaxStorageMB, LimitValue = 500 },
        };

        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(plans.AsQueryable());
        _planRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plans);
        _limitRepoMock.Setup(x => x.GetQueryableSet()).Returns(limits.AsQueryable());
        _limitRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync(limits);

        var query = new GetPlansQuery();

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().HaveCount(1);
        result[0].Limits.Should().HaveCount(2);
    }
}
