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

public class GetPaymentIntentQuery : IQuery<PaymentIntentModel>
{
    public Guid IntentId { get; set; }

    public Guid UserId { get; set; }

    public bool ThrowNotFoundIfNull { get; set; }
}

public class GetPaymentIntentQueryHandler : IQueryHandler<GetPaymentIntentQuery, PaymentIntentModel>
{
    private readonly IRepository<PaymentIntent, Guid> _paymentIntentRepository;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public GetPaymentIntentQueryHandler(
        IRepository<PaymentIntent, Guid> paymentIntentRepository,
        IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _paymentIntentRepository = paymentIntentRepository;
        _planRepository = planRepository;
    }

    public async Task<PaymentIntentModel> HandleAsync(GetPaymentIntentQuery query, CancellationToken cancellationToken = default)
    {
        var entity = await _paymentIntentRepository.FirstOrDefaultAsync(
            _paymentIntentRepository.GetQueryableSet()
                .Where(x => x.Id == query.IntentId && x.UserId == query.UserId));

        if (entity == null)
        {
            if (query.ThrowNotFoundIfNull)
            {
                throw new NotFoundException($"Không tìm thấy yêu cầu thanh toán với mã '{query.IntentId}'.");
            }

            return null;
        }

        entity.Plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == entity.PlanId));

        return entity.ToModel();
    }
}