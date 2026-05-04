using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.AuditLog.Entities;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.WebAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;
    private readonly IRepository<UsageTracking, Guid> _usageTrackingRepository;
    private readonly IRepository<TestRun, Guid> _testRunRepository;
    private readonly IRepository<ClassifiedAds.Modules.AuditLog.Entities.AuditLogEntry, Guid> _auditLogRepository;
    private readonly Dispatcher _dispatcher;

    public AdminDashboardController(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PaymentTransaction, Guid> paymentTransactionRepository,
        IRepository<UsageTracking, Guid> usageTrackingRepository,
        IRepository<TestRun, Guid> testRunRepository,
        IRepository<ClassifiedAds.Modules.AuditLog.Entities.AuditLogEntry, Guid> auditLogRepository,
        Dispatcher dispatcher)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _paymentTransactionRepository = paymentTransactionRepository;
        _usageTrackingRepository = usageTrackingRepository;
        _testRunRepository = testRunRepository;
        _auditLogRepository = auditLogRepository;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<AdminDashboardModel>> Get(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var last7DaysStart = now.AddDays(-7);
        var todayDate = DateOnly.FromDateTime(now.UtcDateTime);

        var subscriptions = await _subscriptionRepository.ToListAsync(
            _subscriptionRepository.GetQueryableSet().AsNoTracking());

        var activeSubscriptions = subscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToList();
        var trialSubscriptions = subscriptions.Where(x => x.Status == SubscriptionStatus.Trial).ToList();

        var planIds = subscriptions.Select(x => x.PlanId).Distinct().ToList();
        var plans = planIds.Count == 0
            ? new List<SubscriptionPlan>()
            : await _planRepository.ToListAsync(
                _planRepository.GetQueryableSet().AsNoTracking().Where(x => planIds.Contains(x.Id)));
        var planLookup = plans.ToDictionary(x => x.Id, x => x);

        var revenueSummary = BuildRevenueSummary(activeSubscriptions.Concat(trialSubscriptions), planLookup);

        var failedTransactions = await _paymentTransactionRepository.ToListAsync(
            _paymentTransactionRepository.GetQueryableSet()
                .AsNoTracking()
                .Where(x => x.Status == PaymentStatus.Failed));

        var failedSummary = new FailedTransactionSummaryModel
        {
            TotalFailed = failedTransactions.Count,
            TopFailureReasons = failedTransactions
                .GroupBy(x => string.IsNullOrWhiteSpace(x.FailureReason) ? "Unknown" : x.FailureReason.Trim())
                .Select(x => new FailureReasonModel
                {
                    Reason = x.Key,
                    Count = x.Count(),
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Reason)
                .Take(5)
                .ToList(),
        };

        var testRunsQuery = _testRunRepository.GetQueryableSet().AsNoTracking();
        var testRunsToday = await testRunsQuery.CountAsync(x => x.CreatedDateTime >= todayStart, ct);
        var testRunsLast7Days = await testRunsQuery.CountAsync(x => x.CreatedDateTime >= last7DaysStart, ct);

        var usageEntries = await _usageTrackingRepository.ToListAsync(
            _usageTrackingRepository.GetQueryableSet()
                .AsNoTracking()
                .Where(x => x.PeriodStart <= todayDate && x.PeriodEnd >= todayDate));

        var users = await _dispatcher.DispatchAsync(new GetUsersQuery { AsNoTracking = true }, ct);
        var userLookup = users.ToDictionary(x => x.Id, x => ResolveUserName(x.UserName, x.Email));

        var topTestRuns = BuildTopUsers(
            usageEntries,
            userLookup,
            x => x.TestRunCount,
            5);

        var topLlmCalls = BuildTopUsers(
            usageEntries,
            userLookup,
            x => x.LlmCallCount,
            5);

        var topStorage = BuildTopUsers(
            usageEntries,
            userLookup,
            x => x.StorageUsedMB,
            5);

        var recentActions = await _auditLogRepository.ToListAsync(
            _auditLogRepository.GetQueryableSet()
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDateTime)
                .Take(10));

        var recentActionModels = recentActions
            .Select(x => new AdminActionModel
            {
                Action = x.Action,
                UserName = ResolveUserName(userLookup, x.UserId),
                CreatedDateTime = x.CreatedDateTime,
            })
            .ToList();

        var response = new AdminDashboardModel
        {
            Subscription = new SubscriptionSummaryModel
            {
                ActiveCount = activeSubscriptions.Count,
                TrialCount = trialSubscriptions.Count,
            },
            Revenue = revenueSummary,
            FailedTransactions = failedSummary,
            TestRuns = new TestRunSummaryModel
            {
                Today = testRunsToday,
                Last7Days = testRunsLast7Days,
            },
            Usage = new UsageSummaryModel
            {
                LlmCallsThisMonth = usageEntries.Sum(x => x.LlmCallCount),
                StorageUsedMB = usageEntries.Sum(x => x.StorageUsedMB),
            },
            TopUsersByTestRuns = topTestRuns,
            TopUsersByLlmCalls = topLlmCalls,
            TopUsersByStorage = topStorage,
            RecentAdminActions = recentActionModels,
        };

        return Ok(response);
    }

    private static RevenueSummaryModel BuildRevenueSummary(
        IEnumerable<UserSubscription> subscriptions,
        Dictionary<Guid, SubscriptionPlan> planLookup)
    {
        decimal mrr = 0;
        decimal arr = 0;
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var subscription in subscriptions)
        {
            if (!planLookup.TryGetValue(subscription.PlanId, out var plan))
            {
                plan = null;
            }

            var currency = subscription.SnapshotCurrency
                ?? plan?.Currency
                ?? "VND";
            if (!string.IsNullOrWhiteSpace(currency))
            {
                currencies.Add(currency.Trim().ToUpperInvariant());
            }

            var monthlyPrice = subscription.SnapshotPriceMonthly
                ?? plan?.PriceMonthly
                ?? 0m;
            var yearlyPrice = subscription.SnapshotPriceYearly
                ?? plan?.PriceYearly
                ?? 0m;

            if (subscription.BillingCycle == BillingCycle.Yearly)
            {
                mrr += yearlyPrice / 12m;
                arr += yearlyPrice;
            }
            else
            {
                mrr += monthlyPrice;
                arr += monthlyPrice * 12m;
            }
        }

        return new RevenueSummaryModel
        {
            Mrr = mrr,
            Arr = arr,
            Currency = currencies.Count == 1 ? currencies.First() : "MIXED",
        };
    }

    private static List<TopUserMetricModel> BuildTopUsers(
        List<UsageTracking> usageEntries,
        Dictionary<Guid, string> userLookup,
        Func<UsageTracking, decimal> selector,
        int take)
    {
        return usageEntries
            .Select(x => new TopUserMetricModel
            {
                UserId = x.UserId,
                UserName = ResolveUserName(userLookup, x.UserId),
                Value = selector(x),
            })
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.UserName)
            .Take(take)
            .ToList();
    }

    private static string ResolveUserName(string userName, string email)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return "Unknown";
    }

    private static string ResolveUserName(Dictionary<Guid, string> lookup, Guid userId)
    {
        if (lookup != null && lookup.TryGetValue(userId, out var userName))
        {
            return userName;
        }

        return "Unknown";
    }
}
