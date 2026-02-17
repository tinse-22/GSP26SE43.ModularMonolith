using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Services;

public class SubscriptionLimitGatewayService : ISubscriptionLimitGatewayService
{
    private readonly Dispatcher _dispatcher;

    public SubscriptionLimitGatewayService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<LimitCheckResultDTO> CheckLimitAsync(
        Guid userId,
        LimitType limitType,
        decimal proposedIncrement = 1,
        CancellationToken cancellationToken = default)
    {
        // 1. Get user's active subscription
        var subscription = await _dispatcher.DispatchAsync(
            new GetCurrentSubscriptionByUserQuery { UserId = userId },
            cancellationToken);

        if (subscription == null)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Không tìm thấy gói đăng ký đang hoạt động. Vui lòng đăng ký gói dịch vụ.",
            };
        }

        // 2. Get the plan with its limits
        var plan = await _dispatcher.DispatchAsync(
            new GetPlanQuery { Id = subscription.PlanId },
            cancellationToken);

        if (plan == null)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = false,
                DenialReason = "Không tìm thấy thông tin gói cước.",
            };
        }

        // 3. Find the specific limit for this LimitType
        var planLimit = plan.Limits?.FirstOrDefault(l => l.LimitType == limitType);

        // No limit defined for this type → treat as unlimited
        if (planLimit == null || planLimit.IsUnlimited)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = true,
                IsUnlimited = true,
            };
        }

        if (!planLimit.LimitValue.HasValue)
        {
            return new LimitCheckResultDTO
            {
                IsAllowed = true,
                IsUnlimited = true,
            };
        }

        // 4. Get current usage for the appropriate period
        var (periodStart, periodEnd) = GetUsagePeriod(limitType, subscription);

        var usageList = await _dispatcher.DispatchAsync(
            new GetUsageTrackingsQuery
            {
                UserId = userId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
            },
            cancellationToken);

        decimal currentUsage = GetUsageValue(limitType, usageList);

        // 5. Check: currentUsage + proposedIncrement <= limitValue
        bool isAllowed = (currentUsage + proposedIncrement) <= planLimit.LimitValue.Value;

        return new LimitCheckResultDTO
        {
            IsAllowed = isAllowed,
            LimitValue = planLimit.LimitValue,
            IsUnlimited = false,
            CurrentUsage = currentUsage,
            DenialReason = isAllowed
                ? null
                : BuildDenialReason(limitType, planLimit.LimitValue.Value, currentUsage, plan.DisplayName ?? plan.Name),
        };
    }

    public async Task IncrementUsageAsync(
        IncrementUsageRequest request,
        CancellationToken cancellationToken = default)
    {
        // Get subscription to determine billing period
        var subscription = await _dispatcher.DispatchAsync(
            new GetCurrentSubscriptionByUserQuery { UserId = request.UserId },
            cancellationToken);

        if (subscription == null)
        {
            return; // No subscription, nothing to track
        }

        var (periodStart, periodEnd) = GetUsagePeriod(request.LimitType, subscription);

        var model = new UpsertUsageTrackingModel
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ReplaceValues = false, // Increment mode
        };

        // Set the appropriate field based on LimitType
        switch (request.LimitType)
        {
            case LimitType.MaxProjects:
                model.ProjectCount = (int)request.IncrementValue;
                break;
            case LimitType.MaxEndpointsPerProject:
                model.EndpointCount = (int)request.IncrementValue;
                break;
            case LimitType.MaxTestCasesPerSuite:
                model.TestCaseCount = (int)request.IncrementValue;
                break;
            case LimitType.MaxTestRunsPerMonth:
                model.TestRunCount = (int)request.IncrementValue;
                break;
            case LimitType.MaxLlmCallsPerMonth:
                model.LlmCallCount = (int)request.IncrementValue;
                break;
            case LimitType.MaxStorageMB:
                model.StorageUsedMB = request.IncrementValue;
                break;
        }

        await _dispatcher.DispatchAsync(
            new UpsertUsageTrackingCommand
            {
                UserId = request.UserId,
                Model = model,
            },
            cancellationToken);
    }

    private static (DateOnly periodStart, DateOnly periodEnd) GetUsagePeriod(
        LimitType limitType,
        SubscriptionModel subscription)
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

    private static decimal GetUsageValue(LimitType limitType, List<UsageTrackingModel> usageList)
    {
        if (usageList == null || usageList.Count == 0)
        {
            return 0;
        }

        return limitType switch
        {
            LimitType.MaxProjects => usageList.Sum(u => u.ProjectCount),
            LimitType.MaxEndpointsPerProject => usageList.Sum(u => u.EndpointCount),
            LimitType.MaxTestCasesPerSuite => usageList.Sum(u => u.TestCaseCount),
            LimitType.MaxTestRunsPerMonth => usageList.Sum(u => u.TestRunCount),
            LimitType.MaxLlmCallsPerMonth => usageList.Sum(u => u.LlmCallCount),
            LimitType.MaxStorageMB => usageList.Sum(u => u.StorageUsedMB),
            _ => 0,
        };
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

    public async Task<LimitCheckResultDTO> TryConsumeLimitAsync(
        Guid userId,
        LimitType limitType,
        decimal incrementValue = 1,
        CancellationToken cancellationToken = default)
    {
        var command = new ConsumeLimitAtomicallyCommand
        {
            UserId = userId,
            LimitType = limitType,
            IncrementValue = incrementValue,
        };

        await _dispatcher.DispatchAsync(command, cancellationToken);

        return command.Result;
    }
}
