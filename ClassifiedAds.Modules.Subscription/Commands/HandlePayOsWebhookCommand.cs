using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class HandlePayOsWebhookCommand : ICommand
{
    public PayOsWebhookPayload Payload { get; set; }

    public string RawBody { get; set; }

    public string SignatureHeader { get; set; }

    public bool SkipSignatureVerification { get; set; }

    public PayOsWebhookOutcome Outcome { get; set; }
}

public class HandlePayOsWebhookCommandHandler : ICommandHandler<HandlePayOsWebhookCommand>
{
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<SubscriptionHistory, Guid> _historyRepository;
    private readonly IPayOsService _payOsService;

    public HandlePayOsWebhookCommandHandler(
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IRepository<PaymentTransaction, Guid> paymentTransactionRepository,
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<SubscriptionHistory, Guid> historyRepository,
        IPayOsService payOsService)
    {
        _paymentIntentRepository = paymentIntentRepository;
        _paymentTransactionRepository = paymentTransactionRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _historyRepository = historyRepository;
        _payOsService = payOsService;
    }

    public async Task HandleAsync(HandlePayOsWebhookCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Payload?.Data == null)
        {
            command.Outcome = PayOsWebhookOutcome.Ignored;
            return;
        }

        if (!command.SkipSignatureVerification
            && !_payOsService.VerifyWebhookSignature(command.Payload, command.RawBody))
        {
            command.Outcome = PayOsWebhookOutcome.Ignored;
            return;
        }

        var orderCode = command.Payload.Data.OrderCode;
        var paymentIntent = await _paymentIntentRepository.FirstOrDefaultAsync(
            _paymentIntentRepository.GetQueryableSet().Where(x => x.OrderCode == orderCode));

        if (paymentIntent == null)
        {
            command.Outcome = PayOsWebhookOutcome.Ignored;
            return;
        }

        var providerRef = ResolveProviderRef(command.Payload.Data);
        var existing = await _paymentTransactionRepository.FirstOrDefaultAsync(
            _paymentTransactionRepository.GetQueryableSet()
                .Where(x => x.Provider == "PAYOS" && x.ProviderRef == providerRef));

        if (existing != null)
        {
            command.Outcome = PayOsWebhookOutcome.Ignored;
            return;
        }

        var isSucceeded = IsSucceeded(command.Payload);

        await _paymentIntentRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (isSucceeded)
            {
                var subscription = await UpsertSubscriptionAsync(paymentIntent, ct);
                paymentIntent.SubscriptionId = subscription.Id;
                paymentIntent.Status = PaymentIntentStatus.Succeeded;

                var succeededTransaction = CreateTransaction(
                    paymentIntent,
                    subscription.Id,
                    providerRef,
                    command.Payload,
                    PaymentStatus.Succeeded,
                    failureReason: null);
                await _paymentTransactionRepository.AddAsync(succeededTransaction, ct);
            }
            else
            {
                paymentIntent.Status = ResolveFailureStatus(command.Payload);

                if (paymentIntent.SubscriptionId.HasValue)
                {
                    var failedTransaction = CreateTransaction(
                        paymentIntent,
                        paymentIntent.SubscriptionId.Value,
                        providerRef,
                        command.Payload,
                        PaymentStatus.Failed,
                        failureReason: command.Payload.Desc ?? command.Payload.Data.Desc ?? "Payment failed.");
                    await _paymentTransactionRepository.AddAsync(failedTransaction, ct);
                }
            }

            await _paymentIntentRepository.UpdateAsync(paymentIntent, ct);
            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        command.Outcome = PayOsWebhookOutcome.Processed;
    }

    private async Task<UserSubscription> UpsertSubscriptionAsync(PaymentIntent intent, CancellationToken ct)
    {
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == intent.PlanId));

        var subscription = intent.SubscriptionId.HasValue
            ? await _subscriptionRepository.FirstOrDefaultAsync(
                _subscriptionRepository.GetQueryableSet().Where(x => x.Id == intent.SubscriptionId.Value))
            : null;

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = intent.BillingCycle == BillingCycle.Yearly
            ? now.AddYears(1)
            : now.AddMonths(1);

        var oldPlanId = subscription?.PlanId;
        if (subscription == null)
        {
            subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = intent.UserId,
                PlanId = intent.PlanId,
                Status = SubscriptionStatus.Active,
                BillingCycle = intent.BillingCycle,
                StartDate = now,
                EndDate = endDate,
                NextBillingDate = endDate,
                AutoRenew = true,
            };
            await _subscriptionRepository.AddAsync(subscription, ct);
        }
        else
        {
            subscription.PlanId = intent.PlanId;
            subscription.Status = SubscriptionStatus.Active;
            subscription.BillingCycle = intent.BillingCycle;
            subscription.StartDate = now;
            subscription.EndDate = endDate;
            subscription.NextBillingDate = endDate;
            subscription.AutoRenew = true;
            subscription.CancelledAt = null;
            subscription.TrialEndsAt = null;
            await _subscriptionRepository.UpdateAsync(subscription, ct);
        }

        var changeType = oldPlanId == null
            ? ChangeType.Created
            : oldPlanId == plan?.Id ? ChangeType.Reactivated : ChangeType.Upgraded;

        var history = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            OldPlanId = oldPlanId,
            NewPlanId = subscription.PlanId,
            ChangeType = changeType,
            ChangeReason = "PaymentSucceeded",
            EffectiveDate = now,
        };

        await _historyRepository.AddAsync(history, ct);

        return subscription;
    }

    private static PaymentTransaction CreateTransaction(
        PaymentIntent intent,
        Guid subscriptionId,
        string providerRef,
        PayOsWebhookPayload payload,
        PaymentStatus status,
        string failureReason)
    {
        return new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = intent.UserId,
            SubscriptionId = subscriptionId,
            PaymentIntentId = intent.Id,
            Amount = intent.Amount,
            Currency = string.IsNullOrWhiteSpace(payload?.Data?.Currency) ? intent.Currency : payload.Data.Currency,
            Status = status,
            PaymentMethod = "bank_transfer",
            Provider = "PAYOS",
            ProviderRef = providerRef,
            ExternalTxnId = payload?.Data?.Reference,
            InvoiceUrl = intent.CheckoutUrl,
            FailureReason = failureReason,
        };
    }

    private static string ResolveProviderRef(PayOsWebhookData data)
    {
        if (!string.IsNullOrWhiteSpace(data?.PaymentLinkId))
        {
            return data.PaymentLinkId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(data?.Reference))
        {
            return data.Reference.Trim();
        }

        return data?.OrderCode.ToString() ?? string.Empty;
    }

    private static bool IsSucceeded(PayOsWebhookPayload payload)
    {
        if (payload?.Data == null)
        {
            return false;
        }

        if (payload.Success)
        {
            return true;
        }

        if (string.Equals(payload.Code, "00", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(payload.Data.Code, "00", StringComparison.OrdinalIgnoreCase);
    }

    private static PaymentIntentStatus ResolveFailureStatus(PayOsWebhookPayload payload)
    {
        var description = payload?.Desc ?? payload?.Data?.Desc ?? string.Empty;
        if (description.Contains("expire", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentIntentStatus.Expired;
        }

        return PaymentIntentStatus.Canceled;
    }
}
