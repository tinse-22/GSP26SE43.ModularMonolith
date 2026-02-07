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

public class GetPaymentTransactionsQuery : IQuery<List<PaymentTransactionModel>>
{
    public Guid? SubscriptionId { get; set; }

    public Guid? UserId { get; set; }

    public PaymentStatus? Status { get; set; }
}

public class GetPaymentTransactionsQueryHandler : IQueryHandler<GetPaymentTransactionsQuery, List<PaymentTransactionModel>>
{
    private readonly IRepository<PaymentTransaction, Guid> _paymentTransactionRepository;

    public GetPaymentTransactionsQueryHandler(IRepository<PaymentTransaction, Guid> paymentTransactionRepository)
    {
        _paymentTransactionRepository = paymentTransactionRepository;
    }

    public async Task<List<PaymentTransactionModel>> HandleAsync(
        GetPaymentTransactionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var db = _paymentTransactionRepository.GetQueryableSet().AsQueryable();

        if (query.SubscriptionId.HasValue && query.SubscriptionId.Value != Guid.Empty)
        {
            db = db.Where(x => x.SubscriptionId == query.SubscriptionId.Value);
        }

        if (query.UserId.HasValue && query.UserId.Value != Guid.Empty)
        {
            db = db.Where(x => x.UserId == query.UserId.Value);
        }

        if (query.Status.HasValue)
        {
            db = db.Where(x => x.Status == query.Status.Value);
        }

        var items = await _paymentTransactionRepository.ToListAsync(
            db.OrderByDescending(x => x.CreatedDateTime));

        return items.Select(x => x.ToModel()).ToList();
    }
}
