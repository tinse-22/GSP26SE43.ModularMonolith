using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Controllers;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class PaymentsControllerTests
{
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly Mock<ICommandHandler<CreateSubscriptionPaymentCommand>> _subscribeHandlerMock;
    private readonly Mock<ICommandHandler<CreatePayOsCheckoutCommand>> _payOsCheckoutHandlerMock;
    private readonly Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>> _getPlansHandlerMock;
    private readonly Mock<IQueryHandler<GetPaymentIntentQuery, PaymentIntentModel>> _getIntentHandlerMock;
    private readonly PaymentsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public PaymentsControllerTests()
    {
        _loggerMock = new Mock<ILogger<PaymentsController>>();
        _subscribeHandlerMock = new Mock<ICommandHandler<CreateSubscriptionPaymentCommand>>();
        _payOsCheckoutHandlerMock = new Mock<ICommandHandler<CreatePayOsCheckoutCommand>>();
        _getPlansHandlerMock = new Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>>();
        _getIntentHandlerMock = new Mock<IQueryHandler<GetPaymentIntentQuery, PaymentIntentModel>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<CreateSubscriptionPaymentCommand>)))
            .Returns(_subscribeHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<CreatePayOsCheckoutCommand>)))
            .Returns(_payOsCheckoutHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetPlansQuery, List<PlanModel>>)))
            .Returns(_getPlansHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetPaymentIntentQuery, PaymentIntentModel>)))
            .Returns(_getIntentHandlerMock.Object);

        _controller = new PaymentsController(
            new Dispatcher(serviceProviderMock.Object),
            _loggerMock.Object,
            Options.Create(new PayOsOptions()));

        SetUser(_currentUserId);
    }

    [Fact]
    public async Task GetPlans_Should_ReturnOkWithPlanList()
    {
        var plans = new List<PlanModel>
        {
            new PlanModel { Id = Guid.NewGuid(), Name = "Free", DisplayName = "Free", IsActive = true },
            new PlanModel { Id = Guid.NewGuid(), Name = "Pro", DisplayName = "Professional", IsActive = true },
        };

        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var result = await _controller.GetPlans();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<PlanModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPlans_Should_MapIsActiveAndSearchFilters()
    {
        GetPlansQuery capturedQuery = null!;

        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPlansQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<PlanModel>());

        await _controller.GetPlans(isActive: false, search: "enterprise");

        capturedQuery.Should().NotBeNull();
        capturedQuery.IsActive.Should().BeFalse();
        capturedQuery.Search.Should().Be("enterprise");
    }

    [Fact]
    public async Task Subscribe_Should_ReturnOkWithPurchaseResult()
    {
        var planId = Guid.NewGuid();
        var expected = new SubscriptionPurchaseResultModel
        {
            RequiresPayment = true,
            PaymentIntentId = Guid.NewGuid(),
        };

        _subscribeHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSubscriptionPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSubscriptionPaymentCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Subscribe(planId, new CreateSubscriptionPaymentModel
        {
            BillingCycle = BillingCycle.Yearly,
        }, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Subscribe_Should_MapUserPlanAndBillingCycle()
    {
        var planId = Guid.NewGuid();
        CreateSubscriptionPaymentCommand capturedCommand = null!;

        _subscribeHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSubscriptionPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateSubscriptionPaymentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = new SubscriptionPurchaseResultModel { RequiresPayment = false };
            })
            .Returns(Task.CompletedTask);

        await _controller.Subscribe(planId, new CreateSubscriptionPaymentModel
        {
            BillingCycle = BillingCycle.Monthly,
        }, CancellationToken.None);

        capturedCommand.Should().NotBeNull();
        capturedCommand.UserId.Should().Be(_currentUserId);
        capturedCommand.PlanId.Should().Be(planId);
        capturedCommand.Model.Should().NotBeNull();
        capturedCommand.Model.BillingCycle.Should().Be(BillingCycle.Monthly);
    }

    [Fact]
    public async Task Subscribe_Should_PropagateNotFoundException()
    {
        _subscribeHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateSubscriptionPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("PLAN_NOT_FOUND"));

        var act = () => _controller.Subscribe(Guid.NewGuid(), new CreateSubscriptionPaymentModel(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PLAN_NOT_FOUND*");
    }

    [Fact]
    public async Task CreatePayOsCheckout_Should_ReturnOkWithCheckoutPayload()
    {
        var expected = new PayOsCheckoutResponseModel
        {
            CheckoutUrl = "https://payos.vn/checkout/123",
            OrderCode = 240526001,
        };

        _payOsCheckoutHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreatePayOsCheckoutCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreatePayOsCheckoutCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.CreatePayOsCheckout(new PayOsCheckoutRequestModel
        {
            IntentId = Guid.NewGuid(),
        }, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CreatePayOsCheckout_Should_MapIntentUserAndOptionalReturnUrl()
    {
        var intentId = Guid.NewGuid();
        CreatePayOsCheckoutCommand capturedCommand = null!;

        _payOsCheckoutHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreatePayOsCheckoutCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreatePayOsCheckoutCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = new PayOsCheckoutResponseModel
                {
                    CheckoutUrl = "https://payos.vn/checkout/abc",
                    OrderCode = 1234567890,
                };
            })
            .Returns(Task.CompletedTask);

        await _controller.CreatePayOsCheckout(new PayOsCheckoutRequestModel
        {
            IntentId = intentId,
            ReturnUrl = null,
        }, CancellationToken.None);

        capturedCommand.Should().NotBeNull();
        capturedCommand.UserId.Should().Be(_currentUserId);
        capturedCommand.IntentId.Should().Be(intentId);
        capturedCommand.ReturnUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetPaymentIntent_Should_ReturnOkWithIntentPayload()
    {
        var intentId = Guid.NewGuid();
        var expected = CreatePaymentIntentModel(intentId);

        _getIntentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPaymentIntentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Get(intentId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetPaymentIntent_Should_MapIntentUserAndThrowNotFound()
    {
        var intentId = Guid.NewGuid();
        GetPaymentIntentQuery capturedQuery = null!;

        _getIntentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPaymentIntentQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPaymentIntentQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreatePaymentIntentModel(intentId));

        await _controller.Get(intentId, CancellationToken.None);

        capturedQuery.Should().NotBeNull();
        capturedQuery.IntentId.Should().Be(intentId);
        capturedQuery.UserId.Should().Be(_currentUserId);
        capturedQuery.ThrowNotFoundIfNull.Should().BeTrue();
    }

    [Fact]
    public async Task GetPaymentIntent_Should_PropagateNotFoundException()
    {
        _getIntentHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPaymentIntentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("PAYMENT_INTENT_NOT_FOUND"));

        var act = () => _controller.Get(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PAYMENT_INTENT_NOT_FOUND*");
    }

    private void SetUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ], "TestAuth");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
    }

    private PaymentIntentModel CreatePaymentIntentModel(Guid intentId)
    {
        return new PaymentIntentModel
        {
            Id = intentId,
            UserId = _currentUserId,
            Amount = 390000,
            Currency = "VND",
            Purpose = PaymentPurpose.SubscriptionPurchase,
            PlanId = Guid.NewGuid(),
            PlanName = "Professional",
            BillingCycle = BillingCycle.Monthly,
            SubscriptionId = Guid.NewGuid(),
            Status = PaymentIntentStatus.Processing,
            CheckoutUrl = "https://payos.vn/checkout/123",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            OrderCode = 240526001,
            CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-3),
            UpdatedDateTime = DateTimeOffset.UtcNow,
        };
    }
}
