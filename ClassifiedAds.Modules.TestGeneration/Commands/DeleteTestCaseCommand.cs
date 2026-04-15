using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class DeleteTestCaseCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid TestCaseId { get; set; }
    public Guid CurrentUserId { get; set; }
    public TestCaseModel Result { get; set; }
}

public class DeleteTestCaseCommandHandler : ICommandHandler<DeleteTestCaseCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public DeleteTestCaseCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(DeleteTestCaseCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (command.TestCaseId == Guid.Empty)
        {
            throw new ValidationException("TestCaseId là bắt buộc.");
        }

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        if (suite.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        if (suite.Status == TestSuiteStatus.Archived)
        {
            throw new ValidationException("Không thể xoá test case cho test suite đã archived.");
        }

        // 3) Load test case
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestCaseId && x.TestSuiteId == command.TestSuiteId));

        if (testCase == null)
        {
            throw new NotFoundException($"Không tìm thấy test case với mã '{command.TestCaseId}'.");
        }

        if (testCase.IsDeleted)
        {
            throw new ValidationException("Test case này đã bị xóa.");
        }

        var now = DateTimeOffset.UtcNow;

        // 4) Create ChangeLog before soft deletion
        await _changeLogRepository.AddAsync(new TestCaseChangeLog
        {
            Id = Guid.NewGuid(),
            TestCaseId = command.TestCaseId,
            ChangedById = command.CurrentUserId,
            ChangeType = TestCaseChangeType.Deleted,
            FieldName = null,
            OldValue = JsonSerializer.Serialize(new
            {
                testCase.Name,
                testCase.TestType,
                testCase.Priority,
                testCase.OrderIndex,
            }, JsonOpts),
            NewValue = null,
            ChangeReason = "Xoá test case thủ công.",
            VersionAfterChange = testCase.Version,
            CreatedDateTime = now,
        }, cancellationToken);

        // 5) Soft delete test case (preserve all related data)
        testCase.IsDeleted = true;
        testCase.DeletedAt = now;
        testCase.DeletedById = command.CurrentUserId;
        testCase.IsEnabled = false;
        testCase.UpdatedDateTime = now;
        await _testCaseRepository.UpdateAsync(testCase, cancellationToken);

        // 6) Recalculate OrderIndex for remaining active cases
        var remainingCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && !x.IsDeleted && x.Id != command.TestCaseId));

        var ordered = remainingCases.OrderBy(x => x.OrderIndex).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].OrderIndex != i)
            {
                ordered[i].OrderIndex = i;
                await _testCaseRepository.UpdateAsync(ordered[i], cancellationToken);
            }
        }

        // 7) Update suite version
        suite.Version += 1;
        suite.LastModifiedById = command.CurrentUserId;
        suite.UpdatedDateTime = now;
        suite.RowVersion = Guid.NewGuid().ToByteArray();
        await _suiteRepository.UpdateAsync(suite, cancellationToken);

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // 8) Return result for API response
        command.Result = TestCaseModel.FromEntity(testCase);
    }
}
