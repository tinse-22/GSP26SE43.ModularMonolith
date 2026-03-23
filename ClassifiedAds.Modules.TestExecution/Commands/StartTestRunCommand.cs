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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Commands;

public class StartTestRunCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid? EnvironmentId { get; set; }

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
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(command.TestSuiteId, cancellationToken);

        // 3. Validate ownership
        if (suiteContext.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        // 4. Validate status
        if (suiteContext.Status != "Ready")
        {
            throw new ValidationException("Test suite chưa sẵn sàng để chạy test.");
        }

        // 5. Resolve environment
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

        // 6. Check concurrent run limit
        var runningCount = await _runRepository.ToListAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.TriggeredById == command.CurrentUserId
                    && (x.Status == TestRunStatus.Pending || x.Status == TestRunStatus.Running)));
        var currentRunning = runningCount.Count;

        var concurrentCheck = await _limitService.CheckLimitAsync(
            command.CurrentUserId,
            LimitType.MaxConcurrentRuns,
            currentRunning + 1,
            cancellationToken);

        if (!concurrentCheck.IsAllowed)
        {
            throw new ValidationException("Đã đạt giới hạn số lượng run đang chạy đồng thời.");
        }

        // 7. Reserve monthly quota
        var monthlyCheck = await _limitService.TryConsumeLimitAsync(
            command.CurrentUserId,
            LimitType.MaxTestRunsPerMonth,
            1,
            cancellationToken);

        if (!monthlyCheck.IsAllowed)
        {
            throw new ValidationException("Đã đạt giới hạn số lượng test run trong tháng.");
        }

        // 8. Allocate RunNumber in Serializable transaction and insert Pending run
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
            };

            await _runRepository.AddAsync(run, ct);
            await _runRepository.UnitOfWork.SaveChangesAsync(ct);
        }, isolationLevel: IsolationLevel.Serializable, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created test run. RunId={RunId}, SuiteId={SuiteId}, RunNumber={RunNumber}, EnvironmentId={EnvironmentId}",
            run.Id, command.TestSuiteId, run.RunNumber, environment.Id);

        // 9. Execute via orchestrator (outside transaction)
        command.Result = await _orchestrator.ExecuteAsync(
            run.Id,
            command.CurrentUserId,
            selectedIds.Count > 0 ? selectedIds : null,
            cancellationToken);
    }
}
