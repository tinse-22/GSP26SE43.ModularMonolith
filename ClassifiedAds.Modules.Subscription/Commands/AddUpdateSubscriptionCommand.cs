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

public class AddUpdateSubscriptionCommand : ICommand
{
    public Guid? SubscriptionId { get; set; }

    public CreateUpdateSubscriptionModel Model { get; set; }

    public Guid SavedSubscriptionId { get; set; }
}

public class AddUpdateSubscriptionCommandHandler : ICommandHandler<AddUpdateSubscriptionCommand>
{
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<SubscriptionHistory, Guid> _historyRepository;

    public AddUpdateSubscriptionCommandHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<SubscriptionHistory, Guid> historyRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _historyRepository = historyRepository;
    }

    public async Task HandleAsync(AddUpdateSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin đăng ký là bắt buộc.");
        }

        if (command.Model.UserId == Guid.Empty)
        {
            throw new ValidationException("Mã người dùng là bắt buộc.");
        }

        if (command.Model.PlanId == Guid.Empty)
        {
            throw new ValidationException("Mã gói cước là bắt buộc.");
        }

        var targetPlan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == command.Model.PlanId));

        if (targetPlan == null)
        {
            throw new NotFoundException($"Không tìm thấy gói cước với mã '{command.Model.PlanId}'.");
        }

        if (!targetPlan.IsActive)
        {
            throw new ValidationException($"Gói cước '{targetPlan.Name}' chưa được kích hoạt.");
        }

        ValidateBilling(command.Model, targetPlan);

        var existingSubscription = await GetExistingSubscriptionAsync(command, command.Model.UserId);
        var isCreate = existingSubscription == null;
        var oldPlanId = existingSubscription?.PlanId;
        var oldStatus = existingSubscription?.Status;

        var oldPlan = await GetOldPlanIfNeededAsync(oldPlanId, targetPlan.Id);

        var utcNow = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        var effectiveDate = command.Model.StartDate ?? today;

        if (isCreate)
        {
            existingSubscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = command.Model.UserId,
            };
        }
        else if (existingSubscription.UserId != command.Model.UserId)
        {
            throw new ValidationException("Không thể thay đổi chủ sở hữu của đăng ký hiện có.");
        }

        existingSubscription.PlanId = targetPlan.Id;
        existingSubscription.BillingCycle = command.Model.BillingCycle;
        existingSubscription.StartDate = effectiveDate;
        existingSubscription.AutoRenew = command.Model.AutoRenew;
        existingSubscription.ExternalSubId = command.Model.ExternalSubId?.Trim();
        existingSubscription.ExternalCustId = command.Model.ExternalCustId?.Trim();
        existingSubscription.CancelledAt = null;

        // Snapshot plan details at activation time
        existingSubscription.SnapshotPriceMonthly = targetPlan.PriceMonthly;
        existingSubscription.SnapshotPriceYearly = targetPlan.PriceYearly;
        existingSubscription.SnapshotCurrency = targetPlan.Currency;
        existingSubscription.SnapshotPlanName = targetPlan.DisplayName ?? targetPlan.Name;

        ApplyLifecycleFields(existingSubscription, command.Model, utcNow);

        var history = new SubscriptionHistory
        {
            Id = Guid.NewGuid(),
            SubscriptionId = existingSubscription.Id,
            OldPlanId = oldPlanId,
            NewPlanId = existingSubscription.PlanId,
            ChangeType = ResolveChangeType(isCreate, oldStatus, oldPlan, targetPlan, command.Model.BillingCycle),
            ChangeReason = command.Model.ChangeReason?.Trim(),
            EffectiveDate = effectiveDate,
        };

        await _subscriptionRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (isCreate)
            {
                await _subscriptionRepository.AddAsync(existingSubscription, ct);
            }
            else
            {
                await _subscriptionRepository.UpdateAsync(existingSubscription, ct);
            }

            await _historyRepository.AddAsync(history, ct);
            await _subscriptionRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        command.SavedSubscriptionId = existingSubscription.Id;
    }

    private async Task<UserSubscription> GetExistingSubscriptionAsync(AddUpdateSubscriptionCommand command, Guid userId)
    {
        if (command.SubscriptionId.HasValue && command.SubscriptionId.Value != Guid.Empty)
        {
            var byId = await _subscriptionRepository.FirstOrDefaultAsync(
                _subscriptionRepository.GetQueryableSet().Where(x => x.Id == command.SubscriptionId.Value));
            if (byId == null)
            {
                throw new NotFoundException($"Không tìm thấy đăng ký với mã '{command.SubscriptionId.Value}'.");
            }

            return byId;
        }

        return await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedDateTime));
    }

    private async Task<SubscriptionPlan> GetOldPlanIfNeededAsync(Guid? oldPlanId, Guid newPlanId)
    {
        if (!oldPlanId.HasValue || oldPlanId.Value == Guid.Empty || oldPlanId.Value == newPlanId)
        {
            return null;
        }

        return await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == oldPlanId.Value));
    }

    private static void ValidateBilling(CreateUpdateSubscriptionModel model, SubscriptionPlan plan)
    {
        if (model.IsTrial)
        {
            if (model.TrialDays <= 0)
            {
                throw new ValidationException("Số ngày dùng thử phải lớn hơn 0 khi kích hoạt dùng thử.");
            }

            return;
        }

        var isPaidPlan = (plan.PriceMonthly ?? 0) > 0 || (plan.PriceYearly ?? 0) > 0;
        if (!isPaidPlan)
        {
            return;
        }

        if (!model.BillingCycle.HasValue)
        {
            throw new ValidationException("Chu kỳ thanh toán là bắt buộc cho gói cước trả phí.");
        }

        if (model.BillingCycle == Entities.BillingCycle.Monthly && !plan.PriceMonthly.HasValue)
        {
            throw new ValidationException("Gói cước này không hỗ trợ thanh toán theo tháng.");
        }

        if (model.BillingCycle == Entities.BillingCycle.Yearly && !plan.PriceYearly.HasValue)
        {
            throw new ValidationException("Gói cước này không hỗ trợ thanh toán theo năm.");
        }
    }

    private static void ApplyLifecycleFields(
        UserSubscription subscription,
        CreateUpdateSubscriptionModel model,
        DateTimeOffset utcNow)
    {
        if (model.IsTrial)
        {
            subscription.Status = SubscriptionStatus.Trial;
            subscription.TrialEndsAt = utcNow.AddDays(model.TrialDays);

            var trialEndDate = DateOnly.FromDateTime(subscription.TrialEndsAt.Value.UtcDateTime);
            subscription.NextBillingDate = trialEndDate;
            subscription.EndDate = trialEndDate;
            return;
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.TrialEndsAt = null;

        if (!model.BillingCycle.HasValue)
        {
            subscription.NextBillingDate = null;
            subscription.EndDate = null;
            return;
        }

        var nextBillingDate = model.BillingCycle == Entities.BillingCycle.Yearly
            ? subscription.StartDate.AddYears(1)
            : subscription.StartDate.AddMonths(1);

        subscription.NextBillingDate = nextBillingDate;
        subscription.EndDate = nextBillingDate;
    }

    private static ChangeType ResolveChangeType(
        bool isCreate,
        SubscriptionStatus? oldStatus,
        SubscriptionPlan oldPlan,
        SubscriptionPlan newPlan,
        BillingCycle? billingCycle)
    {
        if (isCreate)
        {
            return ChangeType.Created;
        }

        if (oldStatus == SubscriptionStatus.Cancelled || oldStatus == SubscriptionStatus.Expired)
        {
            return ChangeType.Reactivated;
        }

        if (oldPlan == null)
        {
            return ChangeType.Reactivated;
        }

        var oldPrice = GetComparablePrice(oldPlan, billingCycle);
        var newPrice = GetComparablePrice(newPlan, billingCycle);
        return newPrice >= oldPrice ? ChangeType.Upgraded : ChangeType.Downgraded;
    }

    private static decimal GetComparablePrice(SubscriptionPlan plan, BillingCycle? billingCycle)
    {
        if (billingCycle == Entities.BillingCycle.Yearly)
        {
            if (plan.PriceYearly.HasValue)
            {
                return plan.PriceYearly.Value;
            }

            if (plan.PriceMonthly.HasValue)
            {
                return plan.PriceMonthly.Value * 12;
            }
        }
        else
        {
            if (plan.PriceMonthly.HasValue)
            {
                return plan.PriceMonthly.Value;
            }

            if (plan.PriceYearly.HasValue)
            {
                return plan.PriceYearly.Value / 12;
            }
        }

        return 0;
    }
}
