using ClassifiedAds.Application;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Controllers;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class SubscriptionsControllerTests
{
    private readonly Mock<ICommandHandler<CancelSubscriptionCommand>> _cancelHandlerMock;
    private readonly Mock<IQueryHandler<GetCurrentSubscriptionByUserQuery, SubscriptionModel>> _getCurrentHandlerMock;
    private readonly Mock<IQueryHandler<GetSubscriptionQuery, SubscriptionModel>> _getSubscriptionHandlerMock;
    private readonly Mock<IQueryHandler<GetUsageTrackingsQuery, List<UsageTrackingModel>>> _getUsageHandlerMock;
    private readonly Mock<IQueryHandler<GetPaymentTransactionsQuery, List<PaymentTransactionModel>>> _getPaymentsHandlerMock;
    private readonly SubscriptionsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public SubscriptionsControllerTests()
    {
        _cancelHandlerMock = new Mock<ICommandHandler<CancelSubscriptionCommand>>();
        _getCurrentHandlerMock = new Mock<IQueryHandler<GetCurrentSubscriptionByUserQuery, SubscriptionModel>>();
        _getSubscriptionHandlerMock = new Mock<IQueryHandler<GetSubscriptionQuery, SubscriptionModel>>();
        _getUsageHandlerMock = new Mock<IQueryHandler<GetUsageTrackingsQuery, List<UsageTrackingModel>>>();
        _getPaymentsHandlerMock = new Mock<IQueryHandler<GetPaymentTransactionsQuery, List<PaymentTransactionModel>>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<CancelSubscriptionCommand>)))
            .Returns(_cancelHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetCurrentSubscriptionByUserQuery, SubscriptionModel>)))
            .Returns(_getCurrentHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetSubscriptionQuery, SubscriptionModel>)))
            .Returns(_getSubscriptionHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetUsageTrackingsQuery, List<UsageTrackingModel>>)))
            .Returns(_getUsageHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetPaymentTransactionsQuery, List<PaymentTransactionModel>>)))
            .Returns(_getPaymentsHandlerMock.Object);

        _controller = new SubscriptionsController(new Dispatcher(serviceProviderMock.Object));
        SetUser(_currentUserId, isAdmin: false);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnOkWithCurrentSubscription()
    {
        var expected = CreateSubscriptionModel(_currentUserId);

        _getCurrentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetCurrentSubscriptionByUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetCurrent();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetCurrent_Should_MapCurrentUserAndThrowNotFound()
    {
        GetCurrentSubscriptionByUserQuery capturedQuery = null!;

        _getCurrentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetCurrentSubscriptionByUserQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetCurrentSubscriptionByUserQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateSubscriptionModel(_currentUserId));

        await _controller.GetCurrent();

        capturedQuery.Should().NotBeNull();
        capturedQuery.UserId.Should().Be(_currentUserId);
        capturedQuery.ThrowNotFoundIfNull.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsage_Should_ReturnOkWithUsageList()
    {
        var usage = new List<UsageTrackingModel>
        {
            CreateUsageModel(_currentUserId, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
        };

        _getUsageHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetUsageTrackingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        var result = await _controller.GetUsage(Guid.NewGuid(), null, null);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<UsageTrackingModel>>().Subject.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsage_Should_ResolveCurrentUserForNonAdminAndMapPeriods()
    {
        GetUsageTrackingsQuery capturedQuery = null!;
        var requestedUserId = Guid.NewGuid();
        var periodStart = new DateOnly(2026, 5, 1);
        var periodEnd = new DateOnly(2026, 5, 31);

        _getUsageHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetUsageTrackingsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetUsageTrackingsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<UsageTrackingModel>());

        await _controller.GetUsage(requestedUserId, periodStart, periodEnd);

        capturedQuery.Should().NotBeNull();
        capturedQuery.UserId.Should().Be(_currentUserId);
        capturedQuery.PeriodStart.Should().Be(periodStart);
        capturedQuery.PeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public async Task GetPayments_Should_ReturnOkWithTransactionList()
    {
        var subscription = CreateSubscriptionModel(_currentUserId);
        var transactions = new List<PaymentTransactionModel>
        {
            CreatePaymentTransactionModel(_currentUserId, subscription.Id, PaymentStatus.Succeeded),
            CreatePaymentTransactionModel(_currentUserId, subscription.Id, PaymentStatus.Pending),
        };

        _getSubscriptionHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _getPaymentsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPaymentTransactionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        var result = await _controller.GetPayments(subscription.Id, PaymentStatus.Succeeded);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<PaymentTransactionModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPayments_Should_EnsureOwnershipAndMapCurrentUserFilter()
    {
        var subscription = CreateSubscriptionModel(_currentUserId);
        GetPaymentTransactionsQuery capturedQuery = null!;

        _getSubscriptionHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _getPaymentsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPaymentTransactionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPaymentTransactionsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<PaymentTransactionModel>());

        await _controller.GetPayments(subscription.Id, PaymentStatus.Pending);

        capturedQuery.Should().NotBeNull();
        capturedQuery.SubscriptionId.Should().Be(subscription.Id);
        capturedQuery.UserId.Should().Be(_currentUserId);
        capturedQuery.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Cancel_Should_ReturnOkWithUpdatedSubscription()
    {
        var subscription = CreateSubscriptionModel(_currentUserId);
        var cancelled = CreateSubscriptionModel(_currentUserId);
        cancelled.Id = subscription.Id;
        cancelled.Status = SubscriptionStatus.Cancelled;
        cancelled.AutoRenew = false;

        _getSubscriptionHandlerMock
            .SetupSequence(x => x.HandleAsync(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription)
            .ReturnsAsync(cancelled);
        _cancelHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CancelSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Cancel(subscription.Id, new CancelSubscriptionModel
        {
            ChangeReason = "No longer needed",
            EffectiveDate = new DateOnly(2026, 5, 26),
        });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SubscriptionModel>().Subject;
        payload.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_Should_EnsureOwnershipAndMapReason()
    {
        var subscription = CreateSubscriptionModel(_currentUserId);
        CancelSubscriptionCommand capturedCommand = null!;
        var model = new CancelSubscriptionModel
        {
            ChangeReason = "Downgrade later",
            EffectiveDate = new DateOnly(2026, 5, 26),
        };

        _getSubscriptionHandlerMock
            .SetupSequence(x => x.HandleAsync(It.IsAny<GetSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription)
            .ReturnsAsync(subscription);
        _cancelHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CancelSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CancelSubscriptionCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Cancel(subscription.Id, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.SubscriptionId.Should().Be(subscription.Id);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    private void SetUser(Guid userId, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        };

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
            },
        };
    }

    private static SubscriptionModel CreateSubscriptionModel(Guid userId)
    {
        return new SubscriptionModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = Guid.NewGuid(),
            PlanName = "Professional",
            PlanDisplayName = "Professional",
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = null,
            AutoRenew = true,
            SnapshotCurrency = "VND",
            SnapshotPlanName = "Professional",
            CreatedDateTime = DateTimeOffset.UtcNow.AddDays(-10),
        };
    }

    private static UsageTrackingModel CreateUsageModel(Guid userId, DateOnly periodStart, DateOnly periodEnd)
    {
        return new UsageTrackingModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ProjectCount = 3,
            EndpointCount = 12,
            TestSuiteCount = 5,
            TestCaseCount = 20,
            TestRunCount = 14,
            LlmCallCount = 88,
            StorageUsedMB = 256.5m,
            CreatedDateTime = DateTimeOffset.UtcNow.AddDays(-3),
        };
    }

    private static PaymentTransactionModel CreatePaymentTransactionModel(Guid userId, Guid subscriptionId, PaymentStatus status)
    {
        return new PaymentTransactionModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionId = subscriptionId,
            PaymentIntentId = Guid.NewGuid(),
            Amount = 390000,
            Currency = "VND",
            Status = status,
            PaymentMethod = "PayOS",
            Provider = "PayOS",
            ProviderRef = "ref-001",
            ExternalTxnId = "txn-001",
            InvoiceUrl = "https://pay.example.com/invoice/1",
            FailureReason = null,
            CreatedDateTime = DateTimeOffset.UtcNow.AddDays(-1),
        };
    }
}
