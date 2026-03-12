using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ReorderTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public List<Guid> TestCaseIds { get; set; } = new();
}

public class ReorderTestCasesCommandHandler : ICommandHandler<ReorderTestCasesCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;

    public ReorderTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _changeLogRepository = changeLogRepository;
    }

    public async Task HandleAsync(ReorderTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");

        if (command.TestCaseIds == null || command.TestCaseIds.Count == 0)
            throw new ValidationException("Danh sách TestCaseIds là bắt buộc.");

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");

        if (suite.CreatedById != command.CurrentUserId)
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");

        // 3) Load all test cases for suite
        var testCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId));

        var testCaseMap = testCases.ToDictionary(x => x.Id);

        // 4) Validate all IDs belong to suite
        var invalidIds = command.TestCaseIds.Where(id => !testCaseMap.ContainsKey(id)).ToList();
        if (invalidIds.Count > 0)
            throw new ValidationException(
                $"Các test case không thuộc test suite này: {string.Join(", ", invalidIds)}.");

        var now = DateTimeOffset.UtcNow;

        // 5) Set CustomOrderIndex and IsOrderCustomized
        for (int i = 0; i < command.TestCaseIds.Count; i++)
        {
            var id = command.TestCaseIds[i];
            var testCase = testCaseMap[id];
            testCase.CustomOrderIndex = i;
            testCase.IsOrderCustomized = true;
            testCase.LastModifiedById = command.CurrentUserId;
            testCase.UpdatedDateTime = now;

            await _testCaseRepository.UpdateAsync(testCase, cancellationToken);

            // Create ChangeLog for each reordered case
            await _changeLogRepository.AddAsync(new TestCaseChangeLog
            {
                Id = Guid.NewGuid(),
                TestCaseId = id,
                ChangedById = command.CurrentUserId,
                ChangeType = TestCaseChangeType.UserCustomizedOrder,
                FieldName = "CustomOrderIndex",
                OldValue = null,
                NewValue = i.ToString(),
                ChangeReason = "Sắp xếp lại thứ tự test case thủ công.",
                VersionAfterChange = testCase.Version,
                CreatedDateTime = now,
            }, cancellationToken);
        }

        await _testCaseRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
