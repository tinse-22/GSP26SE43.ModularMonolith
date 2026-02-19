using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Commands;

public class DeleteExecutionEnvironmentCommand : ICommand
{
    public Guid EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string RowVersion { get; set; }
}

public class DeleteExecutionEnvironmentCommandHandler : ICommandHandler<DeleteExecutionEnvironmentCommand>
{
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly ILogger<DeleteExecutionEnvironmentCommandHandler> _logger;

    public DeleteExecutionEnvironmentCommandHandler(
        IRepository<ExecutionEnvironment, Guid> envRepository,
        ILogger<DeleteExecutionEnvironmentCommandHandler> logger)
    {
        _envRepository = envRepository;
        _logger = logger;
    }

    public async Task HandleAsync(DeleteExecutionEnvironmentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EnvironmentId == Guid.Empty)
        {
            throw new ValidationException("EnvironmentId là bắt buộc.");
        }

        if (command.ProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId là bắt buộc.");
        }

        if (command.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId là bắt buộc.");
        }

        var env = await _envRepository.FirstOrDefaultAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.Id == command.EnvironmentId && x.ProjectId == command.ProjectId));

        if (env == null)
        {
            throw new NotFoundException($"Không tìm thấy execution environment với mã '{command.EnvironmentId}'.");
        }

        if (string.IsNullOrEmpty(command.RowVersion))
        {
            throw new ValidationException("RowVersion là bắt buộc khi xóa.");
        }

        try
        {
            _envRepository.SetRowVersion(env, Convert.FromBase64String(command.RowVersion));
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion không hợp lệ.");
        }

        try
        {
            _envRepository.Delete(env);
            await _envRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.", ex);
        }

        _logger.LogInformation(
            "Deleted execution environment. EnvironmentId={EnvironmentId}, ProjectId={ProjectId}, ActorUserId={ActorUserId}",
            command.EnvironmentId, command.ProjectId, command.CurrentUserId);
    }
}
