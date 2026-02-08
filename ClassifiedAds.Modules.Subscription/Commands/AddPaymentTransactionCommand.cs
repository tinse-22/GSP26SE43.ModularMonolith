using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class AddPaymentTransactionCommand : ICommand
{
    public Guid SubscriptionId { get; set; }

    public AddPaymentTransactionModel Model { get; set; }

    public Guid SavedTransactionId { get; set; }
}

public class AddPaymentTransactionCommandHandler : ICommandHandler<AddPaymentTransactionCommand>
{
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;

    public AddPaymentTransactionCommandHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PaymentTransaction, Guid> paymentTransactionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _paymentTransactionRepository = paymentTransactionRepository;
    }

    public async Task HandleAsync(AddPaymentTransactionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Model == null)
        {
            throw new ValidationException("Payment transaction model is required.");
        }

        if (command.SubscriptionId == Guid.Empty)
        {
            throw new ValidationException("SubscriptionId is required.");
        }

        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet().Where(x => x.Id == command.SubscriptionId));
        if (subscription == null)
        {
            throw new NotFoundException($"Subscription '{command.SubscriptionId}' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(command.Model.ExternalTxnId))
        {
            var existing = await _paymentTransactionRepository.FirstOrDefaultAsync(
                _paymentTransactionRepository.GetQueryableSet()
                    .Where(x => x.SubscriptionId == command.SubscriptionId && x.ExternalTxnId == command.Model.ExternalTxnId));
            if (existing != null)
            {
                command.SavedTransactionId = existing.Id;
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(command.Model.ProviderRef))
        {
            var provider = NormalizeProvider(command.Model.Provider);
            var providerRef = command.Model.ProviderRef.Trim();
            var existing = await _paymentTransactionRepository.FirstOrDefaultAsync(
                _paymentTransactionRepository.GetQueryableSet()
                    .Where(x => x.Provider == provider && x.ProviderRef == providerRef));
            if (existing != null)
            {
                command.SavedTransactionId = existing.Id;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(command.Model.PaymentMethod))
        {
            throw new ValidationException("PaymentMethod is required.");
        }

        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == subscription.PlanId));

        var amount = ResolveAmount(subscription, plan, command.Model.Amount);
        if (amount <= 0)
        {
            throw new ValidationException("Amount must be greater than zero.");
        }

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Amount = amount,
            Currency = ResolveCurrency(subscription, plan, command.Model.Currency),
            Status = command.Model.Status,
            PaymentMethod = command.Model.PaymentMethod?.Trim(),
            Provider = NormalizeProvider(command.Model.Provider),
            ProviderRef = command.Model.ProviderRef?.Trim(),
            ExternalTxnId = command.Model.ExternalTxnId?.Trim(),
            InvoiceUrl = command.Model.InvoiceUrl?.Trim(),
            FailureReason = command.Model.FailureReason?.Trim(),
        };

        if (transaction.Status == PaymentStatus.Failed && string.IsNullOrWhiteSpace(transaction.FailureReason))
        {
            transaction.FailureReason = "Payment failed.";
        }

        await _paymentTransactionRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _paymentTransactionRepository.AddAsync(transaction, ct);

            var subscriptionStatusChanged = false;
            if (transaction.Status == PaymentStatus.Succeeded && subscription.Status == SubscriptionStatus.PastDue)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscriptionStatusChanged = true;
            }

            if (transaction.Status == PaymentStatus.Failed && subscription.Status == SubscriptionStatus.Active)
            {
                subscription.Status = SubscriptionStatus.PastDue;
                subscriptionStatusChanged = true;
            }

            if (subscriptionStatusChanged)
            {
                await _subscriptionRepository.UpdateAsync(subscription, ct);
            }

            await _paymentTransactionRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        command.SavedTransactionId = transaction.Id;
    }

    private static decimal ResolveAmount(
        UserSubscription subscription,
        SubscriptionPlan plan,
        decimal? requestedAmount)
    {
        var snapshotAmount = GetSnapshotAmount(subscription);
        if (snapshotAmount.HasValue && snapshotAmount.Value > 0)
        {
            return snapshotAmount.Value;
        }

        var planAmount = GetPlanAmount(subscription, plan);
        if (planAmount.HasValue && planAmount.Value > 0)
        {
            return planAmount.Value;
        }

        if (requestedAmount.HasValue && requestedAmount.Value > 0)
        {
            return requestedAmount.Value;
        }

        return 0;
    }

    private static decimal? GetSnapshotAmount(UserSubscription subscription)
    {
        if (subscription == null)
        {
            return null;
        }

        if (subscription.BillingCycle == BillingCycle.Yearly)
        {
            return subscription.SnapshotPriceYearly ?? subscription.SnapshotPriceMonthly;
        }

        if (subscription.BillingCycle == BillingCycle.Monthly)
        {
            return subscription.SnapshotPriceMonthly ?? subscription.SnapshotPriceYearly;
        }

        return subscription.SnapshotPriceMonthly ?? subscription.SnapshotPriceYearly;
    }

    private static decimal? GetPlanAmount(UserSubscription subscription, SubscriptionPlan plan)
    {
        if (plan == null)
        {
            return null;
        }

        if (subscription.BillingCycle == BillingCycle.Yearly)
        {
            return plan.PriceYearly ?? plan.PriceMonthly;
        }

        if (subscription.BillingCycle == BillingCycle.Monthly)
        {
            return plan.PriceMonthly ?? plan.PriceYearly;
        }

        return plan.PriceMonthly ?? plan.PriceYearly;
    }

    private static string ResolveCurrency(UserSubscription subscription, SubscriptionPlan plan, string requestedCurrency)
    {
        var snapshotCurrency = NormalizeCurrency(subscription?.SnapshotCurrency);
        if (!string.IsNullOrWhiteSpace(snapshotCurrency))
        {
            return snapshotCurrency;
        }

        var planCurrency = NormalizeCurrency(plan?.Currency);
        if (!string.IsNullOrWhiteSpace(planCurrency))
        {
            return planCurrency;
        }

        var requested = NormalizeCurrency(requestedCurrency);
        if (!string.IsNullOrWhiteSpace(requested))
        {
            return requested;
        }

        return "USD";
    }

    private static string NormalizeCurrency(string currency)
    {
        if (!string.IsNullOrWhiteSpace(currency))
        {
            return currency.Trim().ToUpperInvariant();
        }

        return null;
    }

    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "MANUAL"
            : provider.Trim().ToUpperInvariant();
    }
}
