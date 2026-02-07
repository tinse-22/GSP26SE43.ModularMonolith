using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Queries;

public class GetPlanQuery : IQuery<PlanModel>
{
    public Guid Id { get; set; }

    public bool ThrowNotFoundIfNull { get; set; }
}

public class GetPlanQueryHandler : IQueryHandler<GetPlanQuery, PlanModel>
{
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PlanLimit, Guid> _limitRepository;

    public GetPlanQueryHandler(
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PlanLimit, Guid> limitRepository)
    {
        _planRepository = planRepository;
        _limitRepository = limitRepository;
    }

    public async Task<PlanModel> HandleAsync(GetPlanQuery query, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == query.Id));

        if (query.ThrowNotFoundIfNull && plan == null)
        {
            throw new NotFoundException($"Plan {query.Id} not found.");
        }

        if (plan == null)
        {
            return null;
        }

        var limits = await _limitRepository.ToListAsync(
            _limitRepository.GetQueryableSet().Where(l => l.PlanId == plan.Id));

        return plan.ToModel(limits);
    }
}
