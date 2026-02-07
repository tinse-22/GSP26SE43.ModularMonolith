using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class AddUpdateSubscriptionCommandHandlerTests
{
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<SubscriptionHistory, Guid>> _historyRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddUpdateSubscriptionCommandHandler _handler;

    public AddUpdateSubscriptionCommandHandlerTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _historyRepoMock = new Mock<IRepository<SubscriptionHistory, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _subscriptionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _historyRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _subscriptionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<UserSubscription>().AsQueryable());
        _planRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<SubscriptionPlan>().AsQueryable());

        _handler = new AddUpdateSubscriptionCommandHandler(
            _subscriptionRepoMock.Object,
            _planRepoMock.Object,
            _historyRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_CreateSubscription_Should_AddSubscriptionAndHistory()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = planId,
            Name = "Pro",
            IsActive = true,
            PriceMonthly = 10m,
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync((UserSubscription)null);

        var command = new AddUpdateSubscriptionCommand
        {
            Model = new CreateUpdateSubscriptionModel
            {
                UserId = userId,
                PlanId = planId,
                BillingCycle = BillingCycle.Monthly,
                IsTrial = false,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedSubscriptionId.Should().NotBe(Guid.Empty);
        _subscriptionRepoMock.Verify(x => x.AddAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()), Times.Once);
        _historyRepoMock.Verify(x => x.AddAsync(It.IsAny<SubscriptionHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InactivePlan_Should_ThrowValidationException()
    {
        // Arrange
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Legacy",
            IsActive = false,
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var command = new AddUpdateSubscriptionCommand
        {
            Model = new CreateUpdateSubscriptionModel
            {
                UserId = Guid.NewGuid(),
                PlanId = plan.Id,
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_ExistingSubscriptionWithDifferentUser_Should_ThrowValidationException()
    {
        // Arrange
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            IsActive = true,
        };
        var existing = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
        };

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(existing);

        var command = new AddUpdateSubscriptionCommand
        {
            SubscriptionId = existing.Id,
            Model = new CreateUpdateSubscriptionModel
            {
                UserId = Guid.NewGuid(),
                PlanId = plan.Id,
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
