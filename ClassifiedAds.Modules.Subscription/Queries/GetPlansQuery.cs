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

public class GetPlansQuery : IQuery<List<PlanModel>>
{
    public bool? IsActive { get; set; }

    public string Search { get; set; }
}

public class GetPlansQueryHandler : IQueryHandler<GetPlansQuery, List<PlanModel>>
{
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PlanLimit, Guid> _limitRepository;

    public GetPlansQueryHandler(
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PlanLimit, Guid> limitRepository)
    {
        _planRepository = planRepository;
        _limitRepository = limitRepository;
    }

    public async Task<List<PlanModel>> HandleAsync(GetPlansQuery query, CancellationToken cancellationToken = default)
    {
        var plansQuery = _planRepository.GetQueryableSet().AsQueryable();

        if (query.IsActive.HasValue)
        {
            plansQuery = plansQuery.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            plansQuery = plansQuery.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.DisplayName.ToLower().Contains(search));
        }

        plansQuery = plansQuery.OrderBy(p => p.SortOrder).ThenBy(p => p.Name);

        var plans = await _planRepository.ToListAsync(plansQuery);

        var planIds = plans.Select(p => p.Id).ToList();
        var allLimits = await _limitRepository.ToListAsync(
            _limitRepository.GetQueryableSet().Where(l => planIds.Contains(l.PlanId)));

        var limitsLookup = allLimits.ToLookup(l => l.PlanId);

        return plans.ToModels(limitsLookup).ToList();
    }
}
