using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class AddUpdatePlanCommand : ICommand
{
    public Guid? PlanId { get; set; }

    public CreateUpdatePlanModel Model { get; set; }

    public Guid SavedPlanId { get; set; }
}

public class AddUpdatePlanCommandHandler : ICommandHandler<AddUpdatePlanCommand>
{
    private readonly ICrudService<SubscriptionPlan> _planService;
    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;
    private readonly IRepository<PlanLimit, Guid> _limitRepository;

    public AddUpdatePlanCommandHandler(
        ICrudService<SubscriptionPlan> planService,
        IRepository<SubscriptionPlan, Guid> planRepository,
        IRepository<PlanLimit, Guid> limitRepository)
    {
        _planService = planService;
        _planRepository = planRepository;
        _limitRepository = limitRepository;
    }

    public async Task HandleAsync(AddUpdatePlanCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Model == null)
        {
            throw new ValidationException("Dữ liệu gói cước không hợp lệ.");
        }

        var isCreate = !command.PlanId.HasValue || command.PlanId == Guid.Empty;
        var plan = isCreate
            ? command.Model.ToEntity()
            : await GetExistingPlanAsync(command.PlanId.Value, cancellationToken);

        if (!isCreate)
        {
            ApplyModel(plan, command.Model);
        }

        var limits = command.Model.ToLimitEntities(plan.Id);
        ValidateLimits(limits);

        try
        {
            await _limitRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await EnsureNameUniquenessAsync(plan.Name, isCreate ? null : plan.Id, ct);
                await _planService.AddOrUpdateAsync(plan, ct);
                await ReplaceLimitsAsync(plan.Id, limits, ct);
            }, cancellationToken: cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicatePlanNameException(ex))
        {
            throw new ValidationException($"Tên gói cước '{command.Model.Name?.Trim()}' đã tồn tại.");
        }

        command.SavedPlanId = plan.Id;
    }

    private async Task<SubscriptionPlan> GetExistingPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.FirstOrDefaultAsync(
            _planRepository.GetQueryableSet().Where(x => x.Id == planId));

        if (plan == null)
        {
            throw new NotFoundException($"Không tìm thấy gói cước với mã '{planId}'.");
        }

        return plan;
    }

    private static void ApplyModel(SubscriptionPlan plan, CreateUpdatePlanModel model)
    {
        plan.Name = model.Name?.Trim();
        plan.DisplayName = model.DisplayName?.Trim();
        plan.Description = model.Description?.Trim();
        plan.PriceMonthly = model.PriceMonthly;
        plan.PriceYearly = model.PriceYearly;
        plan.Currency = model.Currency?.Trim().ToUpperInvariant() ?? "USD";
        plan.IsActive = model.IsActive;
        plan.SortOrder = model.SortOrder;
    }

    private async Task EnsureNameUniquenessAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException("Tên gói cước là bắt buộc.");
        }

        var query = _planRepository.GetQueryableSet()
            .Where(p => p.Name.ToLower() == normalizedName);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        var existing = await _planRepository.FirstOrDefaultAsync(query);

        if (existing != null)
        {
            throw new ValidationException($"Tên gói cước '{name.Trim()}' đã tồn tại.");
        }
    }

    private async Task ReplaceLimitsAsync(Guid planId, List<PlanLimit> limits, CancellationToken cancellationToken)
    {
        var oldLimits = await _limitRepository.ToListAsync(
            _limitRepository.GetQueryableSet().Where(l => l.PlanId == planId));

        foreach (var oldLimit in oldLimits)
        {
            _limitRepository.Delete(oldLimit);
        }

        foreach (var limit in limits)
        {
            limit.PlanId = planId;
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
                $"Loại giới hạn bị trùng: {string.Join(", ", duplicates)}. Mỗi loại chỉ được khai báo một lần trong một gói.");
        }

        foreach (var limit in limits)
        {
            if (!limit.IsUnlimited && (!limit.LimitValue.HasValue || limit.LimitValue.Value <= 0))
            {
                throw new ValidationException(
                    $"Giá trị giới hạn phải lớn hơn 0 cho loại '{limit.LimitType}' khi không chọn không giới hạn.");
            }

            if (limit.IsUnlimited)
            {
                limit.LimitValue = null;
            }
        }
    }

    private static bool IsDuplicatePlanNameException(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pgEx)
        {
            return false;
        }

        return pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(pgEx.ConstraintName, "IX_SubscriptionPlans_Name", StringComparison.OrdinalIgnoreCase);
    }
}
