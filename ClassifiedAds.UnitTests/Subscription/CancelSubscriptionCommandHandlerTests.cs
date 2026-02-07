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

public class CancelSubscriptionCommandHandlerTests
{
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<SubscriptionHistory, Guid>> _historyRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CancelSubscriptionCommandHandler _handler;

    public CancelSubscriptionCommandHandlerTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
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

        _handler = new CancelSubscriptionCommandHandler(_subscriptionRepoMock.Object, _historyRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_CancelSubscription_AndWriteHistory()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            AutoRenew = true,
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);

        var command = new CancelSubscriptionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new CancelSubscriptionModel(),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.AutoRenew.Should().BeFalse();
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _historyRepoMock.Verify(x => x.AddAsync(It.IsAny<SubscriptionHistory>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCancelled_Should_DoNothing()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            Status = SubscriptionStatus.Cancelled,
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);

        // Act
        await _handler.HandleAsync(new CancelSubscriptionCommand { SubscriptionId = subscription.Id });

        // Assert
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _historyRepoMock.Verify(x => x.AddAsync(It.IsAny<SubscriptionHistory>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotFound_Should_ThrowNotFoundException()
    {
        // Arrange
        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync((UserSubscription)null);

        // Act
        var act = () => _handler.HandleAsync(new CancelSubscriptionCommand { SubscriptionId = Guid.NewGuid() });

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
