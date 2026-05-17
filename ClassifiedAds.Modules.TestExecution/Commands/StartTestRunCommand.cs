using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Commands;

public class StartTestRunCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public bool StrictValidation { get; set; }

    /// <summary>
    /// Validation profile for this run. When set, takes precedence over <see cref="StrictValidation"/>.
    /// Defaults to <see cref="ValidationProfile.Default"/> which preserves existing adaptive behaviour.
    /// Use <see cref="ValidationProfile.SrsStrict"/> for SRS-driven test suites.
    /// </summary>
    public ValidationProfile ValidationProfile { get; set; } = ValidationProfile.Default;

    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// When false the run will be recorded as ephemeral (not shown in the main TestRuns listing).
    /// Defaults to true.
    /// </summary>
    public bool RecordRun { get; set; } = true;

    /// <summary>
    /// Enables the retry mechanism for failed test cases whose only failures are expectation
    /// mismatches.  When false, no retries are attempted regardless of MaxRetryAttempts.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    public bool RerunSkippedCases { get; set; } = true;

    public TestRunRetryPolicyModel RetryPolicy { get; set; }

    public IReadOnlyList<Guid> SelectedTestCaseIds { get; set; }

    public TestRunResultModel Result { get; set; }
}

public class StartTestRunCommandHandler : ICommandHandler<StartTestRunCommand>
{
    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly ISubscriptionLimitGatewayService _limitService;
    private readonly ITestExecutionOrchestrator _orchestrator;
    private readonly ILogger<StartTestRunCommandHandler> _logger;

    public StartTestRunCommandHandler(
        IRepository<TestRun, Guid> runRepository,
        IRepository<ExecutionEnvironment, Guid> envRepository,
        ITestExecutionReadGatewayService gatewayService,
        ISubscriptionLimitGatewayService limitService,
        ITestExecutionOrchestrator orchestrator,
        ILogger<StartTestRunCommandHandler> logger)
    {
        _runRepository = runRepository;
        _envRepository = envRepository;
        _gatewayService = gatewayService;
        _limitService = limitService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(StartTestRunCommand command, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();

        // 1. Validate input
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        // Normalize selectedTestCaseIds
        var selectedIds = command.SelectedTestCaseIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new List<Guid>();

        // 2. Suite access context
        var validateSw = Stopwatch.StartNew();
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(command.TestSuiteId, cancellationToken);

        // 3. Validate ownership
        if (suiteContext.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        // 4. Validate status
        var isManualSuite = string.Equals(
            suiteContext.GenerationType,
            "Manual",
            StringComparison.OrdinalIgnoreCase);

        if (!isManualSuite && suiteContext.Status != "Ready")
        {
            throw new ValidationException("Test suite chưa sẵn sàng để chạy test.");
        }

        // 5. Validate selected test cases belong to this suite and are enabled
        if (selectedIds.Count > 0)
        {
            var availableIds = await _gatewayService.GetTestCaseIdsBySuiteAsync(command.TestSuiteId, cancellationToken);
            var availableIdSet = (availableIds ?? Array.Empty<Guid>()).ToHashSet();
            var invalidIds = selectedIds.Where(id => !availableIdSet.Contains(id)).ToList();

            if (invalidIds.Count > 0)
            {
                var displayIds = string.Join(", ", invalidIds.Take(5));
                var suffix = invalidIds.Count > 5 ? $" và {invalidIds.Count - 5} ID khác" : string.Empty;

                throw new ValidationException(
                    $"Danh sách test case đã chọn có ID không thuộc suite hoặc đã bị vô hiệu hóa: {displayIds}{suffix}.");
            }
        }

        validateSw.Stop();
        _logger.LogInformation(
            "test_run.start.validate completed. TestSuiteId={TestSuiteId}, CurrentUserId={CurrentUserId}, SelectedCaseCount={SelectedCaseCount}, DurationMs={DurationMs}",
            command.TestSuiteId,
            command.CurrentUserId,
            selectedIds.Count,
            validateSw.ElapsedMilliseconds);

        // 6. Resolve environment
        var environmentSw = Stopwatch.StartNew();
        ExecutionEnvironment environment;
        if (command.EnvironmentId.HasValue)
        {
            environment = await _envRepository.FirstOrDefaultAsync(
                _envRepository.GetQueryableSet()
                    .Where(x => x.Id == command.EnvironmentId.Value && x.ProjectId == suiteContext.ProjectId));

            if (environment == null)
            {
                throw new NotFoundException($"Không tìm thấy execution environment với mã '{command.EnvironmentId.Value}'.");
            }
        }
        else
        {
            environment = await _envRepository.FirstOrDefaultAsync(
                _envRepository.GetQueryableSet()
                    .Where(x => x.ProjectId == suiteContext.ProjectId && x.IsDefault));

            if (environment == null)
            {
                throw new NotFoundException("Không tìm thấy execution environment mặc định cho project này.");
            }
        }

        environmentSw.Stop();
        _logger.LogInformation(
            "test_run.environment.resolve completed. TestSuiteId={TestSuiteId}, EnvironmentId={EnvironmentId}, DurationMs={DurationMs}",
            command.TestSuiteId,
            environment.Id,
            environmentSw.ElapsedMilliseconds);

        // 7. Check concurrent run limit
        var currentRunning = await _runRepository.CountAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.TriggeredById == command.CurrentUserId
                    && (x.Status == TestRunStatus.Pending || x.Status == TestRunStatus.Running)),
            cancellationToken);

        var concurrentCheck = await _limitService.CheckLimitAsync(
            command.CurrentUserId,
            LimitType.MaxConcurrentRuns,
            currentRunning + 1,
            cancellationToken);

        if (!concurrentCheck.IsAllowed)
        {
            throw new ValidationException("Đã đạt giới hạn số lượng run đang chạy đồng thời.");
        }

        // 8. Reserve monthly quota
        var monthlyCheck = await _limitService.TryConsumeLimitAsync(
            command.CurrentUserId,
            LimitType.MaxTestRunsPerMonth,
            1,
            cancellationToken);

        if (!monthlyCheck.IsAllowed)
        {
            throw new ValidationException("Đã đạt giới hạn số lượng test run trong tháng.");
        }

        // 9. Allocate RunNumber in Serializable transaction and insert Pending run
        var createRunSw = Stopwatch.StartNew();
        TestRun run = null;
        await _runRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var maxRunNumber = await _runRepository.FirstOrDefaultAsync(
                _runRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId)
                    .OrderByDescending(x => x.RunNumber)
                    .Select(x => (int?)x.RunNumber));

            var runId = Guid.NewGuid();
            run = new TestRun
            {
                Id = runId,
                TestSuiteId = command.TestSuiteId,
                EnvironmentId = environment.Id,
                TriggeredById = command.CurrentUserId,
                RunNumber = (maxRunNumber ?? 0) + 1,
                Status = TestRunStatus.Pending,
                RedisKey = $"testrun:{runId}:results",
                IsEphemeral = !command.RecordRun,
            };

            await _runRepository.AddAsync(run, ct);
            await _runRepository.UnitOfWork.SaveChangesAsync(ct);
        }, isolationLevel: IsolationLevel.Serializable, cancellationToken: cancellationToken);

        createRunSw.Stop();

        _logger.LogInformation(
            "test_run.start.create_run completed. RunId={RunId}, SuiteId={SuiteId}, RunNumber={RunNumber}, EnvironmentId={EnvironmentId}, CurrentRunning={CurrentRunning}, DurationMs={DurationMs}",
            run.Id,
            command.TestSuiteId,
            run.RunNumber,
            environment.Id,
            currentRunning,
            createRunSw.ElapsedMilliseconds);

        // 10. Build effective retry policy.
        //     Explicit RetryPolicy object takes precedence; scalar fields act as fallback.
        //     MaxRetryAttempts is hard-capped at 3 regardless of input source.
        const int maxAllowedRetries = 3;
        var effectivePolicy = command.RetryPolicy != null
            ? command.RetryPolicy.Clone()
            : new TestRunRetryPolicyModel
            {
                MaxRetryAttempts = command.MaxRetryAttempts,
                EnableRetry = command.EnableRetry,
                RerunSkippedCases = command.RerunSkippedCases,
            };

        effectivePolicy.MaxRetryAttempts = Math.Min(effectivePolicy.MaxRetryAttempts, maxAllowedRetries);

        // 11. Execute via orchestrator (outside transaction).
        // ValidationProfile takes precedence: if explicitly set beyond Default, use it;
        // otherwise fall back to legacy StrictValidation bool for backward compatibility.
        var effectiveProfile = command.ValidationProfile != ValidationProfile.Default
            ? command.ValidationProfile
            : command.StrictValidation ? ValidationProfile.SrsStrict : ValidationProfile.Default;

        command.Result = await _orchestrator.ExecuteAsync(
            run.Id,
            command.CurrentUserId,
            selectedIds.Count > 0 ? selectedIds : null,
            cancellationToken,
            command.StrictValidation,
            effectivePolicy,
            effectiveProfile);

        totalSw.Stop();
        _logger.LogInformation(
            "test_run.start.completed. RunId={RunId}, SuiteId={SuiteId}, TotalDurationMs={DurationMs}",
            run.Id,
            command.TestSuiteId,
            totalSw.ElapsedMilliseconds);
    }
}
