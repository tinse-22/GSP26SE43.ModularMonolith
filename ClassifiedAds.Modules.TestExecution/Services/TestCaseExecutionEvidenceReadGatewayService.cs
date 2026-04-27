using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestExecution.Services;
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

/// <summary>
/// Implements <see cref="ITestCaseExecutionEvidenceReadGatewayService"/>.
/// Reads evidence from PostgreSQL cold storage (TestCaseResult) with a Redis hot-cache read
/// attempt first when available.
/// </summary>
public class TestCaseExecutionEvidenceReadGatewayService : ITestCaseExecutionEvidenceReadGatewayService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IRepository<TestCaseResult, Guid> _resultRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TestCaseExecutionEvidenceReadGatewayService> _logger;

    public TestCaseExecutionEvidenceReadGatewayService(
        IRepository<TestRun, Guid> runRepository,
        IRepository<TestCaseResult, Guid> resultRepository,
        IDistributedCache cache,
        ILogger<TestCaseExecutionEvidenceReadGatewayService> logger)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestCaseExecutionEvidenceDto>> GetLatestEvidenceByTestSuiteAsync(
        Guid testSuiteId,
        Guid? testRunId,
        CancellationToken cancellationToken = default)
    {
        TestRun run;

        if (testRunId.HasValue)
        {
            run = await _runRepository.FirstOrDefaultAsync(
                _runRepository.GetQueryableSet()
                    .Where(x => x.Id == testRunId.Value && x.TestSuiteId == testSuiteId));

            if (run == null)
            {
                _logger.LogWarning(
                    "TestRun {RunId} not found for TestSuiteId {SuiteId} when fetching evidence.",
                    testRunId, testSuiteId);
                return Array.Empty<TestCaseExecutionEvidenceDto>();
            }
        }
        else
        {
            // Latest finished run for the suite (Completed or Failed lifecycle status)
            run = await _runRepository.FirstOrDefaultAsync(
                _runRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == testSuiteId
                        && (x.Status == TestRunStatus.Completed || x.Status == TestRunStatus.Failed))
                    .OrderByDescending(x => x.RunNumber));

            if (run == null)
            {
                _logger.LogDebug(
                    "No finished test run found for TestSuiteId {SuiteId} when fetching evidence.",
                    testSuiteId);
                return Array.Empty<TestCaseExecutionEvidenceDto>();
            }
        }

        // Attempt Redis hot-cache first so we can get per-case warnings
        var fromCache = await TryGetFromCacheAsync(run, cancellationToken);
        if (fromCache != null)
        {
            return MapFromCacheResults(testSuiteId, run, fromCache);
        }

        // Fall back to PostgreSQL cold storage
        return await BuildFromDatabaseAsync(testSuiteId, run, cancellationToken);
    }

    // ── Redis hot-cache path ─────────────────────────────────────────────────

    private async Task<TestRunResultModel> TryGetFromCacheAsync(TestRun run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.RedisKey))
        {
            return null;
        }

        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            return null;
        }

        try
        {
            var cached = await _cache.GetStringAsync(run.RedisKey, ct);
            return TestRunResultsStorage.DeserializeCachedResult(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Redis read failed for evidence. RunId={RunId}, RedisKey={RedisKey}. Falling back to PostgreSQL.",
                run.Id, run.RedisKey);
            return null;
        }
    }

    private static IReadOnlyList<TestCaseExecutionEvidenceDto> MapFromCacheResults(
        Guid testSuiteId,
        TestRun run,
        TestRunResultModel cached)
    {
        return (cached.Cases ?? new List<TestCaseRunResultModel>())
            .Select(c => new TestCaseExecutionEvidenceDto
            {
                TestSuiteId = testSuiteId,
                TestRunId = run.Id,
                RunNumber = run.RunNumber,
                CompletedAt = run.CompletedAt,
                TestCaseId = c.TestCaseId,
                Status = c.Status,
                HttpStatusCode = c.HttpStatusCode,
                FailureCodes = (c.FailureReasons ?? new List<ValidationFailureModel>())
                    .Select(f => f.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .ToList(),
                FailureSummary = BuildFailureSummary(c.FailureReasons),
                HasAdaptiveWarning = (c.Warnings ?? new List<ValidationWarningModel>())
                    .Any(w => w.Code != null && w.Code.StartsWith("ADAPTIVE_", StringComparison.OrdinalIgnoreCase)),
                WarningCodes = (c.Warnings ?? new List<ValidationWarningModel>())
                    .Select(w => w.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .ToList(),
            })
            .ToList();
    }

    // ── PostgreSQL cold-storage path ─────────────────────────────────────────

    private async Task<IReadOnlyList<TestCaseExecutionEvidenceDto>> BuildFromDatabaseAsync(
        Guid testSuiteId,
        TestRun run,
        CancellationToken ct)
    {
        var dbResults = await _resultRepository.ToListAsync(
            _resultRepository.GetQueryableSet()
                .Where(x => x.TestRunId == run.Id));

        return dbResults.Select(r => MapFromDbResult(testSuiteId, run, r)).ToList();
    }

    private static TestCaseExecutionEvidenceDto MapFromDbResult(
        Guid testSuiteId,
        TestRun run,
        TestCaseResult r)
    {
        var failureCodes = DeserializeFailureCodes(r.FailureReasons);
        var failureSummary = DeserializeFailureSummary(r.FailureReasons);

        return new TestCaseExecutionEvidenceDto
        {
            TestSuiteId = testSuiteId,
            TestRunId = run.Id,
            RunNumber = run.RunNumber,
            CompletedAt = run.CompletedAt,
            TestCaseId = r.TestCaseId,
            Status = r.Status,
            HttpStatusCode = r.HttpStatusCode,
            FailureCodes = failureCodes,
            FailureSummary = failureSummary,
            // Warnings are not persisted to PostgreSQL — only available from Redis hot-cache.
            HasAdaptiveWarning = false,
            WarningCodes = Array.Empty<string>(),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string> DeserializeFailureCodes(string failureReasonsJson)
    {
        if (string.IsNullOrWhiteSpace(failureReasonsJson))
        {
            return new List<string>();
        }

        try
        {
            var failures = JsonSerializer.Deserialize<List<ValidationFailureModel>>(
                failureReasonsJson, JsonOptions);

            return (failures ?? new List<ValidationFailureModel>())
                .Select(f => f.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static string DeserializeFailureSummary(string failureReasonsJson)
    {
        if (string.IsNullOrWhiteSpace(failureReasonsJson))
        {
            return null;
        }

        try
        {
            var failures = JsonSerializer.Deserialize<List<ValidationFailureModel>>(
                failureReasonsJson, JsonOptions);

            return BuildFailureSummary(failures);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildFailureSummary(IEnumerable<ValidationFailureModel> failures)
    {
        var list = failures?.Where(f => f != null && !string.IsNullOrWhiteSpace(f.Code)).ToList();
        if (list == null || list.Count == 0)
        {
            return null;
        }

        var first = list[0];
        var summary = $"{first.Code}: {first.Message}".TrimEnd('.');
        if (list.Count > 1)
        {
            summary += $" (+{list.Count - 1} more)";
        }

        return summary;
    }
}
