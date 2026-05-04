using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.Modules.Subscription.Authorization;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.WebAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/usage")]
public class AdminUsageController : ControllerBase
{
    private readonly IRepository<UsageTracking, Guid> _usageRepository;
    private readonly Dispatcher _dispatcher;

    public AdminUsageController(IRepository<UsageTracking, Guid> usageRepository, Dispatcher dispatcher)
    {
        _usageRepository = usageRepository;
        _dispatcher = dispatcher;
    }

    [Authorize(Permissions.GetUsageTracking)]
    [HttpGet]
    public async Task<ActionResult<AdminUsageModel>> Get(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var resolvedTo = to ?? monthStart;
        var resolvedFrom = from ?? monthStart.AddMonths(-5);

        if (resolvedFrom > resolvedTo)
        {
            (resolvedFrom, resolvedTo) = (resolvedTo, resolvedFrom);
        }

        var usageEntries = await _usageRepository.ToListAsync(
            _usageRepository.GetQueryableSet()
                .AsNoTracking()
                .Where(x => x.PeriodStart >= resolvedFrom && x.PeriodStart <= resolvedTo));

        var users = await _dispatcher.DispatchAsync(new GetUsersQuery { AsNoTracking = true }, ct);
        var userLookup = users.ToDictionary(x => x.Id, x => ResolveUserName(x));

        var buckets = usageEntries
            .GroupBy(x => new DateOnly(x.PeriodStart.Year, x.PeriodStart.Month, 1))
            .ToDictionary(
                x => x.Key,
                x => new UsageTotalsModel
                {
                    ProjectCount = x.Sum(y => y.ProjectCount),
                    TestRunCount = x.Sum(y => y.TestRunCount),
                    LlmCallCount = x.Sum(y => y.LlmCallCount),
                    StorageUsedMB = x.Sum(y => y.StorageUsedMB),
                });

        var points = BuildPoints(resolvedFrom, resolvedTo, buckets);
        var totals = new UsageTotalsModel
        {
            ProjectCount = points.Sum(x => x.ProjectCount),
            TestRunCount = points.Sum(x => x.TestRunCount),
            LlmCallCount = points.Sum(x => x.LlmCallCount),
            StorageUsedMB = points.Sum(x => x.StorageUsedMB),
        };

        var targetMonth = new DateOnly(resolvedTo.Year, resolvedTo.Month, 1);
        var currentMonthEntries = usageEntries.Where(x => x.PeriodStart == targetMonth).ToList();

        var response = new AdminUsageModel
        {
            From = resolvedFrom.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            To = resolvedTo.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            Points = points,
            Totals = totals,
            TopUsers = new UsageTopUsersModel
            {
                Projects = BuildTopUsers(currentMonthEntries, userLookup, x => x.ProjectCount, 5),
                TestRuns = BuildTopUsers(currentMonthEntries, userLookup, x => x.TestRunCount, 5),
                LlmCalls = BuildTopUsers(currentMonthEntries, userLookup, x => x.LlmCallCount, 5),
                Storage = BuildTopUsers(currentMonthEntries, userLookup, x => x.StorageUsedMB, 5),
            },
        };

        return Ok(response);
    }

    private static List<UsagePointModel> BuildPoints(
        DateOnly from,
        DateOnly to,
        Dictionary<DateOnly, UsageTotalsModel> buckets)
    {
        var points = new List<UsagePointModel>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var end = new DateOnly(to.Year, to.Month, 1);

        while (cursor <= end)
        {
            buckets.TryGetValue(cursor, out var totals);
            totals ??= new UsageTotalsModel();

            points.Add(new UsagePointModel
            {
                Period = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                ProjectCount = totals.ProjectCount,
                TestRunCount = totals.TestRunCount,
                LlmCallCount = totals.LlmCallCount,
                StorageUsedMB = totals.StorageUsedMB,
            });

            cursor = cursor.AddMonths(1);
        }

        return points;
    }

    private static List<TopUserMetricModel> BuildTopUsers(
        List<UsageTracking> entries,
        Dictionary<Guid, string> userLookup,
        Func<UsageTracking, decimal> selector,
        int take)
    {
        return entries
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

    private static string ResolveUserName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
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
