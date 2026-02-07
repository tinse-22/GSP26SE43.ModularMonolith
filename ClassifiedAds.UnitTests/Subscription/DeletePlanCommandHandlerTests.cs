using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class DeletePlanCommandHandlerTests
{
    private readonly Mock<Dispatcher> _dispatcherMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<UserSubscription, Guid>> _userSubRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeletePlanCommandHandler _handler;

    public DeletePlanCommandHandlerTests()
    {
        _dispatcherMock = new Mock<Dispatcher>(MockBehavior.Loose, new object[] { (IServiceProvider)null! });
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _userSubRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        // Setup ExecuteInTransactionAsync to simply invoke the operation
        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        // Setup empty queryable for user subscriptions
        _userSubRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<UserSubscription>().AsQueryable());
        _userSubRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(new List<UserSubscription>());

        _handler = new DeletePlanCommandHandler(
            _dispatcherMock.Object,
            _planRepoMock.Object,
            _userSubRepoMock.Object);
    }

    #region Delete Plan Tests

    [Fact]
    public async Task HandleAsync_Should_DeactivatePlan_WhenNoActiveSubscribers()
    {
        // Arrange
        var plan = CreateActivePlan();

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var command = new DeletePlanCommand { PlanId = plan.Id };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        plan.IsActive.Should().BeFalse();
        _planRepoMock.Verify(x => x.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenPlanNotFound()
    {
        // Arrange
        _planRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SubscriptionPlan>().AsQueryable());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);

        var command = new DeletePlanCommand { PlanId = Guid.NewGuid() };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenActiveSubscribersExist()
    {
        // Arrange
        var plan = CreateActivePlan();

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var activeSubscriptions = new List<UserSubscription>
        {
            new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
            },
        };

        _userSubRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(activeSubscriptions);

        var command = new DeletePlanCommand { PlanId = plan.Id };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_DoNothing_WhenPlanAlreadyInactive()
    {
        // Arrange
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Old Plan",
            DisplayName = "Old Plan",
            IsActive = false,
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var command = new DeletePlanCommand { PlanId = plan.Id };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _planRepoMock.Verify(x => x.UpdateAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_SaveChanges_AfterDeactivation()
    {
        // Arrange
        var plan = CreateActivePlan();

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var command = new DeletePlanCommand { PlanId = plan.Id };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers

    private static SubscriptionPlan CreateActivePlan()
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro Plan",
            IsActive = true,
        };
    }

    #endregion
}
