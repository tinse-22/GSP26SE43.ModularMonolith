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

public class GetSubscriptionHistoriesQuery : IQuery<List<SubscriptionHistoryModel>>
{
    public Guid SubscriptionId { get; set; }
}

public class GetSubscriptionHistoriesQueryHandler : IQueryHandler<GetSubscriptionHistoriesQuery, List<SubscriptionHistoryModel>>
{
    private readonly IRepository<SubscriptionHistory, Guid> _historyRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public GetSubscriptionHistoriesQueryHandler(
        IRepository<SubscriptionHistory, Guid> historyRepository,
        IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _historyRepository = historyRepository;
        _planRepository = planRepository;
    }

    public async Task<List<SubscriptionHistoryModel>> HandleAsync(
        GetSubscriptionHistoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var histories = await _historyRepository.ToListAsync(
            _historyRepository.GetQueryableSet()
                .Where(x => x.SubscriptionId == query.SubscriptionId)
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.CreatedDateTime));

        var planIds = histories
            .SelectMany(x => new Guid?[] { x.OldPlanId, x.NewPlanId })
            .Where(x => x.HasValue && x.Value != Guid.Empty)
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        var plans = planIds.Count == 0
            ? new List<SubscriptionPlan>()
            : await _planRepository.ToListAsync(
                _planRepository.GetQueryableSet().Where(x => planIds.Contains(x.Id)));

        var planLookup = plans.ToDictionary(x => x.Id, x => x);
        return histories.Select(x => x.ToModel(planLookup)).ToList();
    }
}
