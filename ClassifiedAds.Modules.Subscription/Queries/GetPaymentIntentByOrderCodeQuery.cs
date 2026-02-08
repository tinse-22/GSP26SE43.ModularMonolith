using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Queries;

public class GetPaymentIntentByOrderCodeQuery : IQuery<PaymentIntent>
{
    public long OrderCode { get; set; }
}

public class GetPaymentIntentByOrderCodeQueryHandler : IQueryHandler<GetPaymentIntentByOrderCodeQuery, PaymentIntent>
{
    private readonly IRepository<PaymentIntent, Guid> _repository;

    public GetPaymentIntentByOrderCodeQueryHandler(IRepository<PaymentIntent, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PaymentIntent> HandleAsync(GetPaymentIntentByOrderCodeQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.FirstOrDefaultAsync(
            _repository.GetQueryableSet().Where(x => x.OrderCode == query.OrderCode));
    }
}