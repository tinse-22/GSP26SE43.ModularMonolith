using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Queries;

public class GetSubscriptionQuery : IQuery<SubscriptionModel>
{
    public Guid Id { get; set; }

    public bool ThrowNotFoundIfNull { get; set; }
}

public class GetSubscriptionQueryHandler : IQueryHandler<GetSubscriptionQuery, SubscriptionModel>
{
    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public GetSubscriptionQueryHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<SubscriptionModel> HandleAsync(GetSubscriptionQuery query, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet().Where(x => x.Id == query.Id));

        if (subscription == null && query.ThrowNotFoundIfNull)
        {
            throw new NotFoundException($"Subscription '{query.Id}' was not found.");
        }

        if (subscription == null)
        {
            return null;
        }

        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == subscription.PlanId));

        return subscription.ToModel(plan);
    }
}
