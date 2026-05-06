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
using System.Text.Json;
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

    /// <inheritdoc />
    public async Task<TestRunResultModel> ExecuteAsync(
        Guid testRunId,
        Guid currentUserId,
        IReadOnlyCollection<Guid> selectedTestCaseIds,
        CancellationToken ct = default,
        bool strictValidation = false,
        TestRunRetryPolicyModel retryPolicy = null,
        ValidationProfile validationProfile = ValidationProfile.Default)
    {
        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet().Where(x => x.Id == testRunId));

        var executionContext = await _gatewayService.GetExecutionContextAsync(
            run.TestSuiteId, selectedTestCaseIds, ct);

        var environment = await _runRepository.UnitOfWork.ExecuteInTransactionAsync(
            async _ => await _envRepository.FirstOrDefaultAsync(
                _envRepository.GetQueryableSet().Where(x => x.Id == run.EnvironmentId)),
            cancellationToken: ct);

        var resolvedEnv = NormalizeEnvironmentBaseUrlForMultiResourceSuite(
            await _envResolver.ResolveAsync(environment, ct),
            executionContext.OrderedTestCases);

        var retentionDays = (await _limitService.CheckLimitAsync(
            currentUserId, LimitType.RetentionDays, 0, ct)).LimitValue ?? 7;

        var endpointMetadataMap = new Dictionary<Guid, ApiEndpointMetadataDto>();
        if (executionContext.Suite.ApiSpecId.HasValue && executionContext.OrderedEndpointIds.Count > 0)
        {
            endpointMetadataMap = (await _endpointMetadataService.GetEndpointMetadataAsync(
                executionContext.Suite.ApiSpecId.Value, executionContext.OrderedEndpointIds, ct))
                .ToDictionary(m => m.EndpointId);
        }

        run.Status = TestRunStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        run.TotalTests = executionContext.OrderedTestCases.Count;
        await _runRepository.UpdateAsync(run, ct);
        await _runRepository.UnitOfWork.SaveChangesAsync(ct);

        var effectivePolicy = retryPolicy ?? new TestRunRetryPolicyModel();
        var variableBag = new Dictionary<string, string>(resolvedEnv.Variables, StringComparer.OrdinalIgnoreCase);
        var context = new ExecutionContextState(
            run.Id, effectivePolicy, strictValidation, validationProfile, resolvedEnv, variableBag, endpointMetadataMap);

        _logger.LogInformation(
            "Test run started. RunId={RunId}, TotalCases={TotalCases}, Policy={Policy}",
            run.Id, executionContext.OrderedTestCases.Count, effectivePolicy);

        foreach (var testCase in executionContext.OrderedTestCases)
        {
            await ExecuteAndTrackAsync(testCase, context, ct);

            if (effectivePolicy.RerunSkippedCases)
            {
                await ReplayEligibleSkippedCasesAsync(executionContext.OrderedTestCases, context, ct);
            }
        }

        var orderedResults = context.CaseResults
            .OrderBy(x => x.OrderIndex)
            .ThenBy(x => x.ExecutionAttempt)
            .ToList();

        return await _resultCollector.CollectAsync(
            run,
            orderedResults,
            retentionDays,
            resolvedEnv.Name,
            ct,
            context.Attempts);
    }

    private async Task ExecuteAndTrackAsync(
        ExecutionTestCaseDto testCase,
        ExecutionContextState context,
        CancellationToken ct)
    {
        var attemptNumber = context.NextAttemptNumber(testCase.TestCaseId);
        var attemptId = Guid.NewGuid();
        var parentAttemptId = context.LastAttemptIdByCase.TryGetValue(testCase.TestCaseId, out var existingParent)
            ? existingParent
            : (Guid?)null;

        var failedDepIds = AnalyzeDependencies(testCase, context);
        var retryReason = failedDepIds.Count > 0 ? "Dependency failed" : null;
        var skippedCause = failedDepIds.Count > 0
            ? SummarizeDependencyRootCause(failedDepIds, context.CaseResultMap)
            : null;

        var startedAt = DateTimeOffset.UtcNow;

        TestCaseExecutionResult result;
        if (failedDepIds.Count > 0)
        {
            result = BuildSkippedResult(testCase, attemptNumber, failedDepIds, skippedCause);
        }
        else
        {
            result = await ExecuteSuccessfulPathAsync(testCase, context, ct, attemptNumber);
        }

        result.ExecutionAttempt = attemptNumber;
        var completedAt = DateTimeOffset.UtcNow;

        var attempt = BuildAttemptModel(
            attemptId,
            parentAttemptId,
            context.RunId,
            testCase.TestCaseId,
            attemptNumber,
            retryReason,
            skippedCause,
            failedDepIds,
            new List<Guid>(),
            context.RetryPolicy,
            result,
            startedAt,
            completedAt);

        context.RecordAttempt(attempt);
        context.RecordResult(result, attemptId);
        LogCaseOutcome(context.RunId, result, attemptId, parentAttemptId, retryReason, skippedCause, failedDepIds, null);

        // Retry loop — supports MaxRetryAttempts > 1.
        // Each iteration reads the latest result from the context so the while-condition
        // reflects the outcome of the most recent attempt, not the original one.
        var lastRetryAttemptId = attemptId;
        while (context.RetryPolicy.EnableRetry
            && context.CaseResultMap.TryGetValue(testCase.TestCaseId, out var latestResult)
            && ShouldRetryCase(latestResult, context.RetryPolicy)
            && context.PolicyAllowsRetry(
                context.AttemptCounters.GetValueOrDefault(testCase.TestCaseId, attemptNumber)))
        {
            lastRetryAttemptId = await RetryCaseAsync(testCase, context, ct, lastRetryAttemptId);
        }
    }

    /// <summary>
    /// Executes one retry attempt for <paramref name="testCase"/> and returns the new attempt ID.
    /// The caller must hold the retry-budget check before calling this method.
    /// </summary>
    private async Task<Guid> RetryCaseAsync(
        ExecutionTestCaseDto testCase,
        ExecutionContextState context,
        CancellationToken ct,
        Guid parentAttemptId)
    {
        // Capture attempt number once to avoid double-increment.
        var retryAttemptNumber = context.NextAttemptNumber(testCase.TestCaseId);
        var retryAttemptId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var retryResult = await ExecuteSuccessfulPathAsync(testCase, context, ct, retryAttemptNumber);
        retryResult.ExecutionAttempt = retryAttemptNumber;
        var completedAt = DateTimeOffset.UtcNow;

        var reason = $"Retry after failure (attempt {retryAttemptNumber})";
        var attempt = BuildAttemptModel(
            retryAttemptId,
            parentAttemptId,
            context.RunId,
            testCase.TestCaseId,
            retryAttemptNumber,
            reason,
            null,
            Array.Empty<Guid>(),
            new List<Guid>(),
            context.RetryPolicy,
            retryResult,
            startedAt,
            completedAt);

        context.RecordAttempt(attempt);
        context.RecordResult(retryResult, retryAttemptId);
        LogCaseOutcome(context.RunId, retryResult, retryAttemptId, parentAttemptId, reason, null, Array.Empty<Guid>(), null);
        return retryAttemptId;
    }

    private async Task ReplayEligibleSkippedCasesAsync(
        IReadOnlyCollection<ExecutionTestCaseDto> orderedCases,
        ExecutionContextState context,
        CancellationToken ct)
    {
        // Cascading-replay loop: after replaying a case its result enters CaseResultMap,
        // which may unlock further downstream skipped cases.  We keep sweeping until no
        // new cases become runnable.  The bound (orderedCases.Count + 1) prevents any
        // hypothetical infinite loop.
        bool anyReplayed;
        var maxPasses = orderedCases.Count + 1;
        var passCount = 0;
        do
        {
            anyReplayed = false;
            passCount++;
            var replayables = orderedCases
                .Where(tc => context.IsSkippedAndNowRunnable(tc.TestCaseId))
                .ToList();

            foreach (var testCase in replayables)
            {
                // Defensive re-check: the replayables snapshot was built before this pass began.
                // A case executed earlier in the same pass may have updated CaseResultMap in a way
                // that makes a dependency of the current case no longer satisfied (e.g. a shared
                // dep that was just replayed and failed with a hard error).  Rather than executing
                // with stale dependency state, defer to the next do-while pass where
                // IsSkippedAndNowRunnable will be re-evaluated against fresh CaseResultMap state.
                //
                // Note: under current IsDependencySatisfied constraints this guard will never
                // actually defer a case, but it is kept as an explicit safety net for future
                // changes to the satisfaction logic.
                if (AnalyzeDependencies(testCase, context).Count > 0)
                {
                    continue;
                }

                var parentAttemptId = context.LastAttemptIdByCase.TryGetValue(testCase.TestCaseId, out var parent)
                    ? parent
                    : (Guid?)null;

                // Capture the dependency IDs that originally caused the skip,
                // so we can store them as ReplayedSkippedCaseIds on the replay attempt.
                var originalSkipDeps = context.CaseResultMap.TryGetValue(testCase.TestCaseId, out var originalResult)
                    ? (originalResult.SkippedBecauseDependencyIds ?? new List<Guid>())
                    : new List<Guid>();

                var replayAttemptNumber = context.NextAttemptNumber(testCase.TestCaseId);
                var replayAttemptId = Guid.NewGuid();
                var startedAt = DateTimeOffset.UtcNow;

                const string replayReason = "Replayed skipped case after dependency recovery";
                var replay = await ExecuteSuccessfulPathAsync(testCase, context, ct, replayAttemptNumber);
                replay.ExecutionAttempt = replayAttemptNumber;
                var completedAt = DateTimeOffset.UtcNow;

                var attempt = BuildAttemptModel(
                    replayAttemptId,
                    parentAttemptId,
                    context.RunId,
                    testCase.TestCaseId,
                    replayAttemptNumber,
                    replayReason,
                    null,
                    Array.Empty<Guid>(),
                    originalSkipDeps,
                    context.RetryPolicy,
                    replay,
                    startedAt,
                    completedAt);

                context.RecordAttempt(attempt);
                context.RecordResult(replay, replayAttemptId);
                LogCaseOutcome(context.RunId, replay, replayAttemptId, parentAttemptId, replayReason, null, Array.Empty<Guid>(), originalSkipDeps);
                anyReplayed = true;
            }
        }
        while (anyReplayed && passCount < maxPasses);
    }

    private static TestCaseExecutionAttemptModel BuildAttemptModel(
        Guid attemptId,
        Guid? parentAttemptId,
        Guid runId,
        Guid testCaseId,
        int attemptNumber,
        string retryReason,
        string skippedCause,
        IEnumerable<Guid> dependencyRootCauseIds,
        IEnumerable<Guid> replayedSkippedCaseIds,
        TestRunRetryPolicyModel retryPolicy,
        TestCaseExecutionResult result,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        return new TestCaseExecutionAttemptModel
        {
            ExecutionAttemptId = attemptId,
            ParentAttemptId = parentAttemptId,
            TestRunId = runId,
            TestCaseId = testCaseId,
            AttemptNumber = attemptNumber,
            RetryReason = retryReason,
            SkippedCause = skippedCause,
            DependencyRootCause = skippedCause,
            DependencyRootCauseIds = (dependencyRootCauseIds ?? Array.Empty<Guid>()).ToList(),
            ReplayedSkippedCaseIds = (replayedSkippedCaseIds ?? Array.Empty<Guid>()).ToList(),
            RetryPolicy = retryPolicy?.Clone() ?? new TestRunRetryPolicyModel(),
            Status = result?.Status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = (long)(completedAt - startedAt).TotalMilliseconds,
            FailureReasons = result?.FailureReasons ?? new List<ValidationFailureModel>(),
        };
    }

    private async Task<TestCaseExecutionResult> ExecuteSuccessfulPathAsync(
        ExecutionTestCaseDto testCase,
        ExecutionContextState context,
        CancellationToken ct,
        int attempt)
    {
        context.EndpointMetadataMap.TryGetValue(testCase.EndpointId ?? Guid.Empty, out var endpointMetadata);

        if (RequestBodyAutoHydrator.TryHydrate(testCase, endpointMetadata))
        {
            _logger.LogInformation(
                "Auto-hydrated request body. RunId={RunId}, TestCaseId={TestCaseId}",
                context.RunId, testCase.TestCaseId);
        }

        var preValidation = _preValidator.Validate(testCase, context.ResolvedEnv, context.VariableBag, endpointMetadata);
        if (preValidation.HasErrors)
        {
            return new TestCaseExecutionResult
            {
                TestCaseId = testCase.TestCaseId,
                EndpointId = testCase.EndpointId,
                Name = testCase.Name,
                Description = testCase.Description,
                TestType = testCase.TestType,
                OrderIndex = testCase.OrderIndex,
                Status = "Failed",
                ExecutionAttempt = attempt,
                DependencyIds = testCase.DependencyIds,
                FailureReasons = preValidation.ToFailureReasons(),
                Warnings = preValidation.Warnings,
                ExpectationSource = testCase.Expectation?.ExpectationSource,
                RequirementCode = testCase.Expectation?.RequirementCode,
                PrimaryRequirementId = testCase.Expectation?.PrimaryRequirementId,
            };
        }

        ResolvedTestCaseRequest resolvedRequest;
        try
        {
            resolvedRequest = _variableResolver.Resolve(testCase, context.VariableBag, context.ResolvedEnv);
        }
        catch (UnresolvedVariableException ex)
        {
            return new TestCaseExecutionResult
            {
                TestCaseId = testCase.TestCaseId,
                EndpointId = testCase.EndpointId,
                Name = testCase.Name,
                Description = testCase.Description,
                OrderIndex = testCase.OrderIndex,
                Status = "Failed",
                ExecutionAttempt = attempt,
                DependencyIds = testCase.DependencyIds,
                FailureReasons = new List<ValidationFailureModel>
                {
                    new ValidationFailureModel { Code = "UNRESOLVED_VARIABLE", Message = ex.Message },
                },
                ExpectationSource = testCase.Expectation?.ExpectationSource,
                RequirementCode = testCase.Expectation?.RequirementCode,
                PrimaryRequirementId = testCase.Expectation?.PrimaryRequirementId,
            };
        }

        var response = await _httpExecutor.ExecuteAsync(resolvedRequest, ct);

        var extracted = (_variableExtractor.Extract(
            response,
            testCase.Variables ?? Array.Empty<ExecutionVariableRuleDto>(),
            resolvedRequest.Body)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in ExtractImplicitVariables(testCase, response))
        {
            context.VariableBag[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in ExtractResponseBodyVariables(testCase, response))
        {
            context.VariableBag[kvp.Key] = kvp.Value;
        }

        PromoteAuthTokenAliases(extracted);
        foreach (var kvp in extracted)
        {
            context.VariableBag[kvp.Key] = kvp.Value;
        }

        var validation = _validator.Validate(response, testCase, endpointMetadata, context.ValidationProfile);

        return new TestCaseExecutionResult
        {
            TestCaseId = testCase.TestCaseId,
            EndpointId = testCase.EndpointId,
            Name = testCase.Name,
            Description = testCase.Description,
            TestType = testCase.TestType,
            OrderIndex = testCase.OrderIndex,
            Status = validation.IsPassed ? "Passed" : "Failed",
            ExecutionAttempt = attempt,
            HttpStatusCode = response.StatusCode,
            DurationMs = response.LatencyMs,
            ResolvedUrl = resolvedRequest.ResolvedUrl,
            HttpMethod = resolvedRequest.HttpMethod,
            BodyType = resolvedRequest.BodyType,
            RequestBody = resolvedRequest.Body,
            QueryParams = resolvedRequest.QueryParams,
            TimeoutMs = resolvedRequest.TimeoutMs,
            ExpectedStatus = testCase.Expectation?.ExpectedStatus,
            ExpectedBodyContains = testCase.Expectation?.BodyContains,
            ExpectedBodyNotContains = testCase.Expectation?.BodyNotContains,
            ExpectedHeaderChecks = testCase.Expectation?.HeaderChecks,
            ExpectedJsonPathChecks = testCase.Expectation?.JsonPathChecks,
            ExpectedMaxResponseTime = testCase.Expectation?.MaxResponseTime,
            ExpectationSource = testCase.Expectation?.ExpectationSource,
            RequirementCode = testCase.Expectation?.RequirementCode,
            PrimaryRequirementId = testCase.Expectation?.PrimaryRequirementId,
            RequestHeaders = resolvedRequest.Headers,
            ResponseHeaders = response.Headers,
            ResponseBody = response.Body,
            FailureReasons = validation.Failures?.ToList() ?? new List<ValidationFailureModel>(),
            Warnings = MergeWarnings(preValidation.Warnings, validation.Warnings),
            ChecksPerformed = validation.ChecksPerformed,
            ChecksSkipped = validation.ChecksSkipped,
            ExtractedVariables = extracted,
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

    private IReadOnlyList<Guid> AnalyzeDependencies(ExecutionTestCaseDto testCase, ExecutionContextState context)
    {
        return (testCase.DependencyIds ?? Array.Empty<Guid>())
            .Where(depId =>
                context.CaseResultMap.TryGetValue(depId, out var depResult)
                && !DependencySatisfactionPolicy.IsSatisfied(depResult))
            .ToList();
    }

    /// <summary>
    /// A case should be retried ONLY when it failed with a transient HTTP error:
    /// <list type="bullet">
    ///   <item>HTTP 408 (Request Timeout) or HTTP 429 (Too Many Requests)</item>
    ///   <item>HTTP 5xx (server-side errors)</item>
    ///   <item><c>HTTP_REQUEST_ERROR</c> — transport failure before any response was received</item>
    /// </list>
    /// Deterministic failures (4xx assertion mismatches, schema mismatches, pre-validation errors,
    /// unresolved variables) are NOT retried — they will produce the same result on every attempt.
    /// </summary>
    private static bool ShouldRetryCase(TestCaseExecutionResult result, TestRunRetryPolicyModel retryPolicy)
    {
        if (retryPolicy == null || retryPolicy.MaxRetryAttempts <= 0)
        {
            return false;
        }

        if (!string.Equals(result?.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Transport error (no HTTP response at all) — always transient
        var hasTransportError = result.FailureReasons?.Any(f =>
            string.Equals(f?.Code, "HTTP_REQUEST_ERROR", StringComparison.OrdinalIgnoreCase)) == true;

        if (hasTransportError)
        {
            return true;
        }

        // HTTP 408 / 429 — timeout or rate limit
        if (result.HttpStatusCode is 408 or 429)
        {
            return true;
        }

        // HTTP 5xx — server-side error
        if (result.HttpStatusCode is >= 500 and < 600)
        {
            return true;
        }

        return false;
    }

    private static TestCaseExecutionResult BuildSkippedResult(
        ExecutionTestCaseDto testCase,
        int attempt,
        IReadOnlyList<Guid> failedDeps,
        string rootCause)
    {
        var skipMessage = $"Test case skipped because dependency failed. RootCause={rootCause}";
        return new TestCaseExecutionResult
        {
            TestCaseId = testCase.TestCaseId,
            EndpointId = testCase.EndpointId,
            Name = testCase.Name,
            Description = testCase.Description,
            TestType = testCase.TestType,
            OrderIndex = testCase.OrderIndex,
            Status = "Skipped",
            ExecutionAttempt = attempt,
            DependencyIds = testCase.DependencyIds,
            SkippedBecauseDependencyIds = failedDeps.ToList(),
            SkippedCause = skipMessage,
            FailureReasons = new List<ValidationFailureModel>
            {
                new ValidationFailureModel
                {
                    Code = "DEPENDENCY_FAILED",
                    Message = skipMessage,
                },
            },
            ExpectationSource = testCase.Expectation?.ExpectationSource,
            RequirementCode = testCase.Expectation?.RequirementCode,
            PrimaryRequirementId = testCase.Expectation?.PrimaryRequirementId,
        };
    }

    private void LogCaseOutcome(
        Guid runId,
        TestCaseExecutionResult result,
        Guid executionAttemptId,
        Guid? parentAttemptId,
        string retryReason,
        string skippedCause,
        IEnumerable<Guid> dependencyRootCauseIds,
        IEnumerable<Guid> replayedSkippedCaseIds)
    {
        _logger.LogInformation(
            "Case outcome. RunId={RunId}, TestCaseId={TestCaseId}, AttemptId={AttemptId}, "
            + "ParentAttemptId={ParentAttemptId}, AttemptNumber={AttemptNumber}, "
            + "RetryReason={RetryReason}, SkippedCause={SkippedCause}, "
            + "DependencyRootCauseIds={DependencyRootCauseIds}, "
            + "ReplayedSkippedCaseIds={ReplayedSkippedCaseIds}, "
            + "Status={Status}, DependencyIds={DependencyIds}, "
            + "SkippedBecauseDependencyIds={SkippedBecauseDependencyIds}, "
            + "FailureCodes={FailureCodes}, FailureDetails={FailureDetails}",
            runId,
            result.TestCaseId,
            executionAttemptId,
            parentAttemptId?.ToString() ?? "(none)",
            result.ExecutionAttempt,
            retryReason ?? "(none)",
            skippedCause ?? "(none)",
            SummarizeGuidList(dependencyRootCauseIds),
            SummarizeGuidList(replayedSkippedCaseIds),
            result.Status,
            SummarizeGuidList(result.DependencyIds),
            SummarizeGuidList(result.SkippedBecauseDependencyIds),
            SummarizeFailureCodes(result.FailureReasons),
            SummarizeFailureDetails(result.FailureReasons));
    }

    private static string SummarizeDependencyRootCause(
        IReadOnlyList<Guid> failedDependencyIds,
        IReadOnlyDictionary<Guid, TestCaseExecutionResult> caseResultMap)
    {
        if (failedDependencyIds == null || failedDependencyIds.Count == 0)
        {
            return "(none)";
        }

        return string.Join(" || ", failedDependencyIds.Take(3).Select(id =>
            caseResultMap != null && caseResultMap.TryGetValue(id, out var r)
                ? $"{r.Name} ({id}) => Status={r.Status}, HttpStatus={r.HttpStatusCode?.ToString() ?? "(null)"}, FailureCodes={SummarizeFailureCodes(r.FailureReasons)}, FailureDetails={SummarizeFailureDetails(r.FailureReasons)}"
                : $"{id} => (result unavailable)"));
    }

    private static string SummarizeGuidList(IEnumerable<Guid> values)
    {
        if (values == null)
        {
            return "[]";
        }

        var list = values.Where(x => x != Guid.Empty).Distinct().ToList();
        return list.Count == 0
            ? "[]"
            : $"[{string.Join(", ", list.Take(5))}{(list.Count > 5 ? $", +{list.Count - 5} more" : string.Empty)}]";
    }

    private static string SummarizeFailureCodes(IEnumerable<ValidationFailureModel> failures)
    {
        if (failures == null)
        {
            return "(none)";
        }

        return string.Join(", ", failures
            .Select(x => x?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5));
    }

    private static string SummarizeFailureDetails(IEnumerable<ValidationFailureModel> failures)
    {
        if (failures == null)
        {
            return "(none)";
        }

        return string.Join(" | ", failures
            .Select(f => f == null ? null : $"{f.Code ?? "UNKNOWN"}: {f.Message ?? string.Empty}")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(3));
    }

    private static List<ValidationWarningModel> MergeWarnings(
        List<ValidationWarningModel> pre,
        IReadOnlyList<ValidationWarningModel> post)
    {
        return (pre ?? new List<ValidationWarningModel>())
            .Concat(post ?? Array.Empty<ValidationWarningModel>())
            .ToList();
    }

    /// <summary>
    /// Holds mutable state for a single test run execution — results, attempt records,
    /// attempt counters, and the shared variable bag.
    /// </summary>
    private sealed class ExecutionContextState
    {
        private static bool IsDependencySatisfied(TestCaseExecutionResult dependencyResult)
            => DependencySatisfactionPolicy.IsSatisfied(dependencyResult);

        public ExecutionContextState(
            Guid runId,
            TestRunRetryPolicyModel retryPolicy,
            bool strictValidation,
            ValidationProfile validationProfile,
            ResolvedExecutionEnvironment resolvedEnv,
            Dictionary<string, string> variableBag,
            Dictionary<Guid, ApiEndpointMetadataDto> endpointMetadataMap)
        {
            RunId = runId;
            RetryPolicy = retryPolicy ?? new TestRunRetryPolicyModel();
            StrictValidation = strictValidation;
            ValidationProfile = validationProfile;
            ResolvedEnv = resolvedEnv;
            VariableBag = variableBag;
            EndpointMetadataMap = endpointMetadataMap;
        }

        public Guid RunId { get; }

        public TestRunRetryPolicyModel RetryPolicy { get; }

        public bool StrictValidation { get; }

        public ValidationProfile ValidationProfile { get; }

        public ResolvedExecutionEnvironment ResolvedEnv { get; }

        public Dictionary<string, string> VariableBag { get; }

        public Dictionary<Guid, ApiEndpointMetadataDto> EndpointMetadataMap { get; }

        public Dictionary<Guid, TestCaseExecutionResult> CaseResultMap { get; }
            = new Dictionary<Guid, TestCaseExecutionResult>();

        public List<TestCaseExecutionResult> CaseResults { get; }
            = new List<TestCaseExecutionResult>();

        public List<TestCaseExecutionAttemptModel> Attempts { get; }
            = new List<TestCaseExecutionAttemptModel>();

        public Dictionary<Guid, int> AttemptCounters { get; }
            = new Dictionary<Guid, int>();

        public Dictionary<Guid, Guid> LastAttemptIdByCase { get; }
            = new Dictionary<Guid, Guid>();

        /// <summary>
        /// Increments and returns the next attempt number for the given test case.
        /// Must be called exactly once per attempt to avoid counter drift.
        /// </summary>
        public int NextAttemptNumber(Guid caseId)
        {
            if (!AttemptCounters.TryGetValue(caseId, out var current))
            {
                AttemptCounters[caseId] = 1;
                return 1;
            }

            AttemptCounters[caseId] = current + 1;
            return AttemptCounters[caseId];
        }

        /// <summary>
        /// Returns <c>true</c> when another retry is permitted.
        /// <paramref name="attemptNumber"/> is the number of the attempt that just completed;
        /// if it is still within the allowed retry budget the next retry can proceed.
        /// Example: MaxRetryAttempts=2 allows attempts 1→2→3 (two retries total).
        /// </summary>
        public bool PolicyAllowsRetry(int attemptNumber) =>
            attemptNumber <= (RetryPolicy?.MaxRetryAttempts ?? 0);

        public void RecordResult(TestCaseExecutionResult result, Guid attemptId)
        {
            CaseResultMap[result.TestCaseId] = result;

            // Keep only the latest result per test case — all previous entries for this
            // case are removed so the summary always shows the final-attempt outcome.
            CaseResults.RemoveAll(x => x.TestCaseId == result.TestCaseId);
            CaseResults.Add(result);
            LastAttemptIdByCase[result.TestCaseId] = attemptId;
        }

        public void RecordAttempt(TestCaseExecutionAttemptModel attempt) => Attempts.Add(attempt);

        /// <summary>
        /// Returns <c>true</c> when the test case was previously skipped due to failed dependencies
        /// and ALL of those dependencies now have a satisfied result.
        /// </summary>
        public bool IsSkippedAndNowRunnable(Guid caseId)
        {
            if (!CaseResultMap.TryGetValue(caseId, out var result))
            {
                return false;
            }

            if (!string.Equals(result.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var deps = result.DependencyIds ?? Array.Empty<Guid>();
            return deps.All(depId =>
                CaseResultMap.TryGetValue(depId, out var dep) && IsDependencySatisfied(dep));
        }
    }

    // Variable extraction helpers — preserved from original implementation.
    private static Dictionary<string, string> ExtractImplicitVariables(
        ExecutionTestCaseDto testCase,
        HttpTestResponse response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response == null)
        {
            return result;
        }

        using var bodyDocument = TryParseJson(response.Body);

        if (response.StatusCode is >= 200 and < 300
            && TryExtractIdentifier(response, bodyDocument, out var identifierValue))
        {
            var resourceVariableName = BuildResourceIdVariableName(testCase?.Request?.Url);
            if (!string.IsNullOrWhiteSpace(resourceVariableName))
            {
                result[resourceVariableName] = identifierValue;
            }

            result["id"] = identifierValue;
        }

        if (response.StatusCode is >= 200 and < 300
            && TryExtractToken(response, bodyDocument, allowTextFallback: true, out var tokenValue))
        {
            result["authToken"] = tokenValue;
            result["accessToken"] = tokenValue;
        }

        return result;
    }

    private static Dictionary<string, string> ExtractResponseBodyVariables(
        ExecutionTestCaseDto testCase,
        HttpTestResponse response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (response == null || response.StatusCode < 200 || response.StatusCode >= 300)
        {
            return result;
        }

        using var bodyDocument = TryParseJson(response.Body);
        if (bodyDocument == null)
        {
            return result;
        }

        TryExtractObjectValues(bodyDocument.RootElement, result);

        var resourceVariableName = BuildResourceIdVariableName(testCase?.Request?.Url);
        if (TryExtractIdentifier(response, bodyDocument, out var identifierValue)
            && !string.IsNullOrWhiteSpace(resourceVariableName))
        {
            result[resourceVariableName] = identifierValue;
        }

        return result;
    }

    private static bool TryExtractObjectValues(
        JsonElement element,
        IDictionary<string, string> result,
        string prefix = null)
    {
        if (result == null)
        {
            return false;
        }

        var extractedAny = false;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}{property.Name}";

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    extractedAny |= TryExtractObjectValues(property.Value, result, key + ".");
                }
                else
                {
                    var value = property.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(value) && !result.ContainsKey(key))
                    {
                        result[key] = value;
                        extractedAny = true;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                extractedAny |= TryExtractObjectValues(item, result, $"{prefix}[{index}].");
                index++;
            }
        }

        return extractedAny;
    }

    private static JsonDocument TryParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryExtractIdentifier(
        HttpTestResponse response,
        JsonDocument bodyDocument,
        out string identifier)
    {
        identifier = null;

        if (TryExtractIdFromLocationHeader(response?.Headers, out identifier))
        {
            return true;
        }

        if (bodyDocument != null)
        {
            foreach (var path in new[]
            {
                "$.data._id", "$.data.id", "$.data.userId", "$.data.user.id", "$.data.user._id",
                "$.user._id", "$.user.id", "$._id", "$.id", "$.userId",
                "$.data.data._id", "$.data.data.id",
            })
            {
                var element = VariableExtractor.NavigateJsonPath(bodyDocument.RootElement, path);
                var value = element?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    identifier = value;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryExtractIdFromLocationHeader(
        IReadOnlyDictionary<string, string> headers,
        out string identifier)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(header.Value))
                {
                    var parsed = Uri.TryCreate(header.Value.Trim(), UriKind.Absolute, out var absolute)
                        ? absolute.AbsolutePath
                        : header.Value.Trim();

                    var segment = parsed
                        .TrimEnd('/')
                        .Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .LastOrDefault();

                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        identifier = segment;
                        return true;
                    }
                }
            }
        }

        identifier = null;
        return false;
    }

#pragma warning disable IDE0060 // unused parameters kept for future token extraction logic
    private static bool TryExtractToken(
        HttpTestResponse response,
        JsonDocument bodyDocument,
        bool allowTextFallback,
        out string token)
    {
        token = null;
        return false;
    }
#pragma warning restore IDE0060

    private static string BuildResourceIdVariableName(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            return null;
        }

        // Strip query/fragment, then parse path-only
        var queryIndex = urlOrPath.IndexOfAny(new[] { '?', '#' });
        var path = queryIndex >= 0 ? urlOrPath[..queryIndex] : urlOrPath;
        path = path.TrimEnd('/');

        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            path = absolute.AbsolutePath.TrimEnd('/');
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Walk backwards to find the last meaningful resource segment
        // (skip path params like {id}, version segments like v1, and "api")
        string resourceSegment = null;
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var seg = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(seg))
            {
                continue;
            }

            if (seg.StartsWith('{') && seg.EndsWith('}'))
            {
                continue;
            }

            if (string.Equals(seg, "api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip version segments: v1, v2, v10
            if (seg.Length <= 3 && seg.StartsWith('v') && seg.Skip(1).All(char.IsDigit))
            {
                continue;
            }

            resourceSegment = seg;
            break;
        }

        if (string.IsNullOrWhiteSpace(resourceSegment))
        {
            return null;
        }

        var singular = SingularizeResourceSegment(resourceSegment);
        var camel = ToCamelCase(singular);
        return string.IsNullOrWhiteSpace(camel) ? null : camel + "Id";
    }

    /// <summary>Converts a plural resource segment to singular (e.g. "categories" → "category").</summary>
    private static string SingularizeResourceSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
        {
            return value[..^3] + "y";
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            && value.Length > 1)
        {
            return value[..^1];
        }

        return value;
    }

    /// <summary>Converts a kebab/snake/plain segment to camelCase (e.g. "order-item" → "orderItem").</summary>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return value;
        }

        var first = parts[0].Length == 0
            ? string.Empty
            : char.ToLowerInvariant(parts[0][0]) + parts[0][1..];
        var rest = parts.Skip(1)
            .Select(p => p.Length == 0 ? string.Empty : char.ToUpperInvariant(p[0]) + p[1..]);
        return first + string.Concat(rest);
    }

    private static void PromoteAuthTokenAliases(Dictionary<string, string> extracted)
    {
    }

    // Intentionally empty — host/port normalization is handled by the runtime resolver.
    // Preserved as extension hook for future multi-resource environments.
    private static ResolvedExecutionEnvironment NormalizeEnvironmentBaseUrlForMultiResourceSuite(
        ResolvedExecutionEnvironment env,
        IReadOnlyCollection<ExecutionTestCaseDto> orderedTestCases)
        => env;
}
