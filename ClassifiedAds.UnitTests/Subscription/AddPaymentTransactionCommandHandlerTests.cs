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
    public async Task HandleAsync_Should_AddPendingPayOsTransaction_WithPlanPricing()
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
            UserId = subscription.UserId,
            Model = new AddPaymentTransactionModel
            {
                ExternalTxnId = "txn_1",
                ProviderRef = "ref_1",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().NotBe(Guid.Empty);
        command.SavedTransaction.Should().NotBeNull();
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(19.99m);
        savedTransaction.Currency.Should().Be("VND");
        savedTransaction.Status.Should().Be(PaymentStatus.Pending);
        savedTransaction.PaymentMethod.Should().Be("payos");
        savedTransaction.Provider.Should().Be("PAYOS");
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        _paymentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
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
            UserId = subscription.UserId,
            Model = new AddPaymentTransactionModel
            {
                ExternalTxnId = "dup_1",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().Be(existingTransaction.Id);
        command.SavedTransaction.Should().NotBeNull();
        command.SavedTransaction.Id.Should().Be(existingTransaction.Id);
        _paymentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DuplicateProviderRef_Should_ReturnExistingId()
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
            Provider = "PAYOS",
            ProviderRef = "payos_ref_01",
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);

        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync(existingTransaction);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            UserId = subscription.UserId,
            Model = new AddPaymentTransactionModel
            {
                ProviderRef = "payos_ref_01",
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.SavedTransactionId.Should().Be(existingTransaction.Id);
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
        _paymentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((PaymentTransaction)null);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            UserId = subscription.UserId,
            Model = new AddPaymentTransactionModel(),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
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
            UserId = subscription.UserId,
            Model = new AddPaymentTransactionModel(),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        savedTransaction.Should().NotBeNull();
        savedTransaction.Amount.Should().Be(49.5m);
        savedTransaction.Currency.Should().Be("VND");
    }

    [Fact]
    public async Task HandleAsync_OtherUserSubscription_Should_ThrowNotFoundException()
    {
        // Arrange
        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            BillingCycle = BillingCycle.Monthly,
            Status = SubscriptionStatus.Active,
            SnapshotPriceMonthly = 10m,
            SnapshotCurrency = "VND",
        };

        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync(subscription);

        var command = new AddPaymentTransactionCommand
        {
            SubscriptionId = subscription.Id,
            UserId = Guid.NewGuid(),
            Model = new AddPaymentTransactionModel(),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
