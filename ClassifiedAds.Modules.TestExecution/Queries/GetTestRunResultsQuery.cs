using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Caching.Distributed;
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

    public GetTestRunResultsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache,
        ITestExecutionReadGatewayService gatewayService)
    {
        _runRepository = runRepository;
        _cache = cache;
        _gatewayService = gatewayService;
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
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiết kết quả đã hết hạn lưu trữ trong cache.");
        }

        // Check expiry
        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiết kết quả đã hết hạn lưu trữ trong cache.");
        }

        var cached = await _cache.GetStringAsync(run.RedisKey, cancellationToken);
        if (cached == null)
        {
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiết kết quả đã hết hạn lưu trữ trong cache.");
        }

        var result = JsonSerializer.Deserialize<TestRunResultModel>(cached, JsonOptions);
        result.Run = TestRunModel.FromEntity(run);
        return result;
    }
}
