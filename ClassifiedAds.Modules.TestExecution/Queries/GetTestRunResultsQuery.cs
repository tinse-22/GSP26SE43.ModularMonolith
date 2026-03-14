using ClassifiedAds.Application;
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

    public GetTestRunResultsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache)
    {
        _runRepository = runRepository;
        _cache = cache;
    }

    public async Task<TestRunResultModel> HandleAsync(GetTestRunResultsQuery query, CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == query.RunId && x.TestSuiteId == query.TestSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Khong tim thay test run voi ma '{query.RunId}'.");
        }

        if (string.IsNullOrEmpty(run.RedisKey))
        {
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiet ket qua da het han luu tru trong cache.");
        }

        // Check expiry
        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiet ket qua da het han luu tru trong cache.");
        }

        var cached = await _cache.GetStringAsync(run.RedisKey, cancellationToken);
        if (cached == null)
        {
            throw new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiet ket qua da het han luu tru trong cache.");
        }

        var result = JsonSerializer.Deserialize<TestRunResultModel>(cached, JsonOptions);
        result.Run = TestRunModel.FromEntity(run);
        return result;
    }
}
