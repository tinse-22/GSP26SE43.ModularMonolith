using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IDistributedCache _cache;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly ILogger<GetTestRunResultsQueryHandler> _logger;

    public GetTestRunResultsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache,
        ITestExecutionReadGatewayService gatewayService,
        ILogger<GetTestRunResultsQueryHandler> logger)
    {
        _runRepository = runRepository;
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

        if (string.IsNullOrEmpty(run.RedisKey))
        {
            return CreateUnavailableResult(run);
        }

        // Check expiry
        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            return CreateUnavailableResult(run);
        }

        string cached;
        try
        {
            cached = await _cache.GetStringAsync(run.RedisKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read test run results from cache. RunId={RunId}, RedisKey={RedisKey}", run.Id, run.RedisKey);
            return CreateUnavailableResult(run);
        }

        if (string.IsNullOrEmpty(cached))
        {
            return CreateUnavailableResult(run);
        }

        TestRunResultModel result;
        try
        {
            result = JsonSerializer.Deserialize<TestRunResultModel>(cached, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached test run results. RunId={RunId}", run.Id);
            return CreateUnavailableResult(run);
        }

        if (result == null)
        {
            return CreateUnavailableResult(run);
        }

        result.Run = TestRunModel.FromEntity(run);
        return result;
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
