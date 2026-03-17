using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class TestRunReportReadGatewayService : ITestRunReportReadGatewayService
{
    private const int MinRecentHistoryLimit = 1;
    private const int MaxRecentHistoryLimit = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IDistributedCache _cache;
    private readonly ITestExecutionReadGatewayService _executionReadGatewayService;

    public TestRunReportReadGatewayService(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache,
        ITestExecutionReadGatewayService executionReadGatewayService)
    {
        _runRepository = runRepository;
        _cache = cache;
        _executionReadGatewayService = executionReadGatewayService;
    }

    public async Task<TestRunReportContextDto> GetReportContextAsync(
        Guid testSuiteId,
        Guid runId,
        int recentHistoryLimit = 5,
        CancellationToken ct = default)
    {
        var suiteContext = await _executionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId, ct);

        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == runId && x.TestSuiteId == testSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Khong tim thay test run voi ma '{runId}'.");
        }

        if (run.Status != TestRunStatus.Completed && run.Status != TestRunStatus.Failed)
        {
            throw new ConflictException(
                "REPORT_RUN_NOT_READY",
                "Chi co the tao report sau khi test run da Completed hoac Failed.");
        }

        var runResults = await GetRunResultsAsync(run, ct);
        var executionContext = await _executionReadGatewayService.GetExecutionContextAsync(testSuiteId, null, ct);
        var normalizedRecentHistoryLimit = Math.Clamp(recentHistoryLimit, MinRecentHistoryLimit, MaxRecentHistoryLimit);

        var recentRuns = await _runRepository.ToListAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId && x.Id != runId)
                .OrderByDescending(x => x.RunNumber)
                .ThenByDescending(x => x.Id)
                .Take(normalizedRecentHistoryLimit));

        return new TestRunReportContextDto
        {
            TestSuiteId = suiteContext.TestSuiteId,
            ProjectId = suiteContext.ProjectId,
            ApiSpecId = suiteContext.ApiSpecId,
            CreatedById = suiteContext.CreatedById,
            SuiteName = suiteContext.Name,
            Run = MapRun(run, runResults),
            RecentRuns = recentRuns.Select(MapHistoryItem).ToArray(),
            OrderedEndpointIds = executionContext.OrderedEndpointIds?.ToArray() ?? Array.Empty<Guid>(),
            Definitions = (executionContext.OrderedTestCases ?? Array.Empty<ExecutionTestCaseDto>())
                .Select(MapDefinition)
                .ToArray(),
            Results = (runResults.Cases ?? new List<TestCaseRunResultModel>())
                .OrderBy(x => x.OrderIndex)
                .ThenBy(x => x.TestCaseId)
                .Select(MapResult)
                .ToArray(),
        };
    }

    private async Task<TestRunResultModel> GetRunResultsAsync(TestRun run, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(run.RedisKey))
        {
            throw CreateRunResultsExpiredException();
        }

        if (run.ResultsExpireAt.HasValue && run.ResultsExpireAt.Value < DateTimeOffset.UtcNow)
        {
            throw CreateRunResultsExpiredException();
        }

        var cached = await _cache.GetStringAsync(run.RedisKey, ct);
        if (cached == null)
        {
            throw CreateRunResultsExpiredException();
        }

        return JsonSerializer.Deserialize<TestRunResultModel>(cached, JsonOptions)
            ?? throw CreateRunResultsExpiredException();
    }

    private static TestRunReportRunDto MapRun(TestRun run, TestRunResultModel runResults)
    {
        return new TestRunReportRunDto
        {
            TestRunId = run.Id,
            RunNumber = run.RunNumber,
            EnvironmentId = run.EnvironmentId,
            TriggeredById = run.TriggeredById,
            Status = run.Status.ToString(),
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            DurationMs = run.DurationMs,
            TotalTests = run.TotalTests,
            PassedCount = run.PassedCount,
            FailedCount = run.FailedCount,
            SkippedCount = run.SkippedCount,
            ResolvedEnvironmentName = runResults.ResolvedEnvironmentName,
            ExecutedAt = runResults.ExecutedAt,
        };
    }

    private static TestRunHistoryItemDto MapHistoryItem(TestRun run)
    {
        return new TestRunHistoryItemDto
        {
            TestRunId = run.Id,
            RunNumber = run.RunNumber,
            Status = run.Status.ToString(),
            CompletedAt = run.CompletedAt,
            DurationMs = run.DurationMs,
            PassedCount = run.PassedCount,
            FailedCount = run.FailedCount,
            SkippedCount = run.SkippedCount,
        };
    }

    private static ReportTestCaseDefinitionDto MapDefinition(ExecutionTestCaseDto definition)
    {
        return new ReportTestCaseDefinitionDto
        {
            TestCaseId = definition.TestCaseId,
            EndpointId = definition.EndpointId,
            Name = definition.Name,
            Description = definition.Description,
            TestType = definition.TestType,
            OrderIndex = definition.OrderIndex,
            DependencyIds = definition.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            Request = MapRequest(definition.Request),
            Expectation = MapExpectation(definition.Expectation),
        };
    }

    private static ReportTestCaseResultDto MapResult(TestCaseRunResultModel result)
    {
        return new ReportTestCaseResultDto
        {
            TestCaseId = result.TestCaseId,
            EndpointId = result.EndpointId,
            Name = result.Name,
            OrderIndex = result.OrderIndex,
            Status = result.Status,
            HttpStatusCode = result.HttpStatusCode,
            DurationMs = result.DurationMs,
            ResolvedUrl = result.ResolvedUrl,
            RequestHeaders = result.RequestHeaders != null
                ? new Dictionary<string, string>(result.RequestHeaders)
                : new Dictionary<string, string>(),
            ResponseHeaders = result.ResponseHeaders != null
                ? new Dictionary<string, string>(result.ResponseHeaders)
                : new Dictionary<string, string>(),
            ResponseBodyPreview = result.ResponseBodyPreview,
            FailureReasons = result.FailureReasons?.Select(x => new ReportValidationFailureDto
            {
                Code = x.Code,
                Message = x.Message,
                Target = x.Target,
                Expected = x.Expected,
                Actual = x.Actual,
            }).ToArray() ?? Array.Empty<ReportValidationFailureDto>(),
            ExtractedVariables = result.ExtractedVariables != null
                ? new Dictionary<string, string>(result.ExtractedVariables)
                : new Dictionary<string, string>(),
            DependencyIds = result.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            SkippedBecauseDependencyIds = result.SkippedBecauseDependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            StatusCodeMatched = result.StatusCodeMatched,
            SchemaMatched = result.SchemaMatched,
            HeaderChecksPassed = result.HeaderChecksPassed,
            BodyContainsPassed = result.BodyContainsPassed,
            BodyNotContainsPassed = result.BodyNotContainsPassed,
            JsonPathChecksPassed = result.JsonPathChecksPassed,
            ResponseTimePassed = result.ResponseTimePassed,
        };
    }

    private static ExecutionTestCaseRequestDto MapRequest(ExecutionTestCaseRequestDto request)
    {
        if (request == null)
        {
            return null;
        }

        return new ExecutionTestCaseRequestDto
        {
            HttpMethod = request.HttpMethod,
            Url = request.Url,
            Headers = request.Headers,
            PathParams = request.PathParams,
            QueryParams = request.QueryParams,
            BodyType = request.BodyType,
            Body = request.Body,
            Timeout = request.Timeout,
        };
    }

    private static ExecutionTestCaseExpectationDto MapExpectation(ExecutionTestCaseExpectationDto expectation)
    {
        if (expectation == null)
        {
            return null;
        }

        return new ExecutionTestCaseExpectationDto
        {
            ExpectedStatus = expectation.ExpectedStatus,
            ResponseSchema = expectation.ResponseSchema,
            HeaderChecks = expectation.HeaderChecks,
            BodyContains = expectation.BodyContains,
            BodyNotContains = expectation.BodyNotContains,
            JsonPathChecks = expectation.JsonPathChecks,
            MaxResponseTime = expectation.MaxResponseTime,
        };
    }

    private static ConflictException CreateRunResultsExpiredException()
    {
        return new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiet ket qua da het han luu tru trong cache.");
    }
}
