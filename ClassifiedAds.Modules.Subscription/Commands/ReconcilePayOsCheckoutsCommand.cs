using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Constants;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.IntegrationEvents;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Outbox;
using ClassifiedAds.Modules.Subscription.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class ReconcilePayOsCheckoutsCommand : ICommand
{
    public int ExaminedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SyncedCount { get; set; }
}

public class ReconcilePayOsCheckoutsCommandHandler : ICommandHandler<ReconcilePayOsCheckoutsCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly IRepository<OutboxMessage, Guid> _outboxMessageRepository;
    private readonly IPayOsService _payOsService;
    private readonly PayOsOptions _payOsOptions;
    private readonly ILogger<ReconcilePayOsCheckoutsCommandHandler> _logger;

    public ReconcilePayOsCheckoutsCommandHandler(
        Dispatcher dispatcher,
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IRepository<OutboxMessage, Guid> outboxMessageRepository,
        IPayOsService payOsService,
        IOptions<PayOsOptions> payOsOptions,
        ILogger<ReconcilePayOsCheckoutsCommandHandler> logger)
    {
        _dispatcher = dispatcher;
        _paymentIntentRepository = paymentIntentRepository;
        _outboxMessageRepository = outboxMessageRepository;
        _payOsService = payOsService;
        _payOsOptions = payOsOptions?.Value ?? new PayOsOptions();
        _logger = logger;
    }

    public async Task HandleAsync(ReconcilePayOsCheckoutsCommand command, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, _payOsOptions.CheckoutReconcileBatchSize);
        var lookbackFrom = DateTimeOffset.UtcNow.AddHours(-Math.Max(1, _payOsOptions.CheckoutReconcileLookbackHours));

        var intents = await _paymentIntentRepository.ToListAsync(
            _paymentIntentRepository.GetQueryableSet()
                .Where(x => x.OrderCode.HasValue)
                .Where(x => x.CreatedDateTime >= lookbackFrom)
                .Where(x => x.Status == PaymentIntentStatus.RequiresPayment || x.Status == PaymentIntentStatus.Processing)
                .Where(x => string.IsNullOrWhiteSpace(x.CheckoutUrl) || x.Status == PaymentIntentStatus.Processing)
                .OrderBy(x => x.CreatedDateTime)
                .Take(batchSize));

        command.ExaminedCount = intents.Count;
        if (intents.Count == 0)
        {
            return;
        }

        foreach (var intent in intents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;

            if (intent.ExpiresAt <= now)
            {
                if (await UpdateIntentStatusAsync(
                    intent.Id,
                    PaymentIntentStatus.Expired,
                    provider: "PAYOS",
                    providerStatus: "EXPIRED",
                    reason: "reconcile_expired",
                    cancellationToken))
                {
                    command.UpdatedCount++;
                }

                continue;
            }

            PayOsGetPaymentData paymentInfo;
            try
            {
                paymentInfo = await _payOsService.GetPaymentInfoAsync(intent.OrderCode!.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not reconcile PayOS payment intent {IntentId} with orderCode {OrderCode}.",
                    intent.Id,
                    intent.OrderCode);
                continue;
            }

            if (IsSucceededStatus(paymentInfo.Status))
            {
                var syncCommand = new SyncPaymentFromPayOsCommand
                {
                    UserId = intent.UserId,
                    IntentId = intent.Id,
                };
                await _dispatcher.DispatchAsync(syncCommand, cancellationToken);
                if (string.Equals(syncCommand.Status, "synced", StringComparison.OrdinalIgnoreCase))
                {
                    command.SyncedCount++;
                }

                continue;
            }

            if (IsCanceledStatus(paymentInfo.Status))
            {
                if (await UpdateIntentStatusAsync(
                    intent.Id,
                    PaymentIntentStatus.Canceled,
                    provider: "PAYOS",
                    providerStatus: paymentInfo.Status,
                    reason: "reconcile_canceled",
                    cancellationToken))
                {
                    command.UpdatedCount++;
                }

                continue;
            }

            if (IsExpiredStatus(paymentInfo.Status))
            {
                if (await UpdateIntentStatusAsync(
                    intent.Id,
                    PaymentIntentStatus.Expired,
                    provider: "PAYOS",
                    providerStatus: paymentInfo.Status,
                    reason: "reconcile_expired",
                    cancellationToken))
                {
                    command.UpdatedCount++;
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(paymentInfo.CheckoutUrl))
            {
                if (await UpdateCheckoutLinkAsync(intent.Id, paymentInfo.CheckoutUrl, cancellationToken))
                {
                    command.UpdatedCount++;
                }
            }
        }
    }

    private async Task<bool> UpdateIntentStatusAsync(
        Guid intentId,
        PaymentIntentStatus newStatus,
        string provider,
        string providerStatus,
        string reason,
        CancellationToken cancellationToken)
    {
        return await _paymentIntentRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var intent = await _paymentIntentRepository.FirstOrDefaultAsync(
                _paymentIntentRepository.GetQueryableSet().Where(x => x.Id == intentId));
            if (intent == null || intent.Status == newStatus)
            {
                return false;
            }

            var oldStatus = intent.Status;
            intent.Status = newStatus;
            await _paymentIntentRepository.UpdateAsync(intent, ct);

            var statusChangedEvent = new PaymentIntentStatusChangedOutboxEvent
            {
                IntentId = intent.Id,
                UserId = intent.UserId,
                OrderCode = intent.OrderCode,
                OldStatus = oldStatus,
                NewStatus = intent.Status,
                Provider = provider,
                ProviderStatus = providerStatus,
                Reason = reason,
            };

            await _outboxMessageRepository.AddAsync(
                OutboxMessageFactory.Create(
                    EventTypeConstants.PaymentIntentStatusChanged,
                    intent.UserId,
                    intent.Id,
                    statusChangedEvent),
                ct);

            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken: cancellationToken);
    }

    private async Task<bool> UpdateCheckoutLinkAsync(Guid intentId, string checkoutUrl, CancellationToken cancellationToken)
    {
        return await _paymentIntentRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var intent = await _paymentIntentRepository.FirstOrDefaultAsync(
                _paymentIntentRepository.GetQueryableSet().Where(x => x.Id == intentId));
            if (intent == null)
            {
                return false;
            }

            var shouldUpdate = string.IsNullOrWhiteSpace(intent.CheckoutUrl);
            if (!shouldUpdate)
            {
                return false;
            }

            intent.CheckoutUrl = checkoutUrl.Trim();
            if (intent.Status == PaymentIntentStatus.RequiresPayment)
            {
                intent.Status = PaymentIntentStatus.Processing;
            }

            await _paymentIntentRepository.UpdateAsync(intent, ct);

            var checkoutLinkCreatedEvent = new PaymentCheckoutLinkCreatedOutboxEvent
            {
                IntentId = intent.Id,
                UserId = intent.UserId,
                OrderCode = intent.OrderCode!.Value,
                CheckoutUrl = intent.CheckoutUrl,
                Status = intent.Status,
            };

            await _outboxMessageRepository.AddAsync(
                OutboxMessageFactory.Create(
                    EventTypeConstants.PaymentCheckoutLinkCreated,
                    intent.UserId,
                    intent.Id,
                    checkoutLinkCreatedEvent),
                ct);

            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken: cancellationToken);
    }

    private static bool IsSucceededStatus(string status)
    {
        return string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCanceledStatus(string status)
    {
        return string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExpiredStatus(string status)
    {
        return string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "TIMEOUT", StringComparison.OrdinalIgnoreCase);
    }
}
