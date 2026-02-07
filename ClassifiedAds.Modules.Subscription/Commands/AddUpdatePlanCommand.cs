using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class AddUpdatePlanCommand : ICommand
{
    public SubscriptionPlan Plan { get; set; }

    public List<PlanLimit> Limits { get; set; } = [];
}

public class AddUpdatePlanCommandHandler : ICommandHandler<AddUpdatePlanCommand>
{
    private readonly ICrudService<SubscriptionPlan> _planService;
    private readonly IRepository<PlanLimit, Guid> _limitRepository;

    public AddUpdatePlanCommandHandler(
        ICrudService<SubscriptionPlan> planService,
        IRepository<PlanLimit, Guid> limitRepository)
    {
        _planService = planService;
        _limitRepository = limitRepository;
    }

    public async Task HandleAsync(AddUpdatePlanCommand command, CancellationToken cancellationToken = default)
    {
        var plan = command.Plan;

        // Validate limits
        ValidateLimits(command.Limits);

        // Save plan (triggers domain events via CrudService)
        await _planService.AddOrUpdateAsync(plan, cancellationToken);

        // Replace limits: delete old, add new
        var oldLimits = await _limitRepository.ToListAsync(
            _limitRepository.GetQueryableSet().Where(l => l.PlanId == plan.Id));

        foreach (var oldLimit in oldLimits)
        {
            _limitRepository.Delete(oldLimit);
        }

        foreach (var limit in command.Limits)
        {
            limit.PlanId = plan.Id;
            await _limitRepository.AddAsync(limit, cancellationToken);
        }

        await _limitRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateLimits(List<PlanLimit> limits)
    {
        if (limits == null || limits.Count == 0)
        {
            return;
        }

        // Check for duplicate LimitTypes
        var duplicates = limits
            .GroupBy(l => l.LimitType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new ValidationException(
                $"Duplicate LimitType(s) found: {string.Join(", ", duplicates)}. Each LimitType can appear at most once per plan.");
        }

        // Validate limit values
        foreach (var limit in limits)
        {
            if (!limit.IsUnlimited && (!limit.LimitValue.HasValue || limit.LimitValue.Value <= 0))
            {
                throw new ValidationException(
                    $"LimitValue must be greater than 0 for LimitType '{limit.LimitType}' when IsUnlimited is false.");
            }

            if (limit.IsUnlimited)
            {
                limit.LimitValue = null;
            }
        }
    }
}
