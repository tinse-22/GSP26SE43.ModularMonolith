using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class SyncPaymentFromPayOsCommand : ICommand
{
    public Guid UserId { get; set; }

    public Guid IntentId { get; set; }

    public string Status { get; set; }

    public string PayOsStatus { get; set; }
}

public class SyncPaymentFromPayOsCommandHandler : ICommandHandler<SyncPaymentFromPayOsCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly IPayOsService _payOsService;

    public SyncPaymentFromPayOsCommandHandler(
        Dispatcher dispatcher,
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IPayOsService payOsService)
    {
        _dispatcher = dispatcher;
        _paymentIntentRepository = paymentIntentRepository;
        _payOsService = payOsService;
    }

    public async Task HandleAsync(SyncPaymentFromPayOsCommand command, CancellationToken cancellationToken = default)
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
            _paymentIntentRepository.GetQueryableSet().Where(x => x.Id == command.IntentId && x.UserId == command.UserId));

        if (intent == null)
        {
            throw new NotFoundException("Payment intent not found.");
        }

        if (!intent.OrderCode.HasValue)
        {
            throw new ValidationException("Payment intent does not have an order code yet.");
        }

        if (intent.Status == PaymentIntentStatus.Succeeded)
        {
            command.Status = "already_succeeded";
            command.PayOsStatus = "PAID";
            return;
        }

        var paymentInfo = await _payOsService.GetPaymentInfoAsync(intent.OrderCode.Value, cancellationToken);
        command.PayOsStatus = paymentInfo?.Status;

        if (IsSucceededStatus(paymentInfo?.Status))
        {
            var webhookPayload = new PayOsWebhookPayload
            {
                Code = "00",
                Desc = "synced",
                Success = true,
                Data = new PayOsWebhookData
                {
                    OrderCode = intent.OrderCode.Value,
                    Amount = (int)paymentInfo.Amount,
                    Description = $"sync-{intent.Id.ToString("N")[..8]}",
                    Reference = paymentInfo.Reference,
                    TransactionDateTime = paymentInfo.TransactionDateTime,
                    Currency = intent.Currency,
                    PaymentLinkId = paymentInfo.Id,
                    Code = "00",
                    Desc = "synced",
                },
                Signature = "sync",
            };

            var processWebhookCommand = new HandlePayOsWebhookCommand
            {
                Payload = webhookPayload,
                RawBody = "{}",
                SkipSignatureVerification = true,
            };

            await _dispatcher.DispatchAsync(processWebhookCommand, cancellationToken);

            command.Status = processWebhookCommand.Outcome == PayOsWebhookOutcome.Processed
                ? "synced"
                : "ignored";

            return;
        }

        if (IsCanceledStatus(paymentInfo?.Status))
        {
            intent.Status = PaymentIntentStatus.Canceled;
            await _paymentIntentRepository.UpdateAsync(intent, cancellationToken);
            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            command.Status = "updated";
            return;
        }

        if (IsExpiredStatus(paymentInfo?.Status))
        {
            intent.Status = PaymentIntentStatus.Expired;
            await _paymentIntentRepository.UpdateAsync(intent, cancellationToken);
            await _paymentIntentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            command.Status = "updated";
            return;
        }

        command.Status = "pending";
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