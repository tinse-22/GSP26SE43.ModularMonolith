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

public class AddPaymentTransactionCommandHandlerTests
{
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<PaymentTransaction, Guid>> _paymentRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddPaymentTransactionCommandHandler _handler;

    public AddPaymentTransactionCommandHandlerTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _paymentRepoMock = new Mock<IRepository<PaymentTransaction, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _subscriptionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _paymentRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _subscriptionRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<UserSubscription>().AsQueryable());
        _paymentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PaymentTransaction>().AsQueryable());

        _handler = new AddPaymentTransactionCommandHandler(_subscriptionRepoMock.Object, _paymentRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_AddTransaction_AndRecoverPastDueSubscription()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = SubscriptionStatus.PastDue,
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                Amount = 19.99m,
                Currency = "usd",
                PaymentMethod = "card",
                Status = PaymentStatus.Succeeded,
                ExternalTxnId = "txn_1",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().NotBe(Guid.Empty);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        _paymentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DuplicateExternalTxn_Should_ReturnExistingId()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
        };
        var existingTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            ExternalTxnId = "dup_1",
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync(existingTransaction);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                Amount = 19.99m,
                PaymentMethod = "card",
                ExternalTxnId = "dup_1",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().Be(existingTransaction.Id);
        _paymentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AmountLessThanOrEqualZero_Should_ThrowValidationException()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                Amount = 0,
                PaymentMethod = "card",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
