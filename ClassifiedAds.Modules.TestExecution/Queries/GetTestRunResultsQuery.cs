using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Queries;

public class GetTestRunResultsQuery : IQuery<TestRunResultModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetTestRunResultsQueryHandler : IQueryHandler<GetTestRunResultsQuery, TestRunResultModel>
{
    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IRepository<TestCaseResult, Guid> _resultRepository;
    private readonly IDistributedCache _cache;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly ILogger<GetTestRunResultsQueryHandler> _logger;

    public GetTestRunResultsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        IRepository<TestCaseResult, Guid> resultRepository,
        IDistributedCache cache,
        ITestExecutionReadGatewayService gatewayService,
        ILogger<GetTestRunResultsQueryHandler> logger)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _cache = cache;
        _gatewayService = gatewayService;
        _logger = logger;
    }

    public async Task<TestRunResultModel> HandleAsync(GetTestRunResultsQuery query, CancellationToken cancellationToken = default)
    {
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == query.RunId && x.TestSuiteId == query.TestSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Không tìm thấy test run với mã '{query.RunId}'.");
        }

        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            return CreateUnavailableResult(run);
        }

        var result = await TryReadFromCacheAsync(run, cancellationToken)
            ?? await TryReadFromDatabaseAsync(run, cancellationToken);

        if (result == null)
        {
            return CreateUnavailableResult(run);
        }

        var definitions = await LoadDefinitionsAsync(
            run.TestSuiteId,
            result.Cases.Select(x => x.TestCaseId).ToArray(),
            cancellationToken);
        TestRunResultsStorage.ApplyDefinitionFallbacks(result, definitions);
        result.Run = TestRunModel.FromEntity(run);
        return result;
    }

    private async Task<TestRunResultModel> TryReadFromCacheAsync(TestRun run, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(run.RedisKey))
        {
            return null;
        }

        try
        {
            var cached = await _cache.GetStringAsync(run.RedisKey, cancellationToken);
            var result = TestRunResultsStorage.DeserializeCachedResult(cached);
            if (result != null)
            {
                result.ResultsSource = "cache";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read test run results from cache. RunId={RunId}, RedisKey={RedisKey}", run.Id, run.RedisKey);
            return null;
        }
    }

    private async Task<TestRunResultModel> TryReadFromDatabaseAsync(TestRun run, CancellationToken cancellationToken)
    {
        try
        {
            var persistedResults = await _resultRepository.ToListAsync(
                _resultRepository.GetQueryableSet()
                    .Where(x => x.TestRunId == run.Id)
                    .OrderBy(x => x.OrderIndex));

            var definitions = await LoadDefinitionsAsync(
                run.TestSuiteId,
                persistedResults.Select(x => x.TestCaseId).ToArray(),
                cancellationToken);
            return TestRunResultsStorage.ReconstructFromDatabase(run, persistedResults, definitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read test run results from database fallback. RunId={RunId}", run.Id);
            return null;
        }
    }

    private async Task<IReadOnlyList<ExecutionTestCaseDto>> LoadDefinitionsAsync(
        Guid testSuiteId,
        IReadOnlyCollection<Guid> testCaseIds,
        CancellationToken cancellationToken)
    {
        if (testCaseIds == null || testCaseIds.Count == 0)
        {
            return Array.Empty<ExecutionTestCaseDto>();
        }

        try
        {
            var context = await _gatewayService.GetExecutionContextAsync(
                testSuiteId,
                testCaseIds,
                cancellationToken);
            return context?.OrderedTestCases ?? Array.Empty<ExecutionTestCaseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load test case definitions for result fallback. TestSuiteId={TestSuiteId}", testSuiteId);
            return Array.Empty<ExecutionTestCaseDto>();
        }
    }

    private static TestRunResultModel CreateUnavailableResult(TestRun run)
    {
        return new TestRunResultModel
        {
            Run = TestRunModel.FromEntity(run),
            ResultsSource = "unavailable",
            ExecutedAt = run.CompletedAt ?? run.StartedAt ?? DateTimeOffset.UtcNow,
            ResolvedEnvironmentName = string.Empty,
            Cases = new(),
        };
    }
}
