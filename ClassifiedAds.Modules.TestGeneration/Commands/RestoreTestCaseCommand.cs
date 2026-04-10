using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class RestoreTestCaseCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid TestCaseId { get; set; }
    public Guid CurrentUserId { get; set; }
    public TestCaseModel Result { get; set; }
}

public class RestoreTestCaseCommandHandler : ICommandHandler<RestoreTestCaseCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public RestoreTestCaseCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(RestoreTestCaseCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.TestCaseId != Guid.Empty, "TestCaseId là bắt buộc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");

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

        // 3) Load test case with related entities
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Include(x => x.Request)
                .Include(x => x.Expectation)
                .Include(x => x.Variables)
                .Include(x => x.Dependencies)
                .Where(x => x.Id == command.TestCaseId && x.TestSuiteId == command.TestSuiteId));

        if (testCase == null)
        {
            throw new NotFoundException($"Không tìm thấy test case với mã '{command.TestCaseId}'.");
        }

        ValidationException.Requires(
            testCase.IsDeleted,
            "Test case này chưa bị xóa, không thể khôi phục.");

        var now = DateTimeOffset.UtcNow;

        // 4) Determine next OrderIndex (after all active cases)
        var activeCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && !x.IsDeleted));

        var maxOrderIndex = activeCases.Count > 0
            ? activeCases.Max(x => x.OrderIndex)
            : -1;

        // 5) Restore test case
        testCase.IsDeleted = false;
        testCase.DeletedAt = null;
        testCase.DeletedById = null;
        testCase.IsEnabled = true;
        testCase.OrderIndex = maxOrderIndex + 1;
        testCase.UpdatedDateTime = now;
        testCase.LastModifiedById = command.CurrentUserId;
        await _testCaseRepository.UpdateAsync(testCase, cancellationToken);

        // 6) Create ChangeLog
        await _changeLogRepository.AddAsync(new TestCaseChangeLog
        {
            Id = Guid.NewGuid(),
            TestCaseId = command.TestCaseId,
            ChangedById = command.CurrentUserId,
            ChangeType = TestCaseChangeType.Restored,
            FieldName = null,
            OldValue = JsonSerializer.Serialize(new
            {
                IsDeleted = true,
            }, JsonOpts),
            NewValue = JsonSerializer.Serialize(new
            {
                testCase.Name,
                testCase.TestType,
                testCase.Priority,
                testCase.OrderIndex,
            }, JsonOpts),
            ChangeReason = "Khôi phục test case đã xóa.",
            VersionAfterChange = testCase.Version,
            CreatedDateTime = now,
        }, cancellationToken);

        // 7) Update suite version
        suite.Version += 1;
        suite.LastModifiedById = command.CurrentUserId;
        suite.UpdatedDateTime = now;
        suite.RowVersion = Guid.NewGuid().ToByteArray();
        await _suiteRepository.UpdateAsync(suite, cancellationToken);

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // 8) Return result
        command.Result = TestCaseModel.FromEntity(testCase);
    }
}
