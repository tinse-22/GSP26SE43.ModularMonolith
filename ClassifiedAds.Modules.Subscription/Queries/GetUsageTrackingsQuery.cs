using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Queries;

public class GetUsageTrackingsQuery : IQuery<List<UsageTrackingModel>>
{
    public Guid UserId { get; set; }

    public DateOnly? PeriodStart { get; set; }

    public DateOnly? PeriodEnd { get; set; }
}

public class GetUsageTrackingsQueryHandler : IQueryHandler<GetUsageTrackingsQuery, List<UsageTrackingModel>>
{
    private readonly IRepository<UsageTracking, Guid> _usageTrackingRepository;

    public GetUsageTrackingsQueryHandler(IRepository<UsageTracking, Guid> usageTrackingRepository)
    {
        _usageTrackingRepository = usageTrackingRepository;
    }

    public async Task<List<UsageTrackingModel>> HandleAsync(
        GetUsageTrackingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var db = _usageTrackingRepository.GetQueryableSet()
            .Where(x => x.UserId == query.UserId);

        if (query.PeriodStart.HasValue)
        {
            db = db.Where(x => x.PeriodStart >= query.PeriodStart.Value);
        }

        if (query.PeriodEnd.HasValue)
        {
            db = db.Where(x => x.PeriodEnd <= query.PeriodEnd.Value);
        }

        var items = await _usageTrackingRepository.ToListAsync(
            db.OrderByDescending(x => x.PeriodStart));

        return items.Select(x => x.ToModel()).ToList();
    }
}
