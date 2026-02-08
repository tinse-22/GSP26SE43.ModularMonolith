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

public class CancelSubscriptionCommand : ICommand
{
    public Guid SubscriptionId { get; set; }

    public CancelSubscriptionModel Model { get; set; }
}

public class CancelSubscriptionCommandHandler : ICommandHandler<CancelSubscriptionCommand>
{
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionHistory, Guid> _historyRepository;

    public CancelSubscriptionCommandHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionHistory, Guid> historyRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _historyRepository = historyRepository;
    }

    public async Task HandleAsync(CancelSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.SubscriptionId == Guid.Empty)
        {
            throw new ValidationException("Mã đăng ký là bắt buộc.");
        }

        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet().Where(x => x.Id == command.SubscriptionId));

        if (subscription == null)
        {
            throw new NotFoundException($"Không tìm thấy đăng ký với mã '{command.SubscriptionId}'.");
        }

        if (subscription.Status == SubscriptionStatus.Cancelled)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var effectiveDate = command.Model?.EffectiveDate ?? DateOnly.FromDateTime(utcNow.UtcDateTime);

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = utcNow;
        subscription.AutoRenew = false;
        subscription.EndDate = effectiveDate;
        subscription.NextBillingDate = null;
        subscription.TrialEndsAt = null;

        var history = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            OldPlanId = subscription.PlanId,
            NewPlanId = subscription.PlanId,
            ChangeType = ChangeType.Cancelled,
            ChangeReason = command.Model?.ChangeReason?.Trim(),
            EffectiveDate = effectiveDate,
        };

        await _subscriptionRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _subscriptionRepository.UpdateAsync(subscription, ct);
            await _historyRepository.AddAsync(history, ct);
            await _subscriptionRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);
    }
}
