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
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;

    public AddPaymentTransactionCommandHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<PaymentTransaction, Guid> paymentTransactionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
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

        if (command.Model.Amount <= 0)
        {
            throw new ValidationException("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(command.Model.PaymentMethod))
        {
            throw new ValidationException("PaymentMethod is required.");
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
            var provider = string.IsNullOrWhiteSpace(command.Model.Provider)
                ? "MANUAL"
                : command.Model.Provider.Trim().ToUpperInvariant();
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

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = subscription.UserId,
            SubscriptionId = subscription.Id,
            Amount = command.Model.Amount,
            Currency = command.Model.Currency?.Trim().ToUpperInvariant() ?? "USD",
            Status = command.Model.Status,
            PaymentMethod = command.Model.PaymentMethod?.Trim(),
            Provider = string.IsNullOrWhiteSpace(command.Model.Provider)
                ? "MANUAL"
                : command.Model.Provider.Trim().ToUpperInvariant(),
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
}
