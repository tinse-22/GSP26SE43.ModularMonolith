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

public class TestFailureReadGatewayService : ITestFailureReadGatewayService
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IDistributedCache _cache;
    private readonly ITestExecutionReadGatewayService _executionReadGatewayService;

    public TestFailureReadGatewayService(
        IRepository<TestRun, Guid> runRepository,
        IDistributedCache cache,
        ITestExecutionReadGatewayService executionReadGatewayService)
    {
        _runRepository = runRepository;
        _cache = cache;
        _executionReadGatewayService = executionReadGatewayService;
    }

    public async Task<TestFailureExplanationContextDto> GetFailureExplanationContextAsync(
        Guid testSuiteId,
        Guid runId,
        Guid testCaseId,
        CancellationToken ct = default)
    {
        var suiteContext = await _executionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId, ct);

        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == runId && x.TestSuiteId == testSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Không tìm thấy test run với mã '{runId}'.");
        }

        var runResults = await GetRunResultsAsync(run, ct);
        var failedCaseResult = (runResults.Cases ?? new List<TestCaseRunResultModel>())
            .FirstOrDefault(x => x.TestCaseId == testCaseId);

        if (failedCaseResult == null)
        {
            throw new NotFoundException($"Không tìm thấy test case cần giải thích trong test run '{runId}'.");
        }

        if (!string.Equals(failedCaseResult.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("TEST_CASE_NOT_FAILED", "Chỉ có thể giải thích test case thất bại.");
        }

        var executionContext = await _executionReadGatewayService.GetExecutionContextAsync(testSuiteId, null, ct);
        var definition = (executionContext.OrderedTestCases ?? Array.Empty<ExecutionTestCaseDto>())
            .FirstOrDefault(x => x.TestCaseId == testCaseId);

        if (definition == null)
        {
            throw new NotFoundException($"Không tìm thấy test case definition với mã '{testCaseId}' trong test suite '{testSuiteId}'.");
        }

        return new TestFailureExplanationContextDto
        {
            TestSuiteId = suiteContext.TestSuiteId,
            ProjectId = suiteContext.ProjectId,
            ApiSpecId = suiteContext.ApiSpecId,
            CreatedById = suiteContext.CreatedById,
            TestRunId = run.Id,
            RunNumber = run.RunNumber,
            TriggeredById = run.TriggeredById,
            ResolvedEnvironmentName = runResults.ResolvedEnvironmentName,
            ExecutedAt = runResults.ExecutedAt,
            Definition = MapDefinition(definition),
            ActualResult = MapActualResult(failedCaseResult),
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

    private static FailureExplanationDefinitionDto MapDefinition(ExecutionTestCaseDto definition)
    {
        return new FailureExplanationDefinitionDto
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

    private static FailureExplanationActualResultDto MapActualResult(TestCaseRunResultModel actualResult)
    {
        return new FailureExplanationActualResultDto
        {
            Status = actualResult.Status,
            HttpStatusCode = actualResult.HttpStatusCode,
            DurationMs = actualResult.DurationMs,
            ResolvedUrl = actualResult.ResolvedUrl,
            RequestHeaders = actualResult.RequestHeaders != null
                ? new Dictionary<string, string>(actualResult.RequestHeaders)
                : new Dictionary<string, string>(),
            ResponseHeaders = actualResult.ResponseHeaders != null
                ? new Dictionary<string, string>(actualResult.ResponseHeaders)
                : new Dictionary<string, string>(),
            ResponseBodyPreview = actualResult.ResponseBodyPreview,
            FailureReasons = actualResult.FailureReasons?.Select(x => new FailureExplanationFailureReasonDto
            {
                Code = x.Code,
                Message = x.Message,
                Target = x.Target,
                Expected = x.Expected,
                Actual = x.Actual,
            }).ToArray() ?? Array.Empty<FailureExplanationFailureReasonDto>(),
            ExtractedVariables = actualResult.ExtractedVariables != null
                ? new Dictionary<string, string>(actualResult.ExtractedVariables)
                : new Dictionary<string, string>(),
            DependencyIds = actualResult.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            SkippedBecauseDependencyIds = actualResult.SkippedBecauseDependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            StatusCodeMatched = actualResult.StatusCodeMatched,
            SchemaMatched = actualResult.SchemaMatched,
            HeaderChecksPassed = actualResult.HeaderChecksPassed,
            BodyContainsPassed = actualResult.BodyContainsPassed,
            BodyNotContainsPassed = actualResult.BodyNotContainsPassed,
            JsonPathChecksPassed = actualResult.JsonPathChecksPassed,
            ResponseTimePassed = actualResult.ResponseTimePassed,
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
        return new ConflictException("RUN_RESULTS_EXPIRED", "Chi tiết kết quả đã hết hạn lưu trữ trong cache.");
    }
}
