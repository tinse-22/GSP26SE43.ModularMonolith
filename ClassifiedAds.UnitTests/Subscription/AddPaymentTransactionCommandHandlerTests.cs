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
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<PaymentTransaction, Guid>> _paymentRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddPaymentTransactionCommandHandler _handler;

    public AddPaymentTransactionCommandHandlerTests()
    {
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _paymentRepoMock = new Mock<IRepository<PaymentTransaction, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _subscriptionRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _paymentRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

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
        _paymentRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<PaymentTransaction>().AsQueryable());

        _handler = new AddPaymentTransactionCommandHandler(
            _subscriptionRepoMock.Object,
            _planRepoMock.Object,
            _paymentRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_AddTransaction_AndRecoverPastDueSubscription()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.PastDue,
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            PriceMonthly = 19.99m,
            PriceYearly = 199m,
            Currency = "vnd",
        };
        PaymentTransaction savedTransaction = null;

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);
        _paymentRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentTransaction, CancellationToken>((transaction, _) => savedTransaction = transaction)
            .Returns(Task.CompletedTask);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                PaymentMethod = "card",
                Status = PaymentStatus.Succeeded,
                ExternalTxnId = "txn_1",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().NotBe(Guid.Empty);
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(19.99m);
        savedTransaction.Currency.Should().Be("VND");
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
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            PriceMonthly = 19.99m,
            Currency = "USD",
        };
        var existingTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            ExternalTxnId = "dup_1",
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync(existingTransaction);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
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
    public async Task HandleAsync_DuplicateExternalTxn_WithoutPaymentMethod_Should_ReturnExistingId()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
        };
        var existingTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            ExternalTxnId = "dup_without_required_fields",
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
                ExternalTxnId = "dup_without_required_fields",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().Be(existingTransaction.Id);
        _planRepoMock.Verify(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()), Times.Never);
        _paymentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CannotResolveAmount_Should_ThrowValidationException()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            PriceMonthly = null,
            PriceYearly = null,
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                PaymentMethod = "card",
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_PlanWithoutPrice_Should_UseRequestedAmount()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            PriceMonthly = null,
            PriceYearly = null,
            Currency = null,
        };
        PaymentTransaction savedTransaction = null;

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);
        _paymentRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentTransaction, CancellationToken>((transaction, _) => savedTransaction = transaction)
            .Returns(Task.CompletedTask);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                Amount = 88.5m,
                Currency = "usd",
                PaymentMethod = "bank_transfer",
                Status = PaymentStatus.Succeeded,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(88.5m);
        savedTransaction.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task HandleAsync_Should_UseSnapshotAmountAndCurrency_WhenSnapshotExists()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            SnapshotPriceMonthly = 49.5m,
            SnapshotCurrency = "vnd",
        };
        var plan = new SubscriptionPlan
        {
            Id = subscription.PlanId,
            PriceMonthly = 99m,
            Currency = "usd",
        };
        PaymentTransaction savedTransaction = null;

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync(plan);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);
        _paymentRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentTransaction, CancellationToken>((transaction, _) => savedTransaction = transaction)
            .Returns(Task.CompletedTask);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                Amount = 10m,
                Currency = "eur",
                PaymentMethod = "bank_transfer",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(49.5m);
        savedTransaction.Currency.Should().Be("VND");
    }

    [Fact]
    public async Task HandleAsync_Should_UseSnapshotAmount_WhenPlanNotFound()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Yearly,
            Status = SubscriptionStatus.Active,
            SnapshotPriceYearly = 199m,
            SnapshotCurrency = "usd",
        };
        PaymentTransaction savedTransaction = null;

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((SubscriptionPlan)null);
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);
        _paymentRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentTransaction, CancellationToken>((transaction, _) => savedTransaction = transaction)
            .Returns(Task.CompletedTask);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            Model = new AddPaymentTransactionModel
            {
                PaymentMethod = "bank_transfer",
                Status = PaymentStatus.Succeeded,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(199m);
        savedTransaction.Currency.Should().Be("USD");
    }
}
