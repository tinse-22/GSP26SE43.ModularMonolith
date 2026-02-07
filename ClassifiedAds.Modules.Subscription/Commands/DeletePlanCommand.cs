using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class DeletePlanCommand : ICommand
{
    public Guid PlanId { get; set; }
}

public class DeletePlanCommandHandler : ICommandHandler<DeletePlanCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<UserSubscription, Guid> _userSubscriptionRepository;

    public DeletePlanCommandHandler(
        Dispatcher dispatcher,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<UserSubscription, Guid> userSubscriptionRepository)
    {
        _dispatcher = dispatcher;
        _planRepository = planRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
    }

    public async Task HandleAsync(DeletePlanCommand command, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(p => p.Id == command.PlanId));

        if (plan == null)
        {
            throw new NotFoundException($"Không tìm thấy gói cước với mã '{command.PlanId}'.");
        }

        var activeStatuses = new[]
        {
            SubscriptionStatus.Trial,
            SubscriptionStatus.Active,
            SubscriptionStatus.PastDue,
        };

        var activeSubscribers = await _userSubscriptionRepository.ToListAsync(
            _userSubscriptionRepository.GetQueryableSet()
                .Where(s => s.PlanId == plan.Id && activeStatuses.Contains(s.Status)));

        if (activeSubscribers.Count > 0)
        {
            throw new ValidationException(
                $"Không thể ngừng kích hoạt gói '{plan.DisplayName}' vì vẫn còn {activeSubscribers.Count} thuê bao đang hoạt động. Vui lòng chuyển thuê bao sang gói khác trước.");
        }

        if (!plan.IsActive)
        {
            return;
        }

        plan.IsActive = false;

        await _planRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _planRepository.UpdateAsync(plan, ct);
            await _planRepository.UnitOfWork.SaveChangesAsync(ct);
            await _dispatcher.DispatchAsync(new EntityDeletedEvent<SubscriptionPlan>(plan, DateTime.UtcNow), ct);
        }, cancellationToken: cancellationToken);
    }
}
