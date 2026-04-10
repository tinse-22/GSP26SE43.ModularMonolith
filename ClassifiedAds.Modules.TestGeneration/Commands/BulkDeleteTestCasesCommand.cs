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

public class BulkDeleteTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public List<Guid> TestCaseIds { get; set; }
    public BulkOperationResultModel Result { get; set; }
}

public class BulkDeleteTestCasesCommandHandler : ICommandHandler<BulkDeleteTestCasesCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public BulkDeleteTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(BulkDeleteTestCasesCommand command, CancellationToken cancellationToken = default)
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
            "Không thể xoá test case cho test suite đã archived.");

        // 3) Load test cases by IDs
        var distinctIds = command.TestCaseIds.Distinct().ToList();
        var testCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && distinctIds.Contains(x.Id)));

        var now = DateTimeOffset.UtcNow;
        var processedIds = new List<Guid>();
        var skippedIds = new List<Guid>();

        foreach (var tc in testCases)
        {
            if (tc.IsDeleted)
            {
                skippedIds.Add(tc.Id);
                continue;
            }

            // Soft delete
            tc.IsDeleted = true;
            tc.DeletedAt = now;
            tc.DeletedById = command.CurrentUserId;
            tc.IsEnabled = false;
            tc.UpdatedDateTime = now;
            await _testCaseRepository.UpdateAsync(tc, cancellationToken);

            // ChangeLog
            await _changeLogRepository.AddAsync(new TestCaseChangeLog
            {
                Id = Guid.NewGuid(),
                TestCaseId = tc.Id,
                ChangedById = command.CurrentUserId,
                ChangeType = TestCaseChangeType.Deleted,
                FieldName = null,
                OldValue = JsonSerializer.Serialize(new
                {
                    tc.Name,
                    tc.TestType,
                    tc.Priority,
                    tc.OrderIndex,
                }, JsonOpts),
                NewValue = null,
                ChangeReason = "Xoá test case hàng loạt.",
                VersionAfterChange = tc.Version,
                CreatedDateTime = now,
            }, cancellationToken);

            processedIds.Add(tc.Id);
        }

        // IDs not found in DB
        var notFoundIds = distinctIds.Except(testCases.Select(x => x.Id)).ToList();
        skippedIds.AddRange(notFoundIds);

        // 4) Recalculate OrderIndex for remaining active cases
        if (processedIds.Count > 0)
        {
            var remainingCases = await _testCaseRepository.ToListAsync(
                _testCaseRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId && !x.IsDeleted));

            var ordered = remainingCases.OrderBy(x => x.OrderIndex).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].OrderIndex != i)
                {
                    ordered[i].OrderIndex = i;
                    await _testCaseRepository.UpdateAsync(ordered[i], cancellationToken);
                }
            }

            // 5) Update suite version
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
            Operation = "Delete",
            EntityType = "TestCase",
            RequestedCount = distinctIds.Count,
            ProcessedCount = processedIds.Count,
            SkippedCount = skippedIds.Count,
            ProcessedIds = processedIds,
            SkippedIds = skippedIds,
            SkipReason = skippedIds.Count > 0 ? "Đã ở trạng thái xóa hoặc không tìm thấy." : null,
            OperatedAt = now,
        };
    }
}
