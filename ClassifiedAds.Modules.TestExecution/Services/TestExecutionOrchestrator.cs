using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class TestExecutionOrchestrator : ITestExecutionOrchestrator
{
    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly ISubscriptionLimitGatewayService _limitService;
    private readonly IExecutionEnvironmentRuntimeResolver _envResolver;
    private readonly IVariableResolver _variableResolver;
    private readonly IHttpTestExecutor _httpExecutor;
    private readonly IVariableExtractor _variableExtractor;
    private readonly IRuleBasedValidator _validator;
    private readonly ITestResultCollector _resultCollector;
    private readonly IPreExecutionValidator _preValidator;
    private readonly ILogger<TestExecutionOrchestrator> _logger;

    public TestExecutionOrchestrator(
        IRepository<TestRun, Guid> runRepository,
        IRepository<ExecutionEnvironment, Guid> envRepository,
        ITestExecutionReadGatewayService gatewayService,
        IApiEndpointMetadataService endpointMetadataService,
        ISubscriptionLimitGatewayService limitService,
        IExecutionEnvironmentRuntimeResolver envResolver,
        IVariableResolver variableResolver,
        IHttpTestExecutor httpExecutor,
        IVariableExtractor variableExtractor,
        IRuleBasedValidator validator,
        ITestResultCollector resultCollector,
        IPreExecutionValidator preValidator,
        ILogger<TestExecutionOrchestrator> logger)
    {
        _runRepository = runRepository;
        _envRepository = envRepository;
        _gatewayService = gatewayService;
        _endpointMetadataService = endpointMetadataService;
        _limitService = limitService;
        _envResolver = envResolver;
        _variableResolver = variableResolver;
        _httpExecutor = httpExecutor;
        _variableExtractor = variableExtractor;
        _validator = validator;
        _resultCollector = resultCollector;
        _preValidator = preValidator;
        _logger = logger;
    }

    public async Task<TestRunResultModel> ExecuteAsync(
        Guid testRunId,
        Guid currentUserId,
        IReadOnlyCollection<Guid> selectedTestCaseIds,
        CancellationToken ct = default,
        bool strictValidation = false)
    {
        // Load run record
        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet().Where(x => x.Id == testRunId));

        // Load execution context from gateway
        var executionContext = await _gatewayService.GetExecutionContextAsync(
            run.TestSuiteId,
            selectedTestCaseIds,
            ct);

        // Load environment
        var environment = await _envRepository.FirstOrDefaultAsync(
            _envRepository.GetQueryableSet().Where(x => x.Id == run.EnvironmentId));

        // Resolve runtime environment (auth, headers, etc.) - once per run
        var resolvedEnv = await _envResolver.ResolveAsync(environment, ct);

        // Get retention days from subscription
        var retentionCheck = await _limitService.CheckLimitAsync(
            currentUserId, LimitType.RetentionDays, 0, ct);
        var retentionDays = retentionCheck.LimitValue ?? 7;

        // Load endpoint metadata for schema fallback - one batch per run
        Dictionary<Guid, ApiEndpointMetadataDto> endpointMetadataMap = new();
        if (executionContext.Suite.ApiSpecId.HasValue && executionContext.OrderedEndpointIds.Count > 0)
        {
            var metadata = await _endpointMetadataService.GetEndpointMetadataAsync(
                executionContext.Suite.ApiSpecId.Value,
                executionContext.OrderedEndpointIds,
                ct);
            endpointMetadataMap = metadata.ToDictionary(m => m.EndpointId);
        }

        // Update run status to Running
        run.Status = TestRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        run.TotalTests = executionContext.OrderedTestCases.Count;
        await _runRepository.UpdateAsync(run, ct);
        await _runRepository.UnitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Test run started. RunId={RunId}, TotalTests={TotalTests}", run.Id, run.TotalTests);

        // Sequential execution loop
        var variableBag = new Dictionary<string, string>(resolvedEnv.Variables, StringComparer.OrdinalIgnoreCase);
        var caseResults = new List<TestCaseExecutionResult>();
        var caseStatusMap = new Dictionary<Guid, string>();

        foreach (var testCase in executionContext.OrderedTestCases)
        {
            var result = await ExecuteTestCase(
                testCase, resolvedEnv, variableBag, caseStatusMap, endpointMetadataMap, ct, strictValidation);

            caseResults.Add(result);
            caseStatusMap[testCase.TestCaseId] = result.Status;
        }

        // Collect results
        return await _resultCollector.CollectAsync(run, caseResults, retentionDays, resolvedEnv.Name, ct);
    }

    private async Task<TestCaseExecutionResult> ExecuteTestCase(
        ExecutionTestCaseDto testCase,
        ResolvedExecutionEnvironment resolvedEnv,
        Dictionary<string, string> variableBag,
        Dictionary<Guid, string> caseStatusMap,
        Dictionary<Guid, ApiEndpointMetadataDto> endpointMetadataMap,
        CancellationToken ct,
        bool strictValidation)
    {
        // Check dependencies
        var failedDeps = testCase.DependencyIds
            .Where(depId => caseStatusMap.TryGetValue(depId, out var status) && status != "Passed")
            .ToList();

        if (failedDeps.Count > 0)
        {
            return new TestCaseExecutionResult
            {
                TestCaseId = testCase.TestCaseId,
                EndpointId = testCase.EndpointId,
                Name = testCase.Name,
                OrderIndex = testCase.OrderIndex,
                Status = "Skipped",
                DependencyIds = testCase.DependencyIds,
                SkippedBecauseDependencyIds = failedDeps,
                FailureReasons = new List<ValidationFailureModel>
                {
                    new()
                    {
                        Code = "DEPENDENCY_FAILED",
                        Message = "Test case bị bỏ qua vì dependency không thành công.",
                    },
                },
            };
        }

        // Pre-execution validation: catch ALL issues before HTTP call
        endpointMetadataMap.TryGetValue(testCase.EndpointId ?? Guid.Empty, out var endpointMetadata);
        var preValidation = _preValidator.Validate(testCase, resolvedEnv, variableBag, endpointMetadata);

        if (preValidation.HasErrors)
        {
            _logger.LogWarning(
                "Pre-execution validation failed for TestCase={TestCaseName} ({TestCaseId}). Errors={ErrorCount}",
                testCase.Name, testCase.TestCaseId, preValidation.Errors.Count);

            return new TestCaseExecutionResult
            {
                TestCaseId = testCase.TestCaseId,
                EndpointId = testCase.EndpointId,
                Name = testCase.Name,
                TestType = testCase.TestType,
                OrderIndex = testCase.OrderIndex,
                Status = "Failed",
                DependencyIds = testCase.DependencyIds,
                FailureReasons = preValidation.ToFailureReasons(),
                Warnings = preValidation.Warnings,
            };
        }

        // Resolve request
        ResolvedTestCaseRequest resolvedRequest;
        try
        {
            resolvedRequest = _variableResolver.Resolve(testCase, variableBag, resolvedEnv);
        }
        catch (UnresolvedVariableException ex)
        {
            return new TestCaseExecutionResult
            {
                TestCaseId = testCase.TestCaseId,
                EndpointId = testCase.EndpointId,
                Name = testCase.Name,
                OrderIndex = testCase.OrderIndex,
                Status = "Failed",
                DependencyIds = testCase.DependencyIds,
                FailureReasons = new List<ValidationFailureModel>
                {
                    new()
                    {
                        Code = "UNRESOLVED_VARIABLE",
                        Message = ex.Message,
                    },
                },
            };
        }

        // Execute HTTP request
        var response = await _httpExecutor.ExecuteAsync(resolvedRequest, ct);

        // Extract variables
        var extracted = _variableExtractor.Extract(response, testCase.Variables, resolvedRequest.Body);
        foreach (var kvp in extracted)
        {
            variableBag[kvp.Key] = kvp.Value;
        }

        // Validate response
        var validation = _validator.Validate(response, testCase, endpointMetadata, strictValidation);

        var caseStatus = validation.IsPassed ? "Passed" : "Failed";

        return new TestCaseExecutionResult
        {
            TestCaseId = testCase.TestCaseId,
            EndpointId = testCase.EndpointId,
            Name = testCase.Name,
            TestType = testCase.TestType,
            OrderIndex = testCase.OrderIndex,
            Status = caseStatus,
            HttpStatusCode = response.StatusCode,
            DurationMs = response.LatencyMs,
            ResolvedUrl = resolvedRequest.ResolvedUrl,
            HttpMethod = resolvedRequest.HttpMethod,
            BodyType = resolvedRequest.BodyType,
            RequestBody = resolvedRequest.Body,
            QueryParams = resolvedRequest.QueryParams,
            TimeoutMs = resolvedRequest.TimeoutMs,
            ExpectedStatus = testCase.Expectation?.ExpectedStatus,
            RequestHeaders = resolvedRequest.Headers,
            ResponseHeaders = response.Headers,
            ResponseBody = response.Body,
            FailureReasons = validation.Failures?.ToList() ?? new List<ValidationFailureModel>(),
            Warnings = MergeWarnings(preValidation.Warnings, validation.Warnings),
            ChecksPerformed = validation.ChecksPerformed,
            ChecksSkipped = validation.ChecksSkipped,
            ExtractedVariables = extracted.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            DependencyIds = testCase.DependencyIds,
            StatusCodeMatched = validation.StatusCodeMatched,
            SchemaMatched = validation.SchemaMatched,
            HeaderChecksPassed = validation.HeaderChecksPassed,
            BodyContainsPassed = validation.BodyContainsPassed,
            BodyNotContainsPassed = validation.BodyNotContainsPassed,
            JsonPathChecksPassed = validation.JsonPathChecksPassed,
            ResponseTimePassed = validation.ResponseTimePassed,
        };
    }

    private static List<ValidationWarningModel> MergeWarnings(
        List<ValidationWarningModel> preValidationWarnings,
        IReadOnlyList<ValidationWarningModel> validationWarnings)
    {
        var merged = new List<ValidationWarningModel>();

        if (preValidationWarnings?.Count > 0)
        {
            merged.AddRange(preValidationWarnings);
        }

        if (validationWarnings?.Count > 0)
        {
            merged.AddRange(validationWarnings);
        }

        return merged;
    }
}
