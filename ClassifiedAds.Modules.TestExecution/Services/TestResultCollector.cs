using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class TestResultCollector : ITestResultCollector
{
    private const int MaxResponseBodyPreviewLength = 65536;
    private static readonly string[] SensitiveKeywords = { "token", "secret", "password", "apikey" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TestResultCollector> _logger;

    public TestResultCollector(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache,
        ILogger<TestResultCollector> logger)
    {
        _runRepository = runRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TestRunResultModel> CollectAsync(
        TestRun run,
        IReadOnlyList<TestCaseExecutionResult> caseResults,
        int retentionDays,
        string environmentName,
        CancellationToken ct = default)
    {
        var passedCount = caseResults.Count(r => r.Status == "Passed");
        var failedCount = caseResults.Count(r => r.Status == "Failed");
        var skippedCount = caseResults.Count(r => r.Status == "Skipped");
        var totalDurationMs = caseResults.Sum(r => r.DurationMs);

        // Build result model
        var caseModels = caseResults.Select(r => new TestCaseRunResultModel
        {
            TestCaseId = r.TestCaseId,
            EndpointId = r.EndpointId,
            Name = r.Name,
            OrderIndex = r.OrderIndex,
            Status = r.Status,
            HttpStatusCode = r.HttpStatusCode,
            DurationMs = r.DurationMs,
            ResolvedUrl = r.ResolvedUrl,
            RequestHeaders = r.RequestHeaders ?? new Dictionary<string, string>(),
            ResponseHeaders = r.ResponseHeaders ?? new Dictionary<string, string>(),
            ResponseBodyPreview = TruncateBody(r.ResponseBody),
            FailureReasons = r.FailureReasons ?? new List<ValidationFailureModel>(),
            ExtractedVariables = MaskSensitiveVariables(r.ExtractedVariables),
            DependencyIds = r.DependencyIds?.ToList() ?? new List<Guid>(),
            SkippedBecauseDependencyIds = r.SkippedBecauseDependencyIds ?? new List<Guid>(),
        }).ToList();

        var resultModel = new TestRunResultModel
        {
            ResultsSource = "cache",
            ExecutedAt = run.StartedAt ?? DateTimeOffset.UtcNow,
            ResolvedEnvironmentName = environmentName,
            Cases = caseModels,
        };

        // Update run entity
        run.TotalTests = caseResults.Count;
        run.PassedCount = passedCount;
        run.FailedCount = failedCount;
        run.SkippedCount = skippedCount;
        run.DurationMs = totalDurationMs;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.Status = failedCount > 0 ? TestRunStatus.Failed : TestRunStatus.Completed;
        run.ResultsExpireAt = DateTimeOffset.UtcNow.AddDays(retentionDays > 0 ? retentionDays : 7);

        // Try to save cache payload
        bool cacheSaved = false;
        try
        {
            var payload = JsonSerializer.Serialize(resultModel, JsonOptions);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = run.ResultsExpireAt,
            };

            await _cache.SetStringAsync(run.RedisKey, payload, cacheOptions, ct);
            cacheSaved = true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to save test run results to cache. RunId={RunId}, RedisKey={RedisKey}", run.Id, run.RedisKey);
            run.Status = TestRunStatus.Failed;
        }

        // Always persist summary to DB
        try
        {
            await _runRepository.UpdateAsync(run, ct);
            await _runRepository.UnitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to update test run summary. RunId={RunId}", run.Id);
            throw;
        }

        // If cache failed after summary is saved, propagate
        if (!cacheSaved)
        {
            _logger.LogWarning("Test run summary saved but cache write failed. RunId={RunId}", run.Id);
        }

        resultModel.Run = TestRunModel.FromEntity(run);
        return resultModel;
    }

    private static string TruncateBody(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        return body.Length > MaxResponseBodyPreviewLength
            ? body[..MaxResponseBodyPreviewLength]
            : body;
    }

    private static Dictionary<string, string> MaskSensitiveVariables(Dictionary<string, string> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var masked = new Dictionary<string, string>(variables.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in variables)
        {
            var isSensitive = SensitiveKeywords.Any(kw =>
                kvp.Key.Contains(kw, StringComparison.OrdinalIgnoreCase));

            masked[kvp.Key] = isSensitive ? "******" : kvp.Value;
        }

        return masked;
    }
}
