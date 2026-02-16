using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class ConsumeLimitAtomicallyCommandHandlerTests
{
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PlanLimit, Guid>> _planLimitRepoMock;
    private readonly Mock<IRepository<UsageTracking, Guid>> _usageRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ConsumeLimitAtomicallyCommandHandler>> _loggerMock;

    private readonly List<UserSubscription> _subscriptions = new();
    private readonly List<SubscriptionPlan> _plans = new();
    private readonly List<PlanLimit> _planLimits = new();
    private readonly List<UsageTracking> _usageTrackings = new();

    private readonly ConsumeLimitAtomicallyCommandHandler _handler;

    public ConsumeLimitAtomicallyCommandHandlerTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _planLimitRepoMock = new Mock<IRepository<PlanLimit, Guid>>();
        _usageRepoMock = new Mock<IRepository<UsageTracking, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ConsumeLimitAtomicallyCommandHandler>>();

        _usageRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _subscriptionRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _subscriptions.AsQueryable());
        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _plans.AsQueryable());
        _planLimitRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _planLimits.AsQueryable());
        _usageRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _usageTrackings.AsQueryable());

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync((IQueryable<UserSubscription> query) => query.FirstOrDefault());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((IQueryable<SubscriptionPlan> query) => query.FirstOrDefault());
        _planLimitRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PlanLimit>>()))
            .ReturnsAsync((IQueryable<PlanLimit> query) => query.FirstOrDefault());
        _usageRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UsageTracking>>()))
            .ReturnsAsync((IQueryable<UsageTracking> query) => query.FirstOrDefault());

        _usageRepoMock.Setup(x => x.AddAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<UsageTracking, CancellationToken>((entity, _) => _usageTrackings.Add(entity));
        _usageRepoMock.Setup(x => x.UpdateAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<ClassifiedAds.Contracts.Subscription.DTOs.LimitCheckResultDTO>>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ClassifiedAds.Contracts.Subscription.DTOs.LimitCheckResultDTO>>, IsolationLevel, CancellationToken>(
                (operation, _, ct) => operation(ct));
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new ConsumeLimitAtomicallyCommandHandler(
            _subscriptionRepoMock.Object,
            _planRepoMock.Object,
            _planLimitRepoMock.Object,
            _usageRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_NoActiveSubscription_ShouldDeny()
    {
        // Arrange
        var command = new ConsumeLimitAtomicallyCommand
        {
            UserId = Guid.NewGuid(),
            LimitType = LimitType.MaxEndpointsPerProject,
            IncrementValue = 1,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.IsAllowed.Should().BeFalse();
        _usageRepoMock.Verify(x => x.AddAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Never);
        _usageRepoMock.Verify(x => x.UpdateAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnlimitedPlanLimit_ShouldAllowWithoutPersistingUsage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = SeedActiveSubscriptionWithPlan(userId);
        _planLimits.Add(new PlanLimit
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IsUnlimited = true,
            LimitValue = null,
        });

        var command = new ConsumeLimitAtomicallyCommand
        {
            UserId = userId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IncrementValue = 1,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.IsAllowed.Should().BeTrue();
        command.Result.IsUnlimited.Should().BeTrue();
        _usageTrackings.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithinLimit_ShouldUpdateUsage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = SeedActiveSubscriptionWithPlan(userId);
        var subscription = _subscriptions.Single();
        _planLimits.Add(new PlanLimit
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IsUnlimited = false,
            LimitValue = 5,
        });

        _usageTrackings.Add(new UsageTracking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodStart = subscription.StartDate,
            PeriodEnd = subscription.EndDate!.Value,
            EndpointCount = 3,
        });

        var command = new ConsumeLimitAtomicallyCommand
        {
            UserId = userId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IncrementValue = 2,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.IsAllowed.Should().BeTrue();
        command.Result.IsUnlimited.Should().BeFalse();
        command.Result.CurrentUsage.Should().Be(5);
        _usageTrackings.Single().EndpointCount.Should().Be(5);
        _usageRepoMock.Verify(x => x.UpdateAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OverLimit_ShouldDenyWithoutPersisting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = SeedActiveSubscriptionWithPlan(userId);
        var subscription = _subscriptions.Single();
        _planLimits.Add(new PlanLimit
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IsUnlimited = false,
            LimitValue = 5,
        });

        _usageTrackings.Add(new UsageTracking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodStart = subscription.StartDate,
            PeriodEnd = subscription.EndDate!.Value,
            EndpointCount = 5,
        });

        var command = new ConsumeLimitAtomicallyCommand
        {
            UserId = userId,
            LimitType = LimitType.MaxEndpointsPerProject,
            IncrementValue = 1,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.IsAllowed.Should().BeFalse();
        command.Result.CurrentUsage.Should().Be(5);
        command.Result.DenialReason.Should().NotBeNullOrWhiteSpace();
        _usageTrackings.Single().EndpointCount.Should().Be(5);
        _usageRepoMock.Verify(x => x.UpdateAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Never);
        _usageRepoMock.Verify(x => x.AddAsync(It.IsAny<UsageTracking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Guid SeedActiveSubscriptionWithPlan(Guid userId)
    {
        var planId = Guid.NewGuid();
        _plans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "Pro",
            DisplayName = "Pro",
            IsActive = true,
        });

        _subscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            AutoRenew = true,
        });

        return planId;
    }
}
