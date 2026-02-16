using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.DTOs;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Contracts.Notification.DTOs;
using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class AddUpdatePlanCommand : ICommand
{
    public Guid? PlanId { get; set; }

    public CreateUpdatePlanModel Model { get; set; }

    public Guid SavedPlanId { get; set; }
}

public class AddUpdatePlanCommandHandler : ICommandHandler<AddUpdatePlanCommand>
{
    private static readonly SubscriptionStatus[] ActiveStatuses =
    {
        SubscriptionStatus.Trial,
        SubscriptionStatus.Active,
        SubscriptionStatus.PastDue,
    };

    private readonly ICrudService<SubscriptionPlan> _planService;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PlanLimit, Guid> _limitRepository;
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IUserService _userService;
    private readonly IEmailMessageService _emailMessageService;
    private readonly ILogger<AddUpdatePlanCommandHandler> _logger;

    public AddUpdatePlanCommandHandler(
        ICrudService<SubscriptionPlan> planService,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PlanLimit, Guid> limitRepository,
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IUserService userService,
        IEmailMessageService emailMessageService,
        ILogger<AddUpdatePlanCommandHandler> logger)
    {
        _planService = planService;
        _planRepository = planRepository;
        _limitRepository = limitRepository;
        _subscriptionRepository = subscriptionRepository;
        _userService = userService;
        _emailMessageService = emailMessageService;
        _logger = logger;
    }

    public async Task HandleAsync(AddUpdatePlanCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Model == null)
        {
            throw new ValidationException("Thông tin gói cước là bắt buộc.");
        }

        var isCreate = !command.PlanId.HasValue || command.PlanId == Guid.Empty;
        var plan = isCreate
            ? command.Model.ToEntity()
            : await GetExistingPlanAsync(command.PlanId.Value, cancellationToken);

        var oldPricing = isCreate ? null : PlanPricingSnapshot.From(plan);
        var activeSubscriberUserIds = isCreate
            ? new List<Guid>()
            : await GetActiveSubscriberUserIdsAsync(plan.Id, cancellationToken);

        if (!isCreate && plan.IsActive && !command.Model.IsActive && activeSubscriberUserIds.Count > 0)
        {
            throw new ValidationException(
                $"Không thể ngừng kích hoạt gói '{plan.DisplayName}' vì vẫn còn {activeSubscriberUserIds.Count} thuê bao đang hoạt động. Vui lòng chuyển thuê bao sang gói khác trước.");
        }

        if (!isCreate)
        {
            ApplyModel(plan, command.Model);
        }

        var limits = command.Model.ToLimitEntities(plan.Id);
        ValidateLimits(limits);

        try
        {
            await _limitRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await EnsureNameUniquenessAsync(plan.Name, isCreate ? null : plan.Id, ct);
                await _planService.AddOrUpdateAsync(plan, ct);
                await ReplaceLimitsAsync(plan.Id, limits, ct);
            }, cancellationToken: cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicatePlanNameException(ex))
        {
            throw new ValidationException($"Tên gói cước '{command.Model.Name?.Trim()}' đã tồn tại.");
        }

        command.SavedPlanId = plan.Id;

        if (!isCreate
            && oldPricing != null
            && HasPricingChanged(oldPricing, plan)
            && activeSubscriberUserIds.Count > 0)
        {
            await NotifyActiveSubscribersAboutPriceChangeAsync(plan, oldPricing, activeSubscriberUserIds, cancellationToken);
        }
    }

    private async Task<SubscriptionPlan> GetExistingPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == planId));

        if (plan == null)
        {
            throw new NotFoundException($"Không tìm thấy gói cước với mã '{planId}'.");
        }

        return plan;
    }

    private static void ApplyModel(SubscriptionPlan plan, CreateUpdatePlanModel model)
    {
        plan.Name = model.Name?.Trim();
        plan.DisplayName = model.DisplayName?.Trim();
        plan.Description = model.Description?.Trim();
        plan.PriceMonthly = model.PriceMonthly;
        plan.PriceYearly = model.PriceYearly;
        plan.Currency = model.Currency?.Trim().ToUpperInvariant() ?? "USD";
        plan.IsActive = model.IsActive;
        plan.SortOrder = model.SortOrder;
    }

    private async Task EnsureNameUniquenessAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("Tên gói cước là bắt buộc.");
        }

        var query = _planRepository.GetQueryableSet()
            .Where(p => p.Name.ToLower() == normalizedName);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        var existing = await _planRepository.FirstOrDefaultAsync(query);
        if (existing != null)
        {
            throw new ValidationException($"Tên gói cước '{name.Trim()}' đã tồn tại.");
        }
    }

    private async Task ReplaceLimitsAsync(Guid planId, List<PlanLimit> limits, CancellationToken cancellationToken)
    {
        var oldLimits = await _limitRepository.ToListAsync(
            _limitRepository.GetQueryableSet().Where(l => l.PlanId == planId));

        foreach (var oldLimit in oldLimits)
        {
            _limitRepository.Delete(oldLimit);
        }

        foreach (var limit in limits)
        {
            limit.PlanId = planId;
            await _limitRepository.AddAsync(limit, cancellationToken);
        }

        await _limitRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetActiveSubscriberUserIdsAsync(Guid planId, CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptionRepository.ToListAsync(
            _subscriptionRepository.GetQueryableSet()
                .Where(s => s.PlanId == planId && ActiveStatuses.Contains(s.Status)));

        return subscriptions
            .Select(s => s.UserId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private async Task NotifyActiveSubscribersAboutPriceChangeAsync(
        SubscriptionPlan plan,
        PlanPricingSnapshot oldPricing,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userService.GetUsersAsync(new UserQueryOptions());
            var usersToNotify = users
                .Where(u => userIds.Contains(u.Id) && !string.IsNullOrWhiteSpace(u.Email))
                .ToList();

            if (usersToNotify.Count == 0)
            {
                return;
            }

            var planName = string.IsNullOrWhiteSpace(plan.DisplayName) ? plan.Name : plan.DisplayName;
            var subject = $"[ClassifiedAds] Thông báo cập nhật giá gói {planName}";
            var body = BuildPriceChangeEmailBody(plan, oldPricing);

            foreach (var user in usersToNotify)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _emailMessageService.CreateEmailMessageAsync(new EmailMessageDTO
                    {
                        From = "noreply@classifiedads.com",
                        Tos = user.Email,
                        Subject = subject,
                        Body = body,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to send plan price update email for plan {PlanId} to user {UserId}.",
                        plan.Id,
                        user.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send plan price update notifications for plan {PlanId}.",
                plan.Id);
        }
    }

    private static string BuildPriceChangeEmailBody(SubscriptionPlan plan, PlanPricingSnapshot oldPricing)
    {
        var currency = NormalizeCurrency(plan.Currency) ?? oldPricing.Currency ?? "USD";
        var planName = string.IsNullOrWhiteSpace(plan.DisplayName) ? plan.Name : plan.DisplayName;

        var oldMonthly = FormatPrice(oldPricing.PriceMonthly, currency);
        var newMonthly = FormatPrice(plan.PriceMonthly, currency);
        var oldYearly = FormatPrice(oldPricing.PriceYearly, currency);
        var newYearly = FormatPrice(plan.PriceYearly, currency);

        return $@"
<p>Xin chào,</p>
<p>Chúng tôi thông báo giá gói cước <strong>{planName}</strong> đã được cập nhật.</p>
<p>Hàng tháng: <strong>{oldMonthly}</strong> -> <strong>{newMonthly}</strong></p>
<p>Hàng năm: <strong>{oldYearly}</strong> -> <strong>{newYearly}</strong></p>
<p>Đăng ký hiện tại của bạn không bị thay đổi ngay lập tức. Giá mới có thể được áp dụng từ kỳ thanh toán tiếp theo, tùy theo chính sách đăng ký của bạn.</p>
<p>Trân trọng,<br/>Đội ngũ ClassifiedAds</p>";
    }

    private static bool HasPricingChanged(PlanPricingSnapshot oldPricing, SubscriptionPlan plan)
    {
        return oldPricing.PriceMonthly != plan.PriceMonthly
            || oldPricing.PriceYearly != plan.PriceYearly
            || !string.Equals(oldPricing.Currency, NormalizeCurrency(plan.Currency), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPrice(decimal? price, string currency)
    {
        return price.HasValue
            ? $"{price.Value:0.##} {currency}"
            : "N/A";
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? null
            : currency.Trim().ToUpperInvariant();
    }

    private static void ValidateLimits(List<PlanLimit> limits)
    {
        if (limits == null || limits.Count == 0)
        {
            return;
        }

        var duplicates = limits
            .GroupBy(l => l.LimitType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new ValidationException(
                $"Phát hiện loại giới hạn bị trùng: {string.Join(", ", duplicates)}. Mỗi loại chỉ được xuất hiện một lần trong mỗi gói.");
        }

        foreach (var limit in limits)
        {
            if (!limit.IsUnlimited && (!limit.LimitValue.HasValue || limit.LimitValue.Value <= 0))
            {
                throw new ValidationException(
                    $"Giá trị giới hạn phải lớn hơn 0 cho '{limit.LimitType}' khi không được đặt là không giới hạn.");
            }

            if (limit.IsUnlimited)
            {
                limit.LimitValue = null;
            }
        }
    }

    private static bool IsDuplicatePlanNameException(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pgEx)
        {
            return false;
        }

        return pgEx.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(pgEx.ConstraintName, "IX_SubscriptionPlans_Name", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PlanPricingSnapshot
    {
        public decimal? PriceMonthly { get; init; }

        public decimal? PriceYearly { get; init; }

        public string Currency { get; init; }

        public static PlanPricingSnapshot From(SubscriptionPlan plan)
        {
            return new PlanPricingSnapshot
            {
                PriceMonthly = plan.PriceMonthly,
                PriceYearly = plan.PriceYearly,
                Currency = NormalizeCurrency(plan.Currency),
            };
        }
    }
}
