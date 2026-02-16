using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class CreateSubscriptionPaymentCommandHandlerTests
{
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<SubscriptionHistory, Guid>> _historyRepoMock;
    private readonly Mock<IRepository<PaymentIntent, Guid>> _paymentIntentRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    private readonly List<SubscriptionPlan> _plans = new();
    private readonly List<UserSubscription> _subscriptions = new();
    private readonly List<SubscriptionHistory> _histories = new();
    private readonly List<PaymentIntent> _paymentIntents = new();

    private readonly CreateSubscriptionPaymentCommandHandler _handler;

    public CreateSubscriptionPaymentCommandHandlerTests()
    {
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _historyRepoMock = new Mock<IRepository<SubscriptionHistory, Guid>>();
        _paymentIntentRepoMock = new Mock<IRepository<PaymentIntent, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _planRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _plans.AsQueryable());
        _subscriptionRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _subscriptions.AsQueryable());
        _historyRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _histories.AsQueryable());
        _paymentIntentRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _paymentIntents.AsQueryable());

        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((IQueryable<SubscriptionPlan> query) => query.FirstOrDefault());
        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync((IQueryable<UserSubscription> query) => query.FirstOrDefault());

        _subscriptionRepoMock.Setup(x => x.AddAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<UserSubscription, CancellationToken>((entity, _) => _subscriptions.Add(entity));
        _subscriptionRepoMock.Setup(x => x.UpdateAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock.Setup(x => x.AddAsync(It.IsAny<SubscriptionHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<SubscriptionHistory, CancellationToken>((entity, _) => _histories.Add(entity));

        _paymentIntentRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<PaymentIntent, CancellationToken>((entity, _) => _paymentIntents.Add(entity));

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                (operation, _, ct) => operation(ct));
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var options = Options.Create(new PayOsOptions
        {
            IntentExpirationMinutes = 30,
        });

        _handler = new CreateSubscriptionPaymentCommandHandler(
            _planRepoMock.Object,
            _subscriptionRepoMock.Object,
            _historyRepoMock.Object,
            _paymentIntentRepoMock.Object,
            options);
    }

    [Fact]
    public async Task HandleAsync_FreePlan_ShouldActivateSubscriptionImmediately()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Free",
            DisplayName = "Free",
            IsActive = true,
            PriceMonthly = 0,
            Currency = "VND",
        };
        _plans.Add(plan);

        var command = new CreateSubscriptionPaymentCommand
        {
            UserId = userId,
            PlanId = plan.Id,
            Model = new CreateSubscriptionPaymentModel
            {
                BillingCycle = BillingCycle.Monthly,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.RequiresPayment.Should().BeFalse();
        command.Result.PaymentIntentId.Should().BeNull();
        command.Result.Subscription.Should().NotBeNull();
        _subscriptions.Should().ContainSingle(x => x.UserId == userId && x.PlanId == plan.Id);
        _histories.Should().ContainSingle();
        _paymentIntents.Should().BeEmpty();
        _paymentIntentRepoMock.Verify(x => x.AddAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaidPlan_ShouldCreatePaymentIntentInRequiresPayment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro",
            IsActive = true,
            PriceMonthly = 129000,
            Currency = "VND",
        };
        _plans.Add(plan);

        var command = new CreateSubscriptionPaymentCommand
        {
            UserId = userId,
            PlanId = plan.Id,
            Model = new CreateSubscriptionPaymentModel
            {
                BillingCycle = BillingCycle.Monthly,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.RequiresPayment.Should().BeTrue();
        command.Result.PaymentIntentId.Should().NotBeNull();
        _paymentIntents.Should().ContainSingle();

        var paymentIntent = _paymentIntents.Single();
        paymentIntent.UserId.Should().Be(userId);
        paymentIntent.PlanId.Should().Be(plan.Id);
        paymentIntent.Amount.Should().Be(129000);
        paymentIntent.Status.Should().Be(PaymentIntentStatus.RequiresPayment);
        paymentIntent.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));

        _subscriptions.Should().BeEmpty();
        _histories.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasActiveSubscription_ShouldCreateUpgradeIntent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldPlanId = Guid.NewGuid();
        var newPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Business",
            DisplayName = "Business",
            IsActive = true,
            PriceMonthly = 299000,
            Currency = "VND",
        };
        _plans.Add(newPlan);
        _subscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = oldPlanId,
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            AutoRenew = true,
        });

        var command = new CreateSubscriptionPaymentCommand
        {
            UserId = userId,
            PlanId = newPlan.Id,
            Model = new CreateSubscriptionPaymentModel
            {
                BillingCycle = BillingCycle.Monthly,
            },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _paymentIntents.Should().ContainSingle();
        var paymentIntent = _paymentIntents.Single();
        paymentIntent.Purpose.Should().Be(PaymentPurpose.SubscriptionUpgrade);
        paymentIntent.SubscriptionId.Should().Be(_subscriptions.Single().Id);
    }

    [Fact]
    public async Task HandleAsync_InactivePlan_ShouldThrowValidationException()
    {
        // Arrange
        var inactivePlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Legacy",
            IsActive = false,
            PriceMonthly = 10,
        };
        _plans.Add(inactivePlan);

        var command = new CreateSubscriptionPaymentCommand
        {
            UserId = Guid.NewGuid(),
            PlanId = inactivePlan.Id,
            Model = new CreateSubscriptionPaymentModel
            {
                BillingCycle = BillingCycle.Monthly,
            },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
