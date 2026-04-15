using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class BulkRestoreTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public List<Guid> TestCaseIds { get; set; }
    public BulkOperationResultModel Result { get; set; }
}

public class BulkRestoreTestCasesCommandHandler : ICommandHandler<BulkRestoreTestCasesCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public BulkRestoreTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(BulkRestoreTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");
        ValidationException.Requires(
            command.TestCaseIds != null && command.TestCaseIds.Count > 0,
            "TestCaseIds là bắt buộc và phải có ít nhất 1 phần tử.");

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Bạn không có quyền thao tác test suite này.");

        ValidationException.Requires(
            suite.Status != TestSuiteStatus.Archived,
            "Không thể khôi phục test case cho test suite đã archived.");

        // 3) Load test cases by IDs
        var distinctIds = command.TestCaseIds.Distinct().ToList();
        var testCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && distinctIds.Contains(x.Id)));

        // 4) Determine next OrderIndex
        var activeCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && !x.IsDeleted));

        var maxOrderIndex = activeCases.Count > 0
            ? activeCases.Max(x => x.OrderIndex)
            : -1;

        var now = DateTimeOffset.UtcNow;
        var processedIds = new List<Guid>();
        var skippedIds = new List<Guid>();
        var nextOrder = maxOrderIndex + 1;

        foreach (var tc in testCases)
        {
            if (!tc.IsDeleted)
            {
                skippedIds.Add(tc.Id);
                continue;
            }

            // Restore
            tc.IsDeleted = false;
            tc.DeletedAt = null;
            tc.DeletedById = null;
            tc.IsEnabled = true;
            tc.OrderIndex = nextOrder++;
            tc.UpdatedDateTime = now;
            tc.LastModifiedById = command.CurrentUserId;
            await _testCaseRepository.UpdateAsync(tc, cancellationToken);

            // ChangeLog
            await _changeLogRepository.AddAsync(new TestCaseChangeLog
            {
                Id = Guid.NewGuid(),
                TestCaseId = tc.Id,
                ChangedById = command.CurrentUserId,
                ChangeType = TestCaseChangeType.Restored,
                FieldName = null,
                OldValue = JsonSerializer.Serialize(new
                {
                    IsDeleted = true,
                }, JsonOpts),
                NewValue = JsonSerializer.Serialize(new
                {
                    tc.Name,
                    tc.TestType,
                    tc.Priority,
                    tc.OrderIndex,
                }, JsonOpts),
                ChangeReason = "Khôi phục test case hàng loạt.",
                VersionAfterChange = tc.Version,
                CreatedDateTime = now,
            }, cancellationToken);

            processedIds.Add(tc.Id);
        }

        // IDs not found
        var notFoundIds = distinctIds.Except(testCases.Select(x => x.Id)).ToList();
        skippedIds.AddRange(notFoundIds);

        // 5) Update suite version
        if (processedIds.Count > 0)
        {
            suite.Version += 1;
            suite.LastModifiedById = command.CurrentUserId;
            suite.UpdatedDateTime = now;
            suite.RowVersion = Guid.NewGuid().ToByteArray();
            await _suiteRepository.UpdateAsync(suite, cancellationToken);
        }

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = new BulkOperationResultModel
        {
            TestSuiteId = command.TestSuiteId,
            Operation = "Restore",
            EntityType = "TestCase",
            RequestedCount = distinctIds.Count,
            ProcessedCount = processedIds.Count,
            SkippedCount = skippedIds.Count,
            ProcessedIds = processedIds,
            SkippedIds = skippedIds,
            SkipReason = skippedIds.Count > 0 ? "Chưa bị xóa hoặc không tìm thấy." : null,
            OperatedAt = now,
        };
    }
}
