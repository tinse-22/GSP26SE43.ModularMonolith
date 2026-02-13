using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class ConsumeLimitAtomicallyCommand : ICommand
{
    public Guid UserId { get; set; }

    public LimitType LimitType { get; set; }

    public decimal IncrementValue { get; set; } = 1;

    /// <summary>
    /// Set by the handler after execution.
    /// </summary>
    public LimitCheckResultDTO Result { get; set; }
}

public class ConsumeLimitAtomicallyCommandHandler : ICommandHandler<ConsumeLimitAtomicallyCommand>
{
    private const int MaxRetries = 3;

    private static readonly SubscriptionStatus[] CurrentStatuses =
    {
        SubscriptionStatus.Trial,
        SubscriptionStatus.Active,
        SubscriptionStatus.PastDue,
    };

    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PlanLimit, Guid> _planLimitRepository;
    private readonly IRepository<UsageTracking, Guid> _usageTrackingRepository;
    private readonly ILogger<ConsumeLimitAtomicallyCommandHandler> _logger;

    public ConsumeLimitAtomicallyCommandHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PlanLimit, Guid> planLimitRepository,
        IRepository<UsageTracking, Guid> usageTrackingRepository,
        ILogger<ConsumeLimitAtomicallyCommandHandler> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _planLimitRepository = planLimitRepository;
        _usageTrackingRepository = usageTrackingRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ConsumeLimitAtomicallyCommand command, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                command.Result = await _usageTrackingRepository.UnitOfWork.ExecuteInTransactionAsync(
                    async ct => await ExecuteAtomicConsume(command, ct),
                    IsolationLevel.Serializable,
                    cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (IsTransientConflict(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Transient conflict on ConsumeLimitAtomically attempt {Attempt}/{MaxRetries} for user {UserId}, limitType {LimitType}. Retrying...",
                    attempt, MaxRetries, command.UserId, command.LimitType);

                // Small backoff before retry
                await Task.Delay(attempt * 50, cancellationToken);
            }
        }
    }

    private async Task<LimitCheckResultDTO> ExecuteAtomicConsume(
        ConsumeLimitAtomicallyCommand command,
        CancellationToken ct)
    {
        // 1. Resolve active subscription
        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet()
                .Where(x => x.UserId == command.UserId && CurrentStatuses.Contains(x.Status))
                .OrderByDescending(x => x.CreatedDateTime));

        if (subscription == null)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Không tìm thấy gói đăng ký đang hoạt động. Vui lòng đăng ký gói dịch vụ.",
            };
        }

        // 2. Resolve plan limit
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(p => p.Id == subscription.PlanId));

        if (plan == null)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Không tìm thấy thông tin gói cước.",
            };
        }

        var planLimit = await _planLimitRepository.FirstOrDefaultAsync(
            _planLimitRepository.GetQueryableSet()
                .Where(l => l.PlanId == plan.Id && l.LimitType == command.LimitType));

        // No limit defined or unlimited → allow without tracking
        if (planLimit == null || planLimit.IsUnlimited || !planLimit.LimitValue.HasValue)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = true,
                IsUnlimited = true,
            };
        }

        // 3. Calculate usage period
        var (periodStart, periodEnd) = GetUsagePeriod(command.LimitType, subscription);

        // 4. Load or create UsageTracking row
        var tracking = await _usageTrackingRepository.FirstOrDefaultAsync(
            _usageTrackingRepository.GetQueryableSet()
                .Where(x => x.UserId == command.UserId
                    && x.PeriodStart == periodStart
                    && x.PeriodEnd == periodEnd));

        bool isNew = tracking == null;
        if (isNew)
        {
            tracking = new UsageTracking
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
            };
        }

        // 5. Validate: currentUsage + increment <= limit
        decimal currentUsage = GetUsageValue(command.LimitType, tracking);
        decimal proposedTotal = currentUsage + command.IncrementValue;

        if (proposedTotal > planLimit.LimitValue.Value)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = false,
                LimitValue = planLimit.LimitValue,
                IsUnlimited = false,
                CurrentUsage = currentUsage,
                DenialReason = BuildDenialReason(command.LimitType, planLimit.LimitValue.Value, currentUsage, plan.DisplayName ?? plan.Name),
            };
        }

        // 6. Persist increment
        IncrementUsageField(command.LimitType, tracking, command.IncrementValue);

        if (isNew)
        {
            await _usageTrackingRepository.AddAsync(tracking, ct);
        }
        else
        {
            await _usageTrackingRepository.UpdateAsync(tracking, ct);
        }

        await _usageTrackingRepository.UnitOfWork.SaveChangesAsync(ct);

        return new LimitCheckResultDTO
        {
            IsAllowed = true,
            LimitValue = planLimit.LimitValue,
            IsUnlimited = false,
            CurrentUsage = proposedTotal,
        };
    }

    private static (DateOnly periodStart, DateOnly periodEnd) GetUsagePeriod(
        LimitType limitType,
        UserSubscription subscription)
    {
        // Cumulative limits: entire subscription lifetime
        if (limitType is LimitType.MaxProjects
                      or LimitType.MaxStorageMB
                      or LimitType.MaxEndpointsPerProject
                      or LimitType.MaxTestCasesPerSuite
                      or LimitType.RetentionDays
                      or LimitType.MaxConcurrentRuns)
        {
            var endDate = subscription.EndDate
                ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(100));
            return (subscription.StartDate, endDate);
        }

        // Periodic limits (monthly): calculate current billing month window
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subStartDay = subscription.StartDate.Day;

        DateOnly periodStart;
        if (today.Day >= subStartDay)
        {
            var day = Math.Min(subStartDay, DateTime.DaysInMonth(today.Year, today.Month));
            periodStart = new DateOnly(today.Year, today.Month, day);
        }
        else
        {
            var prevMonth = today.AddMonths(-1);
            var day = Math.Min(subStartDay, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month));
            periodStart = new DateOnly(prevMonth.Year, prevMonth.Month, day);
        }

        var nextMonth = periodStart.AddMonths(1);
        var periodEnd = nextMonth.AddDays(-1);

        return (periodStart, periodEnd);
    }

    private static decimal GetUsageValue(LimitType limitType, UsageTracking tracking)
    {
        return limitType switch
        {
            LimitType.MaxProjects => tracking.ProjectCount,
            LimitType.MaxEndpointsPerProject => tracking.EndpointCount,
            LimitType.MaxTestCasesPerSuite => tracking.TestCaseCount,
            LimitType.MaxTestRunsPerMonth => tracking.TestRunCount,
            LimitType.MaxLlmCallsPerMonth => tracking.LlmCallCount,
            LimitType.MaxStorageMB => tracking.StorageUsedMB,
            _ => 0,
        };
    }

    private static void IncrementUsageField(LimitType limitType, UsageTracking tracking, decimal increment)
    {
        switch (limitType)
        {
            case LimitType.MaxProjects:
                tracking.ProjectCount += (int)increment;
                break;
            case LimitType.MaxEndpointsPerProject:
                tracking.EndpointCount += (int)increment;
                break;
            case LimitType.MaxTestCasesPerSuite:
                tracking.TestCaseCount += (int)increment;
                break;
            case LimitType.MaxTestRunsPerMonth:
                tracking.TestRunCount += (int)increment;
                break;
            case LimitType.MaxLlmCallsPerMonth:
                tracking.LlmCallCount += (int)increment;
                break;
            case LimitType.MaxStorageMB:
                tracking.StorageUsedMB += increment;
                break;
        }
    }

    private static bool IsTransientConflict(DbUpdateException ex)
    {
        // PostgreSQL error codes:
        // 40001 = serialization_failure
        // 23505 = unique_violation
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("40001") || message.Contains("23505")
            || message.Contains("serialization_failure", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique_violation", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDenialReason(
        LimitType limitType,
        int limitValue,
        decimal currentUsage,
        string planName)
    {
        var limitLabel = limitType switch
        {
            LimitType.MaxProjects => "số lượng project",
            LimitType.MaxStorageMB => "dung lượng lưu trữ (MB)",
            LimitType.MaxEndpointsPerProject => "số endpoint mỗi project",
            LimitType.MaxTestCasesPerSuite => "số test case mỗi suite",
            LimitType.MaxTestRunsPerMonth => "số lần chạy test mỗi tháng",
            LimitType.MaxLlmCallsPerMonth => "số lần gọi LLM mỗi tháng",
            _ => limitType.ToString(),
        };

        return $"Bạn đã đạt giới hạn {limitLabel} ({currentUsage}/{limitValue}) của gói {planName}. Vui lòng nâng cấp gói để tiếp tục.";
    }
}
