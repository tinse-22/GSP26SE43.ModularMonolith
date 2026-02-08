using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class CreatePayOsCheckoutCommand : ICommand
{
    public Guid UserId { get; set; }

    public Guid IntentId { get; set; }

    public string ReturnUrl { get; set; }

    public PayOsCheckoutResponseModel Result { get; set; }
}

public class CreatePayOsCheckoutCommandHandler : ICommandHandler<CreatePayOsCheckoutCommand>
{
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IPayOsService _payOsService;

    public CreatePayOsCheckoutCommandHandler(
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IPayOsService payOsService)
    {
        _paymentIntentRepository = paymentIntentRepository;
        _planRepository = planRepository;
        _payOsService = payOsService;
    }

    public async Task HandleAsync(CreatePayOsCheckoutCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            throw new ValidationException("UserId is required.");
        }

        if (command.IntentId == Guid.Empty)
        {
            throw new ValidationException("IntentId is required.");
        }

        var intent = await _paymentIntentRepository.FirstOrDefaultAsync(
            _paymentIntentRepository.GetQueryableSet().Where(x => x.Id == command.IntentId));

        if (intent == null)
        {
            throw new NotFoundException("Payment intent not found.");
        }

        if (intent.UserId != command.UserId)
        {
            throw new NotFoundException("Payment intent not found.");
        }

        if (intent.Status == PaymentIntentStatus.Succeeded)
        {
            throw new ValidationException("Payment intent has already succeeded.");
        }

        if (intent.Status == PaymentIntentStatus.Canceled || intent.Status == PaymentIntentStatus.Expired)
        {
            throw new ValidationException($"Payment intent is {intent.Status}.");
        }

        if (intent.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            intent.Status = PaymentIntentStatus.Expired;
            await _paymentIntentRepository.UpdateAsync(intent, cancellationToken);
            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            throw new ValidationException("Payment intent has expired.");
        }

        var orderCode = intent.OrderCode ?? await GenerateUniqueOrderCodeAsync(cancellationToken);

        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == intent.PlanId));

        var request = new PayOsCreatePaymentRequest
        {
            OrderCode = orderCode,
            Amount = decimal.ToInt64(decimal.Round(intent.Amount, 0, MidpointRounding.AwayFromZero)),
            Description = BuildDescription(plan, intent),
            ReturnUrl = command.ReturnUrl,
        };

        var checkoutUrl = await _payOsService.CreatePaymentLinkAsync(request, cancellationToken);

        intent.OrderCode = orderCode;
        intent.CheckoutUrl = checkoutUrl;
        if (intent.Status == PaymentIntentStatus.RequiresPayment)
        {
            intent.Status = PaymentIntentStatus.Processing;
        }

        await _paymentIntentRepository.UpdateAsync(intent, cancellationToken);
        await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = new PayOsCheckoutResponseModel
        {
            CheckoutUrl = checkoutUrl,
            OrderCode = orderCode,
        };
    }

    private async Task<long> GenerateUniqueOrderCodeAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var prefix = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
            var suffix = RandomNumberGenerator.GetInt32(100, 1000);
            var candidate = long.Parse($"{prefix}{suffix}", CultureInfo.InvariantCulture);

            var existing = await _paymentIntentRepository.FirstOrDefaultAsync(
                _paymentIntentRepository.GetQueryableSet().Where(x => x.OrderCode == candidate));
            if (existing == null)
            {
                return candidate;
            }

            await Task.Delay(5, cancellationToken);
        }

        throw new ValidationException("Could not generate a unique PayOS order code.");
    }

    private static string BuildDescription(SubscriptionPlan plan, PaymentIntent intent)
    {
        var planName = plan?.DisplayName ?? plan?.Name ?? "Subscription";
        var hint = intent.Id.ToString("N")[..8];
        var value = $"{planName}-{hint}";

        return value.Length <= 25 ? value : value[..25];
    }
}