using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ArchiveTestSuiteScopeCommand : ICommand
{
    public Guid SuiteId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string RowVersion { get; set; }
}

public class ArchiveTestSuiteScopeCommandHandler : ICommandHandler<ArchiveTestSuiteScopeCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly ILogger<ArchiveTestSuiteScopeCommandHandler> _logger;

    public ArchiveTestSuiteScopeCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        ILogger<ArchiveTestSuiteScopeCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ArchiveTestSuiteScopeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.SuiteId == Guid.Empty)
        {
            throw new ValidationException("SuiteId là bắt buộc.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuiteId && x.ProjectId == command.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.SuiteId}'.");
        }

        if (suite.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền xóa test suite này.");
        }

        if (string.IsNullOrEmpty(command.RowVersion))
        {
            throw new ValidationException("RowVersion là bắt buộc khi xóa.");
        }

        try
        {
            _suiteRepository.SetRowVersion(suite, Convert.FromBase64String(command.RowVersion));
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion không hợp lệ.");
        }

        suite.Status = TestSuiteStatus.Archived;
        suite.LastModifiedById = command.CurrentUserId;

        try
        {
            await _suiteRepository.UpdateAsync(suite, cancellationToken);
            await _suiteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.", ex);
        }

        _logger.LogInformation(
            "Archived test suite. SuiteId={SuiteId}, ProjectId={ProjectId}, ActorUserId={ActorUserId}",
            command.SuiteId, command.ProjectId, command.CurrentUserId);
    }
}
