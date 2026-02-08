using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class CreateSubscriptionPaymentCommand : ICommand
{
    public Guid UserId { get; set; }

    public Guid PlanId { get; set; }

    public CreateSubscriptionPaymentModel Model { get; set; }

    public SubscriptionPurchaseResultModel Result { get; set; }
}

public class CreateSubscriptionPaymentCommandHandler : ICommandHandler<CreateSubscriptionPaymentCommand>
{
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionHistory, Guid> _historyRepository;
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly PayOsOptions _payOsOptions;

    public CreateSubscriptionPaymentCommandHandler(
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionHistory, Guid> historyRepository,
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IOptions<PayOsOptions> payOsOptions)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _historyRepository = historyRepository;
        _paymentIntentRepository = paymentIntentRepository;
        _payOsOptions = payOsOptions?.Value ?? new PayOsOptions();
    }

    public async Task HandleAsync(CreateSubscriptionPaymentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            throw new ValidationException("UserId is required.");
        }

        if (command.PlanId == Guid.Empty)
        {
            throw new ValidationException("PlanId is required.");
        }

        if (command.Model == null)
        {
            throw new ValidationException("Model is required.");
        }

        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == command.PlanId));

        if (plan == null)
        {
            throw new NotFoundException($"Subscription plan '{command.PlanId}' was not found.");
        }

        if (!plan.IsActive)
        {
            throw new ValidationException("Subscription plan is inactive.");
        }

        var billingCycle = command.Model.BillingCycle;
        var price = billingCycle == BillingCycle.Yearly ? plan.PriceYearly : plan.PriceMonthly;

        if (price is null)
        {
            throw new ValidationException($"Plan '{plan.Name}' does not support {billingCycle} billing.");
        }

        var existingSubscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet()
                .Where(x => x.UserId == command.UserId
                    && (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trial)));

        if (price.Value <= 0)
        {
            await _planRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var now = DateOnly.FromDateTime(DateTime.UtcNow);
                var subscription = existingSubscription ?? new UserSubscription
                {
                    Id = Guid.NewGuid(),
                    UserId = command.UserId,
                    AutoRenew = true,
                };

                var oldPlanId = existingSubscription?.PlanId;
                subscription.PlanId = plan.Id;
                subscription.BillingCycle = billingCycle;
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartDate = now;
                subscription.EndDate = null;
                subscription.NextBillingDate = null;
                subscription.CancelledAt = null;
                subscription.TrialEndsAt = null;

                if (existingSubscription == null)
                {
                    await _subscriptionRepository.AddAsync(subscription, ct);
                }
                else
                {
                    await _subscriptionRepository.UpdateAsync(subscription, ct);
                }

                var changeType = existingSubscription == null
                    ? ChangeType.Created
                    : oldPlanId == plan.Id ? ChangeType.Reactivated : ChangeType.Upgraded;

                var history = new SubscriptionHistory
                {
                    Id = Guid.NewGuid(),
                    SubscriptionId = subscription.Id,
                    OldPlanId = oldPlanId,
                    NewPlanId = plan.Id,
                    ChangeType = changeType,
                    ChangeReason = changeType.ToString(),
                    EffectiveDate = now,
                };
                await _historyRepository.AddAsync(history, ct);

                await _planRepository.UnitOfWork.SaveChangesAsync(ct);

                command.Result = new SubscriptionPurchaseResultModel
                {
                    RequiresPayment = false,
                    Subscription = subscription.ToModel(plan),
                };
            }, cancellationToken: cancellationToken);

            return;
        }

        var purpose = existingSubscription != null
            ? PaymentPurpose.SubscriptionUpgrade
            : PaymentPurpose.SubscriptionPurchase;

        await _planRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var paymentIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                Amount = price.Value,
                Currency = string.IsNullOrWhiteSpace(plan.Currency) ? "VND" : plan.Currency,
                Purpose = purpose,
                PlanId = plan.Id,
                BillingCycle = billingCycle,
                SubscriptionId = existingSubscription?.Id,
                Status = PaymentIntentStatus.RequiresPayment,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _payOsOptions.IntentExpirationMinutes)),
            };

            await _paymentIntentRepository.AddAsync(paymentIntent, ct);
            await _planRepository.UnitOfWork.SaveChangesAsync(ct);

            command.Result = new SubscriptionPurchaseResultModel
            {
                RequiresPayment = true,
                PaymentIntentId = paymentIntent.Id,
            };
        }, cancellationToken: cancellationToken);
    }
}