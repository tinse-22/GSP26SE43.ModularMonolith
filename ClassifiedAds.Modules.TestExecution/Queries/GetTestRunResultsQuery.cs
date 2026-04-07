using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
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

        // PRIMARY: Try Redis (hot cache) - only if not expired
        if (!string.IsNullOrEmpty(run.RedisKey) &&
            (!run.ResultsExpireAt.HasValue || run.ResultsExpireAt.Value >= DateTimeOffset.UtcNow))
        {
            try
            {
                var cached = await _cache.GetStringAsync(run.RedisKey, cancellationToken);
                var result = TestRunResultsStorage.DeserializeCachedResult(cached);
                if (result != null)
                {
                    result.Run = TestRunModel.FromEntity(run);
                    result.ResultsSource = "cache";
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache read failed for RunId={RunId}, RedisKey={RedisKey}. Falling back to PostgreSQL.", run.Id, run.RedisKey);
            }
        }

        // FALLBACK: Reconstruct from PostgreSQL (cold storage)
        _logger.LogInformation("Redis unavailable/expired for RunId={RunId}, falling back to PostgreSQL", run.Id);
        try
        {
            var pgResults = await _resultRepository.ToListAsync(
                _resultRepository.GetQueryableSet()
                    .Where(x => x.TestRunId == run.Id)
                    .OrderBy(x => x.OrderIndex));

            var reconstructed = TestRunResultsStorage.ReconstructFromDatabase(run, pgResults);
            if (reconstructed != null)
            {
                _logger.LogInformation("Successfully reconstructed {CaseCount} test cases from PostgreSQL for RunId={RunId}", reconstructed.Cases.Count, run.Id);
                return reconstructed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read test case results from PostgreSQL. RunId={RunId}", run.Id);
        }

        // All fallbacks exhausted
        return CreateUnavailableResult(run);
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
