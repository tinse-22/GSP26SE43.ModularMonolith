using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class HandlePayOsWebhookCommandHandlerTests
{
    private readonly Mock<IRepository<PaymentIntent, Guid>> _paymentIntentRepoMock;
    private readonly Mock<IRepository<PaymentTransaction, Guid>> _paymentTransactionRepoMock;
    private readonly Mock<IRepository<UserSubscription, Guid>> _subscriptionRepoMock;
    private readonly Mock<IRepository<SubscriptionPlan, Guid>> _planRepoMock;
    private readonly Mock<IRepository<SubscriptionHistory, Guid>> _historyRepoMock;
    private readonly Mock<IPayOsService> _payOsServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    private readonly List<PaymentIntent> _paymentIntents = new();
    private readonly List<PaymentTransaction> _transactions = new();
    private readonly List<UserSubscription> _subscriptions = new();
    private readonly List<SubscriptionPlan> _plans = new();
    private readonly List<SubscriptionHistory> _histories = new();

    private readonly HandlePayOsWebhookCommandHandler _handler;

    public HandlePayOsWebhookCommandHandlerTests()
    {
        _paymentIntentRepoMock = new Mock<IRepository<PaymentIntent, Guid>>();
        _paymentTransactionRepoMock = new Mock<IRepository<PaymentTransaction, Guid>>();
        _subscriptionRepoMock = new Mock<IRepository<UserSubscription, Guid>>();
        _planRepoMock = new Mock<IRepository<SubscriptionPlan, Guid>>();
        _historyRepoMock = new Mock<IRepository<SubscriptionHistory, Guid>>();
        _payOsServiceMock = new Mock<IPayOsService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _paymentIntentRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _paymentIntentRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _paymentIntents.AsQueryable());
        _paymentTransactionRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _transactions.AsQueryable());
        _subscriptionRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _subscriptions.AsQueryable());
        _planRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _plans.AsQueryable());
        _historyRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _histories.AsQueryable());

        _paymentIntentRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentIntent>>()))
            .ReturnsAsync((IQueryable<PaymentIntent> query) => query.FirstOrDefault());
        _paymentTransactionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<PaymentTransaction>>()))
            .ReturnsAsync((IQueryable<PaymentTransaction> query) => query.FirstOrDefault());
        _subscriptionRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<UserSubscription>>()))
            .ReturnsAsync((IQueryable<UserSubscription> query) => query.FirstOrDefault());
        _planRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<SubscriptionPlan>>()))
            .ReturnsAsync((IQueryable<SubscriptionPlan> query) => query.FirstOrDefault());

        _subscriptionRepoMock.Setup(x => x.AddAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<UserSubscription, CancellationToken>((entity, _) => _subscriptions.Add(entity));
        _subscriptionRepoMock.Setup(x => x.UpdateAsync(It.IsAny<UserSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentTransactionRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<PaymentTransaction, CancellationToken>((entity, _) => _transactions.Add(entity));

        _paymentIntentRepoMock.Setup(x => x.UpdateAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _historyRepoMock.Setup(x => x.AddAsync(It.IsAny<SubscriptionHistory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<SubscriptionHistory, CancellationToken>((entity, _) => _histories.Add(entity));

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                (operation, _, ct) => operation(ct));
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new HandlePayOsWebhookCommandHandler(
            _paymentIntentRepoMock.Object,
            _paymentTransactionRepoMock.Object,
            _subscriptionRepoMock.Object,
            _planRepoMock.Object,
            _historyRepoMock.Object,
            _payOsServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_NullPayloadData_Should_ReturnIgnored()
    {
        var command = new HandlePayOsWebhookCommand
        {
            Payload = new PayOsWebhookPayload { Data = null },
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Ignored);
    }

    [Fact]
    public async Task HandleAsync_InvalidSignature_Should_ReturnIgnored()
    {
        _payOsServiceMock.Setup(x => x.VerifyWebhookSignature(It.IsAny<PayOsWebhookPayload>(), It.IsAny<string>()))
            .Returns(false);

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(12345),
            RawBody = "{}",
            SignatureHeader = "invalid-sig",
            SkipSignatureVerification = false,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Ignored);
    }

    [Fact]
    public async Task HandleAsync_ValidSignature_Should_NotReturnIgnoredDueToSignature()
    {
        _payOsServiceMock.Setup(x => x.VerifyWebhookSignature(It.IsAny<PayOsWebhookPayload>(), It.IsAny<string>()))
            .Returns(true);

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(99999),
            RawBody = "{}",
            SignatureHeader = "valid-sig",
            SkipSignatureVerification = false,
        };

        await _handler.HandleAsync(command);

        // No matching payment intent → Ignored (but not due to signature)
        command.Outcome.Should().Be(PayOsWebhookOutcome.Ignored);
        _payOsServiceMock.Verify(x => x.VerifyWebhookSignature(It.IsAny<PayOsWebhookPayload>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OrderCodeNotFound_Should_ReturnIgnored()
    {
        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(99999),
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Ignored);
    }

    [Fact]
    public async Task HandleAsync_DuplicateTransaction_Should_ReturnIgnored()
    {
        var orderCode = 12345L;
        var intent = CreatePaymentIntent(orderCode);
        _paymentIntents.Add(intent);

        _transactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Provider = "PAYOS",
            ProviderRef = "link-abc",
        });

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(orderCode, paymentLinkId: "link-abc"),
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Ignored);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulPayment_Should_CreateSubscriptionAndTransaction()
    {
        var orderCode = 12345L;
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            DisplayName = "Pro Plan",
            PriceMonthly = 129000,
            Currency = "VND",
            IsActive = true,
        };
        _plans.Add(plan);

        var intent = CreatePaymentIntent(orderCode, planId: plan.Id);
        _paymentIntents.Add(intent);

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(orderCode, paymentLinkId: "link-new"),
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Processed);
        intent.Status.Should().Be(PaymentIntentStatus.Succeeded);
        _subscriptions.Should().ContainSingle();
        _subscriptions.Single().Status.Should().Be(SubscriptionStatus.Active);
        _transactions.Should().ContainSingle();
        _transactions.Single().Status.Should().Be(PaymentStatus.Succeeded);
        _histories.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_FailedPayment_Should_UpdateIntentStatus()
    {
        var orderCode = 12345L;
        var intent = CreatePaymentIntent(orderCode);
        _paymentIntents.Add(intent);

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateFailedPayload(orderCode, paymentLinkId: "link-fail"),
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Processed);
        intent.Status.Should().Be(PaymentIntentStatus.Canceled);
    }

    [Fact]
    public async Task HandleAsync_FailedPaymentWithExpireDesc_Should_SetExpiredStatus()
    {
        var orderCode = 12345L;
        var intent = CreatePaymentIntent(orderCode);
        _paymentIntents.Add(intent);

        var payload = CreateFailedPayload(orderCode, paymentLinkId: "link-expire");
        payload.Desc = "Payment link expired";

        var command = new HandlePayOsWebhookCommand
        {
            Payload = payload,
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Processed);
        intent.Status.Should().Be(PaymentIntentStatus.Expired);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulPayment_WithExistingSubscription_Should_UpdateSubscription()
    {
        var orderCode = 12345L;
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Business",
            DisplayName = "Business Plan",
            PriceMonthly = 299000,
            Currency = "VND",
            IsActive = true,
        };
        _plans.Add(plan);

        var existingSubscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
        };
        _subscriptions.Add(existingSubscription);

        var intent = CreatePaymentIntent(orderCode, planId: plan.Id, userId: existingSubscription.UserId);
        intent.SubscriptionId = existingSubscription.Id;
        _paymentIntents.Add(intent);

        var command = new HandlePayOsWebhookCommand
        {
            Payload = CreateSuccessPayload(orderCode, paymentLinkId: "link-upgrade"),
            SkipSignatureVerification = true,
        };

        await _handler.HandleAsync(command);

        command.Outcome.Should().Be(PayOsWebhookOutcome.Processed);
        existingSubscription.PlanId.Should().Be(plan.Id);
        existingSubscription.Status.Should().Be(SubscriptionStatus.Active);
        _subscriptionRepoMock.Verify(x => x.UpdateAsync(existingSubscription, It.IsAny<CancellationToken>()), Times.Once);
        _histories.Should().ContainSingle();
    }

    private static PaymentIntent CreatePaymentIntent(
        long orderCode,
        Guid? planId = null,
        Guid? userId = null)
    {
        return new PaymentIntent
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            PlanId = planId ?? Guid.NewGuid(),
            OrderCode = orderCode,
            Amount = 129000,
            Currency = "VND",
            Status = PaymentIntentStatus.RequiresPayment,
            BillingCycle = BillingCycle.Monthly,
        };
    }

    private static PayOsWebhookPayload CreateSuccessPayload(long orderCode, string paymentLinkId = "link-123")
    {
        return new PayOsWebhookPayload
        {
            Code = "00",
            Desc = "Success",
            Success = true,
            Data = new PayOsWebhookData
            {
                OrderCode = orderCode,
                Amount = 129000,
                Currency = "VND",
                PaymentLinkId = paymentLinkId,
                Reference = $"ref-{orderCode}",
            },
        };
    }

    private static PayOsWebhookPayload CreateFailedPayload(long orderCode, string paymentLinkId = "link-fail")
    {
        return new PayOsWebhookPayload
        {
            Code = "01",
            Desc = "Failed",
            Success = false,
            Data = new PayOsWebhookData
            {
                OrderCode = orderCode,
                Amount = 129000,
                Currency = "VND",
                PaymentLinkId = paymentLinkId,
            },
        };
    }
}
