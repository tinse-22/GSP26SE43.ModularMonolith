using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Queries;

public class GetRevenueSeriesQuery : IQuery<RevenueSeriesModel>
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public string GroupBy { get; set; }

    public string Currency { get; set; }

    public PaymentStatus? Status { get; set; }
}

public class GetRevenueSeriesQueryHandler : IQueryHandler<GetRevenueSeriesQuery, RevenueSeriesModel>
{
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;

    public GetRevenueSeriesQueryHandler(IRepository<PaymentTransaction, Guid> paymentTransactionRepository)
    {
        _paymentTransactionRepository = paymentTransactionRepository;
    }

    public async Task<RevenueSeriesModel> HandleAsync(
        GetRevenueSeriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var groupBy = NormalizeGroupBy(query.GroupBy);
        var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? ResolveDefaultFrom(groupBy, to);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        var status = query.Status ?? PaymentStatus.Succeeded;
        var currency = string.IsNullOrWhiteSpace(query.Currency)
            ? null
            : query.Currency.Trim().ToUpperInvariant();

        var rangeStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var rangeEnd = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        var db = _paymentTransactionRepository
            .GetQueryableSet()
            .AsNoTracking()
            .Where(x => x.CreatedDateTime >= rangeStart && x.CreatedDateTime <= rangeEnd)
            .Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(currency))
        {
            db = db.Where(x => x.Currency == currency);
        }

        var transactions = await _paymentTransactionRepository.ToListAsync(db);

        var resolvedCurrency = ResolveCurrency(currency, transactions);
        var buckets = BuildBuckets(groupBy, transactions);
        var points = BuildPoints(groupBy, from, to, buckets);

        return new RevenueSeriesModel
        {
            Currency = resolvedCurrency,
            GroupBy = groupBy,
            From = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            To = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TotalAmount = buckets.Values.Sum(x => x.Amount),
            TotalTransactions = buckets.Values.Sum(x => x.Count),
            Points = points,
        };
    }

    private static string NormalizeGroupBy(string groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return "day";
        }

        var normalized = groupBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "month" => "month",
            "year" => "year",
            _ => "day",
        };
    }

    private static DateOnly ResolveDefaultFrom(string groupBy, DateOnly to)
    {
        return groupBy switch
        {
            "month" => to.AddMonths(-11),
            "year" => to.AddYears(-4),
            _ => to.AddDays(-29),
        };
    }

    private static string ResolveCurrency(string requestedCurrency, List<PaymentTransaction> transactions)
    {
        if (!string.IsNullOrWhiteSpace(requestedCurrency))
        {
            return requestedCurrency;
        }

        var distinct = transactions
            .Select(x => x.Currency)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (distinct.Count == 1)
        {
            return distinct[0];
        }

        return distinct.Count == 0 ? "VND" : "MIXED";
    }

    private static Dictionary<DateOnly, (decimal Amount, int Count)> BuildBuckets(
        string groupBy,
        List<PaymentTransaction> transactions)
    {
        var buckets = new Dictionary<DateOnly, (decimal Amount, int Count)>();

        foreach (var txn in transactions)
        {
            var date = DateOnly.FromDateTime(txn.CreatedDateTime.UtcDateTime);
            var key = groupBy switch
            {
                "month" => new DateOnly(date.Year, date.Month, 1),
                "year" => new DateOnly(date.Year, 1, 1),
                _ => date,
            };

            if (!buckets.TryGetValue(key, out var value))
            {
                value = (0, 0);
            }

            value.Amount += txn.Amount;
            value.Count += 1;
            buckets[key] = value;
        }

        return buckets;
    }

    private static List<RevenuePointModel> BuildPoints(
        string groupBy,
        DateOnly from,
        DateOnly to,
        Dictionary<DateOnly, (decimal Amount, int Count)> buckets)
    {
        var points = new List<RevenuePointModel>();

        if (groupBy == "month")
        {
            for (var cursor = new DateOnly(from.Year, from.Month, 1);
                 cursor <= new DateOnly(to.Year, to.Month, 1);
                 cursor = cursor.AddMonths(1))
            {
                buckets.TryGetValue(cursor, out var value);
                points.Add(new RevenuePointModel
                {
                    Period = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    Amount = value.Amount,
                    TransactionCount = value.Count,
                });
            }

            return points;
        }

        if (groupBy == "year")
        {
            for (var year = from.Year; year <= to.Year; year++)
            {
                var cursor = new DateOnly(year, 1, 1);
                buckets.TryGetValue(cursor, out var value);
                points.Add(new RevenuePointModel
                {
                    Period = year.ToString(CultureInfo.InvariantCulture),
                    Amount = value.Amount,
                    TransactionCount = value.Count,
                });
            }

            return points;
        }

        for (var cursor = from; cursor <= to; cursor = cursor.AddDays(1))
        {
            buckets.TryGetValue(cursor, out var value);
            points.Add(new RevenuePointModel
            {
                Period = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Amount = value.Amount,
                TransactionCount = value.Count,
            });
        }

        return points;
    }
}
