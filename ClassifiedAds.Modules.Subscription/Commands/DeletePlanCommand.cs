using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class DeletePlanCommand : ICommand
{
    public SubscriptionPlan Plan { get; set; }
}

public class DeletePlanCommandHandler : ICommandHandler<DeletePlanCommand>
{
    private readonly ICrudService<SubscriptionPlan> _planService;
    private readonly IRepository<UserSubscription, Guid> _userSubscriptionRepository;

    public DeletePlanCommandHandler(
        ICrudService<SubscriptionPlan> planService,
        IRepository<UserSubscription, Guid> userSubscriptionRepository)
    {
        _planService = planService;
        _userSubscriptionRepository = userSubscriptionRepository;
    }

    public async Task HandleAsync(DeletePlanCommand command, CancellationToken cancellationToken = default)
    {
        var plan = command.Plan;

        // Check for active subscribers
        var activeStatuses = new[]
        {
            SubscriptionStatus.Trial,
            SubscriptionStatus.Active,
            SubscriptionStatus.PastDue,
        };

        var activeSubscriberCount = await _userSubscriptionRepository.ToListAsync(
            _userSubscriptionRepository.GetQueryableSet()
                .Where(s => s.PlanId == plan.Id && activeStatuses.Contains(s.Status)));

        if (activeSubscriberCount.Count > 0)
        {
            throw new ValidationException(
                $"Cannot deactivate plan '{plan.DisplayName}' because it has {activeSubscriberCount.Count} active subscriber(s). Migrate subscribers to another plan first.");
        }

        // Soft delete: set IsActive to false
        plan.IsActive = false;

        // Use CrudService which triggers EntityUpdatedEvent (we treat soft-delete as update)
        // We dispatch EntityDeletedEvent manually via the controller to distinguish the action
        await _planService.AddOrUpdateAsync(plan, cancellationToken);
    }
}
