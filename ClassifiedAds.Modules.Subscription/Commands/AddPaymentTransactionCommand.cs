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

    public Guid UserId { get; set; }

    public AddPaymentTransactionModel Model { get; set; }

    public Guid SavedTransactionId { get; set; }

    public PaymentTransactionModel SavedTransaction { get; set; }
}

public class AddPaymentTransactionCommandHandler : ICommandHandler<AddPaymentTransactionCommand>
{
    private const string PaymentMethodPayOs = "payos";
    private const string PaymentProviderPayOs = "PAYOS";

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
        if (command.SubscriptionId == Guid.Empty)
        {
            throw new ValidationException("Ma dang ky la bat buoc.");
        }

        command.Model ??= new AddPaymentTransactionModel();

        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet().Where(x => x.Id == command.SubscriptionId));
        if (subscription == null)
        {
            throw new NotFoundException($"Khong tim thay dang ky voi ma '{command.SubscriptionId}'.");
        }

        if (command.UserId != Guid.Empty && subscription.UserId != command.UserId)
        {
            throw new NotFoundException($"Khong tim thay dang ky voi ma '{command.SubscriptionId}'.");
        }

        var externalTxnId = command.Model.ExternalTxnId?.Trim();
        if (!string.IsNullOrWhiteSpace(externalTxnId))
        {
            var existing = await _paymentTransactionRepository.FirstOrDefaultAsync(
                _paymentTransactionRepository.GetQueryableSet()
                    .Where(x => x.SubscriptionId == command.SubscriptionId && x.ExternalTxnId == externalTxnId));
            if (existing != null)
            {
                command.SavedTransactionId = existing.Id;
                command.SavedTransaction = existing.ToModel();
                return;
            }
        }

        var providerRef = command.Model.ProviderRef?.Trim();
        if (!string.IsNullOrWhiteSpace(providerRef))
        {
            var existing = await _paymentTransactionRepository.FirstOrDefaultAsync(
                _paymentTransactionRepository.GetQueryableSet()
                    .Where(x => x.Provider == PaymentProviderPayOs && x.ProviderRef == providerRef));
            if (existing != null)
            {
                command.SavedTransactionId = existing.Id;
                command.SavedTransaction = existing.ToModel();
                return;
            }
        }

        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == subscription.PlanId));

        var amount = ResolveAmount(subscription, plan);
        if (amount <= 0)
        {
            throw new ValidationException("So tien phai lon hon 0.");
        }

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Amount = amount,
            Currency = ResolveCurrency(subscription, plan),
            Status = PaymentStatus.Pending,
            PaymentMethod = PaymentMethodPayOs,
            Provider = PaymentProviderPayOs,
            ProviderRef = providerRef,
            ExternalTxnId = externalTxnId,
        };

        await _paymentTransactionRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _paymentTransactionRepository.AddAsync(transaction, ct);
            await _paymentTransactionRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        command.SavedTransactionId = transaction.Id;
        command.SavedTransaction = transaction.ToModel();
    }

    private static decimal ResolveAmount(UserSubscription subscription, SubscriptionPlan plan)
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

    private static string ResolveCurrency(UserSubscription subscription, SubscriptionPlan plan)
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

        return "VND";
    }

    private static string NormalizeCurrency(string currency)
    {
        if (!string.IsNullOrWhiteSpace(currency))
        {
            return currency.Trim().ToUpperInvariant();
        }

        return null;
    }
}
