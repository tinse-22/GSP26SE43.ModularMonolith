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

public class GetCurrentSubscriptionByUserQuery : IQuery<SubscriptionModel>
{
    public Guid UserId { get; set; }

    public bool ThrowNotFoundIfNull { get; set; }
}

public class GetCurrentSubscriptionByUserQueryHandler : IQueryHandler<GetCurrentSubscriptionByUserQuery, SubscriptionModel>
{
    private static readonly SubscriptionStatus[] CurrentStatuses =
    {
        SubscriptionStatus.Trial,
        SubscriptionStatus.Active,
        SubscriptionStatus.PastDue,
    };

    private readonly IRepository<UserSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public GetCurrentSubscriptionByUserQueryHandler(
        IRepository<UserSubscription, Guid> subscriptionRepository,
        IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<SubscriptionModel> HandleAsync(GetCurrentSubscriptionByUserQuery query, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.FirstOrDefaultAsync(
            _subscriptionRepository.GetQueryableSet()
                .Where(x => x.UserId == query.UserId && CurrentStatuses.Contains(x.Status))
                .OrderByDescending(x => x.CreatedDateTime));

        if (subscription == null && query.ThrowNotFoundIfNull)
        {
            throw new NotFoundException($"Không tìm thấy đăng ký đang hoạt động cho người dùng '{query.UserId}'.");
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
