using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Commands;

public class UpsertUsageTrackingCommand : ICommand
{
    public Guid UserId { get; set; }

    public UpsertUsageTrackingModel Model { get; set; }

    public Guid SavedUsageTrackingId { get; set; }
}

public class UpsertUsageTrackingCommandHandler : ICommandHandler<UpsertUsageTrackingCommand>
{
    private readonly IRepository<UsageTracking, Guid> _usageTrackingRepository;

    public UpsertUsageTrackingCommandHandler(IRepository<UsageTracking, Guid> usageTrackingRepository)
    {
        _usageTrackingRepository = usageTrackingRepository;
    }

    public async Task HandleAsync(UpsertUsageTrackingCommand command, CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            throw new ValidationException("Mã người dùng là bắt buộc.");
        }

        if (command.Model == null)
        {
            throw new ValidationException("Thông tin theo dõi sử dụng là bắt buộc.");
        }

        if (command.Model.PeriodStart > command.Model.PeriodEnd)
        {
            throw new ValidationException("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        }

        ValidateUsageValues(command.Model);

        var tracking = await _usageTrackingRepository.FirstOrDefaultAsync(
            _usageTrackingRepository.GetQueryableSet()
                .Where(x => x.UserId == command.UserId
                    && x.PeriodStart == command.Model.PeriodStart
                    && x.PeriodEnd == command.Model.PeriodEnd));

        var isCreate = tracking == null;
        if (isCreate)
        {
            tracking = new UsageTracking
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                PeriodStart = command.Model.PeriodStart,
                PeriodEnd = command.Model.PeriodEnd,
            };
        }

        if (command.Model.ReplaceValues || isCreate)
        {
            tracking.ProjectCount = command.Model.ProjectCount;
            tracking.EndpointCount = command.Model.EndpointCount;
            tracking.TestSuiteCount = command.Model.TestSuiteCount;
            tracking.TestCaseCount = command.Model.TestCaseCount;
            tracking.TestRunCount = command.Model.TestRunCount;
            tracking.LlmCallCount = command.Model.LlmCallCount;
            tracking.StorageUsedMB = command.Model.StorageUsedMB;
        }
        else
        {
            tracking.ProjectCount += command.Model.ProjectCount;
            tracking.EndpointCount += command.Model.EndpointCount;
            tracking.TestSuiteCount += command.Model.TestSuiteCount;
            tracking.TestCaseCount += command.Model.TestCaseCount;
            tracking.TestRunCount += command.Model.TestRunCount;
            tracking.LlmCallCount += command.Model.LlmCallCount;
            tracking.StorageUsedMB += command.Model.StorageUsedMB;
        }

        if (isCreate)
        {
            await _usageTrackingRepository.AddAsync(tracking, cancellationToken);
        }
        else
        {
            await _usageTrackingRepository.UpdateAsync(tracking, cancellationToken);
        }

        await _usageTrackingRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.SavedUsageTrackingId = tracking.Id;
    }

    private static void ValidateUsageValues(UpsertUsageTrackingModel model)
    {
        if (model.ProjectCount < 0
            || model.EndpointCount < 0
            || model.TestSuiteCount < 0
            || model.TestCaseCount < 0
            || model.TestRunCount < 0
            || model.LlmCallCount < 0
            || model.StorageUsedMB < 0)
        {
            throw new ValidationException("Giá trị sử dụng không được âm.");
        }
    }
}
