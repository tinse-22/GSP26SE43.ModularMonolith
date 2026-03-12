using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ToggleTestCaseCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid TestCaseId { get; set; }
    public Guid CurrentUserId { get; set; }
    public bool IsEnabled { get; set; }
}

public class ToggleTestCaseCommandHandler : ICommandHandler<ToggleTestCaseCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public ToggleTestCaseCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(ToggleTestCaseCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");

        if (command.TestCaseId == Guid.Empty)
            throw new ValidationException("TestCaseId là bắt buộc.");

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");

        if (suite.CreatedById != command.CurrentUserId)
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");

        // 3) Load test case
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestCaseId && x.TestSuiteId == command.TestSuiteId));

        if (testCase == null)
            throw new NotFoundException($"Không tìm thấy test case với mã '{command.TestCaseId}'.");

        var now = DateTimeOffset.UtcNow;
        var oldValue = testCase.IsEnabled;

        // 4) Toggle IsEnabled
        testCase.IsEnabled = command.IsEnabled;
        testCase.LastModifiedById = command.CurrentUserId;
        testCase.UpdatedDateTime = now;

        await _testCaseRepository.UpdateAsync(testCase, cancellationToken);

        // 5) Create ChangeLog
        await _changeLogRepository.AddAsync(new TestCaseChangeLog
        {
            Id = Guid.NewGuid(),
            TestCaseId = command.TestCaseId,
            ChangedById = command.CurrentUserId,
            ChangeType = TestCaseChangeType.EnabledStatusChanged,
            FieldName = "IsEnabled",
            OldValue = oldValue.ToString(),
            NewValue = command.IsEnabled.ToString(),
            ChangeReason = command.IsEnabled ? "Bật test case." : "Tắt test case.",
            VersionAfterChange = testCase.Version,
            CreatedDateTime = now,
        }, cancellationToken);

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
