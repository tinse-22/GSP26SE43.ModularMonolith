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
using System.Text.RegularExpressions;
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
        resolvedEnv = NormalizeEnvironmentBaseUrlForMultiResourceSuite(resolvedEnv, executionContext.OrderedTestCases);

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
        var caseResultMap = new Dictionary<Guid, TestCaseExecutionResult>();

        foreach (var testCase in executionContext.OrderedTestCases)
        {
            var result = await ExecuteTestCase(
                testCase, resolvedEnv, variableBag, caseStatusMap, caseResultMap, endpointMetadataMap, ct, strictValidation);

            caseResults.Add(result);
            caseStatusMap[testCase.TestCaseId] = result.Status;
            caseResultMap[testCase.TestCaseId] = result;

            LogCaseOutcome(run.Id, result);
        }

        // Collect results
        return await _resultCollector.CollectAsync(run, caseResults, retentionDays, resolvedEnv.Name, ct);
    }

    private async Task<TestCaseExecutionResult> ExecuteTestCase(
        ExecutionTestCaseDto testCase,
        ResolvedExecutionEnvironment resolvedEnv,
        Dictionary<string, string> variableBag,
        Dictionary<Guid, string> caseStatusMap,
        Dictionary<Guid, TestCaseExecutionResult> caseResultMap,
        Dictionary<Guid, ApiEndpointMetadataDto> endpointMetadataMap,
        CancellationToken ct,
        bool strictValidation)
    {
        // Check dependencies
        var failedDeps = testCase.DependencyIds
            .Where(depId =>
            {
                if (caseResultMap.TryGetValue(depId, out var dependencyResult))
                {
                    return !IsDependencySatisfied(dependencyResult);
                }

                if (caseStatusMap.TryGetValue(depId, out var status))
                {
                    return !string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            })
            .ToList();

        if (failedDeps.Count > 0)
        {
            _logger.LogWarning(
                "Dependency skip. TestCase={TestCaseName} ({TestCaseId}), Dependencies={DependencyIds}, FailedDependencies={FailedDependencyIds}, FailedDependencyDetails={FailedDependencyDetails}",
                testCase.Name,
                testCase.TestCaseId,
                SummarizeGuidList(testCase.DependencyIds),
                SummarizeGuidList(failedDeps),
                SummarizeFailedDependencyDetails(failedDeps, caseResultMap));

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

        if (RequestBodyAutoHydrator.TryHydrate(testCase, endpointMetadata))
        {
            _logger.LogInformation(
                "Auto-hydrated request body from endpoint schema. TestCase={TestCaseName} ({TestCaseId}), EndpointId={EndpointId}, BodyLength={BodyLength}",
                testCase.Name,
                testCase.TestCaseId,
                testCase.EndpointId,
                testCase.Request?.Body?.Length ?? 0);
        }

        var preValidation = _preValidator.Validate(testCase, resolvedEnv, variableBag, endpointMetadata);

        if (preValidation.HasErrors)
        {
            _logger.LogWarning(
                "Pre-execution validation failed. TestCase={TestCaseName} ({TestCaseId}), Errors={ErrorCount}, FailureCodes={FailureCodes}, FailureDetails={FailureDetails}, HttpMethod={HttpMethod}, Url={Url}, BodyLength={BodyLength}, PathParams={PathParams}, QueryParams={QueryParams}",
                testCase.Name,
                testCase.TestCaseId,
                preValidation.Errors.Count,
                SummarizeFailureCodes(preValidation.Errors),
                SummarizeFailureDetails(preValidation.Errors),
                testCase.Request?.HttpMethod ?? "(null)",
                testCase.Request?.Url ?? "(null)",
                testCase.Request?.Body?.Length ?? 0,
                Truncate(testCase.Request?.PathParams, 256),
                Truncate(testCase.Request?.QueryParams, 256));

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
            _logger.LogWarning(
                "Variable resolution failed. TestCase={TestCaseName} ({TestCaseId}), HttpMethod={HttpMethod}, Url={Url}, Reason={Reason}",
                testCase.Name,
                testCase.TestCaseId,
                testCase.Request?.HttpMethod ?? "(null)",
                testCase.Request?.Url ?? "(null)",
                ex.Message);

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
        var extracted = _variableExtractor
            .Extract(response, testCase.Variables, resolvedRequest.Body)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var implicitExtracted = ExtractImplicitVariables(testCase, response);
        foreach (var kvp in implicitExtracted)
        {
            if (!extracted.ContainsKey(kvp.Key))
            {
                extracted[kvp.Key] = kvp.Value;
            }
        }

        var currentResourceIdVariableName = BuildResourceIdVariableName(testCase?.Request?.Url);
        foreach (var kvp in extracted.ToList())
        {
            if (ShouldKeepExistingIdentifierVariable(variableBag, kvp.Key, currentResourceIdVariableName))
            {
                // Avoid corrupting previously extracted foreign-resource IDs
                // (e.g., categoryId from create-category being overwritten by create-product _id).
                extracted.Remove(kvp.Key);
                continue;
            }

            variableBag[kvp.Key] = kvp.Value;
        }

        // Validate response
        var validation = _validator.Validate(response, testCase, endpointMetadata, strictValidation);

        if (!validation.IsPassed)
        {
            _logger.LogWarning(
                "Response validation failed. TestCase={TestCaseName} ({TestCaseId}), HttpMethod={HttpMethod}, Url={ResolvedUrl}, HttpStatus={HttpStatus}, ExpectedStatus={ExpectedStatus}, FailureCodes={FailureCodes}, FailureDetails={FailureDetails}, ResponseContentType={ResponseContentType}",
                testCase.Name,
                testCase.TestCaseId,
                resolvedRequest.HttpMethod,
                resolvedRequest.ResolvedUrl,
                response.StatusCode,
                testCase.Expectation?.ExpectedStatus,
                SummarizeFailureCodes(validation.Failures),
                SummarizeFailureDetails(validation.Failures),
                response.ContentType);
        }

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

        if (response.StatusCode is >= 200 and < 300)
        {
            if (TryExtractIdentifier(response, bodyDocument, out var identifierValue))
            {
                var resourceVariableName = BuildResourceIdVariableName(testCase?.Request?.Url);
                if (!string.IsNullOrWhiteSpace(resourceVariableName))
                {
                    result[resourceVariableName] = identifierValue;
                }

                result["id"] = identifierValue;
            }

            if (IsAuthLikeRequest(testCase) && TryExtractToken(response, bodyDocument, out var tokenValue))
            {
                result["authToken"] = tokenValue;
                result["accessToken"] = tokenValue;
            }
        }

        return result;
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
        if (TryExtractIdFromLocationHeader(response?.Headers, out identifier))
        {
            return true;
        }

        if (bodyDocument != null)
        {
            var root = bodyDocument.RootElement;
            foreach (var path in new[] { "$.data._id", "$.data.id", "$._id", "$.id", "$.data.data._id", "$.data.data.id" })
            {
                var element = VariableExtractor.NavigateJsonPath(root, path);
                var value = element?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    identifier = value;
                    return true;
                }
            }
        }

        identifier = null;
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
                if (!header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(header.Value))
                {
                    continue;
                }

                var rawValue = header.Value.Trim();
                var parsed = Uri.TryCreate(rawValue, UriKind.Absolute, out var absolute)
                    ? absolute.AbsolutePath
                    : rawValue;

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

        identifier = null;
        return false;
    }

    private static bool TryExtractToken(
        HttpTestResponse response,
        JsonDocument bodyDocument,
        out string token)
    {
        token = null;

        if (response?.Headers != null)
        {
            foreach (var header in response.Headers)
            {
                if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(header.Value))
                {
                    continue;
                }

                var rawValue = header.Value.Trim();
                token = rawValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? rawValue[7..].Trim()
                    : rawValue;

                if (!string.IsNullOrWhiteSpace(token))
                {
                    return true;
                }
            }
        }

        if (bodyDocument != null)
        {
            var root = bodyDocument.RootElement;
            foreach (var path in new[] { "$.data.token", "$.token", "$.data.accessToken", "$.accessToken", "$.data.jwt", "$.jwt" })
            {
                var element = VariableExtractor.NavigateJsonPath(root, path);
                var value = element?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    token = value;
                    return true;
                }
            }

            // Some APIs return auth/session identifiers in a message field
            // instead of dedicated token properties.
            foreach (var messagePath in new[] { "$.message", "$.data.message", "$.result.message" })
            {
                var element = VariableExtractor.NavigateJsonPath(root, messagePath);
                var value = element?.ToString();
                if (TryExtractTokenFromText(value, out token))
                {
                    return true;
                }
            }
        }

        if (TryExtractTokenFromText(response?.Body, out token))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractTokenFromText(string value, out string token)
    {
        token = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var input = value.Trim().Trim('"');
        if (input.Length == 0)
        {
            return false;
        }

        var patterns = new[]
        {
            @"(?i)\b(?:access[_\s-]?token|auth[_\s-]?token|token|session(?:Id)?)\b\s*[:=]\s*(?<value>[A-Za-z0-9\-._~+/=]+)",
            @"(?i)\bBearer\s+(?<value>[A-Za-z0-9\-._~+/=]+)",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                continue;
            }

            var candidate = match.Groups["value"].Success
                ? match.Groups["value"].Value
                : match.Value;
            candidate = candidate?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                token = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsAuthLikeRequest(ExecutionTestCaseDto testCase)
    {
        var signature = $"{testCase?.Request?.HttpMethod} {testCase?.Request?.Url} {testCase?.Name}";
        return signature.Contains("/auth", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("login", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signin", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("sign-in", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("register", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signup", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/token", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildResourceIdVariableName(string urlOrPath)
    {
        var path = ExtractPath(urlOrPath);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment)
                || string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase)
                || IsVersionSegment(segment)
                || (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)))
            {
                continue;
            }

            var resourceName = ToCamelIdentifier(Singularize(segment));
            return string.IsNullOrWhiteSpace(resourceName) ? null : resourceName + "Id";
        }

        return null;
    }

    private static bool ShouldKeepExistingIdentifierVariable(
        IReadOnlyDictionary<string, string> variableBag,
        string variableName,
        string currentResourceIdVariableName)
    {
        if (variableBag == null
            || string.IsNullOrWhiteSpace(currentResourceIdVariableName)
            || string.IsNullOrWhiteSpace(variableName)
            || !variableBag.ContainsKey(variableName)
            || !IsIdentifierVariableName(variableName)
            || string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(variableName, currentResourceIdVariableName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdentifierVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        return string.Equals(variableName, "id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || variableName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPath(string urlOrPath)
    {
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            return absolute.AbsolutePath;
        }

        return urlOrPath ?? string.Empty;
    }

    private static bool IsVersionSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment.Length <= 3
            && segment.StartsWith('v')
            && segment.Skip(1).All(char.IsDigit);
    }

    private static string Singularize(string value)
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

    private static string ToCamelIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value
            .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return value;
        }

        var first = parts[0].Length == 0
            ? string.Empty
            : char.ToLowerInvariant(parts[0][0]) + parts[0][1..];

        var rest = parts
            .Skip(1)
            .Select(part => part.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(part[0]) + part[1..]);

        return first + string.Concat(rest);
    }

    private ResolvedExecutionEnvironment NormalizeEnvironmentBaseUrlForMultiResourceSuite(
        ResolvedExecutionEnvironment resolvedEnv,
        IReadOnlyCollection<ExecutionTestCaseDto> orderedTestCases)
    {
        if (resolvedEnv == null || string.IsNullOrWhiteSpace(resolvedEnv.BaseUrl))
        {
            return resolvedEnv;
        }

        var normalizedBaseUrl = TryNormalizeBaseUrlForMultiResourceSuite(resolvedEnv.BaseUrl, orderedTestCases);
        if (string.Equals(normalizedBaseUrl, resolvedEnv.BaseUrl, StringComparison.Ordinal))
        {
            return resolvedEnv;
        }

        _logger.LogWarning(
            "Normalized execution environment base URL for multi-resource suite. OriginalBaseUrl={OriginalBaseUrl}, NormalizedBaseUrl={NormalizedBaseUrl}",
            resolvedEnv.BaseUrl,
            normalizedBaseUrl);

        resolvedEnv.BaseUrl = normalizedBaseUrl;
        return resolvedEnv;
    }

    private static string TryNormalizeBaseUrlForMultiResourceSuite(
        string baseUrl,
        IReadOnlyCollection<ExecutionTestCaseDto> orderedTestCases)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var absoluteBase)
            || (absoluteBase.Scheme != Uri.UriSchemeHttp && absoluteBase.Scheme != Uri.UriSchemeHttps))
        {
            return baseUrl;
        }

        var resourceHeads = orderedTestCases?
            .Select(testCase => ExtractTopLevelResourceSegment(testCase?.Request?.Url))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resourceHeads == null || resourceHeads.Count < 2)
        {
            return baseUrl;
        }

        var baseSegments = absoluteBase.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (baseSegments.Count == 0)
        {
            return baseUrl;
        }

        var lastSegment = baseSegments[^1];
        if (string.IsNullOrWhiteSpace(lastSegment)
            || IsReservedBasePathSegment(lastSegment)
            || !resourceHeads.Contains(lastSegment, StringComparer.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        baseSegments.RemoveAt(baseSegments.Count - 1);

        var normalizedPath = "/" + string.Join("/", baseSegments);
        var normalizedBuilder = new UriBuilder(absoluteBase)
        {
            Path = normalizedPath,
        };

        return normalizedBuilder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static string ExtractTopLevelResourceSegment(string urlOrPath)
    {
        var path = ExtractPath(urlOrPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var firstSegment = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstSegment)
            ? null
            : firstSegment.Trim();
    }

    private static bool IsReservedBasePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return true;
        }

        if (string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsVersionSegment(segment);
    }

    private void LogCaseOutcome(Guid runId, TestCaseExecutionResult result)
    {
        if (string.Equals(result.Status, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogWarning(
            "Test case completed with non-pass status. RunId={RunId}, Status={Status}, TestCase={TestCaseName} ({TestCaseId}), HttpMethod={HttpMethod}, Url={ResolvedUrl}, HttpStatus={HttpStatus}, RequestBodyLength={RequestBodyLength}, DependencyIds={DependencyIds}, SkippedBecauseDependencyIds={SkippedDependencyIds}, FailureCodes={FailureCodes}, FailureDetails={FailureDetails}",
            runId,
            result.Status,
            result.Name,
            result.TestCaseId,
            result.HttpMethod ?? "(n/a)",
            result.ResolvedUrl ?? "(n/a)",
            result.HttpStatusCode,
            result.RequestBody?.Length ?? 0,
            SummarizeGuidList(result.DependencyIds),
            SummarizeGuidList(result.SkippedBecauseDependencyIds),
            SummarizeFailureCodes(result.FailureReasons),
            SummarizeFailureDetails(result.FailureReasons));
    }

    private static string SummarizeFailedDependencyDetails(
        IReadOnlyList<Guid> failedDependencyIds,
        IReadOnlyDictionary<Guid, TestCaseExecutionResult> caseResultMap)
    {
        if (failedDependencyIds == null || failedDependencyIds.Count == 0)
        {
            return "(none)";
        }

        var details = new List<string>();

        foreach (var dependencyId in failedDependencyIds.Take(5))
        {
            if (caseResultMap != null && caseResultMap.TryGetValue(dependencyId, out var dependencyResult))
            {
                details.Add(
                    $"{dependencyResult.Name} ({dependencyId}) => {dependencyResult.Status}, Codes={SummarizeFailureCodes(dependencyResult.FailureReasons)}");
            }
            else
            {
                details.Add(dependencyId.ToString());
            }
        }

        if (failedDependencyIds.Count > 5)
        {
            details.Add($"+{failedDependencyIds.Count - 5} more");
        }

        return string.Join(" | ", details);
    }

    private static string SummarizeGuidList(IEnumerable<Guid> values)
    {
        if (values == null)
        {
            return "[]";
        }

        var list = values
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (list.Count == 0)
        {
            return "[]";
        }

        var preview = list.Take(5).Select(x => x.ToString());
        var suffix = list.Count > 5 ? $", +{list.Count - 5} more" : string.Empty;
        return $"[{string.Join(", ", preview)}{suffix}]";
    }

    private static string SummarizeFailureCodes(IEnumerable<ValidationFailureModel> failures)
    {
        if (failures == null)
        {
            return "(none)";
        }

        var codes = failures
            .Select(x => x?.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count == 0)
        {
            return "(none)";
        }

        var preview = codes.Take(5);
        var suffix = codes.Count > 5 ? $", +{codes.Count - 5} more" : string.Empty;
        return string.Join(", ", preview) + suffix;
    }

    private static string SummarizeFailureDetails(IEnumerable<ValidationFailureModel> failures)
    {
        if (failures == null)
        {
            return "(none)";
        }

        var details = failures
            .Select(FormatFailure)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (details.Count == 0)
        {
            return "(none)";
        }

        var preview = details.Take(3);
        var suffix = details.Count > 3 ? $" | +{details.Count - 3} more" : string.Empty;
        return string.Join(" | ", preview) + suffix;
    }

    private static string FormatFailure(ValidationFailureModel failure)
    {
        if (failure == null)
        {
            return null;
        }

        var code = string.IsNullOrWhiteSpace(failure.Code) ? "UNKNOWN" : failure.Code.Trim();
        var message = Truncate(failure.Message, 200);
        var target = string.IsNullOrWhiteSpace(failure.Target) ? null : failure.Target.Trim();

        if (string.IsNullOrWhiteSpace(target))
        {
            return string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}";
        }

        return string.IsNullOrWhiteSpace(message)
            ? $"{code}@{target}"
            : $"{code}@{target}: {message}";
    }

    private static bool IsDependencySatisfied(TestCaseExecutionResult dependencyResult)
    {
        if (dependencyResult == null)
        {
            return false;
        }

        if (string.Equals(dependencyResult.Status, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!dependencyResult.HttpStatusCode.HasValue)
        {
            return false;
        }

        var actualStatus = dependencyResult.HttpStatusCode.Value;
        if (actualStatus < 200 || actualStatus >= 300)
        {
            return false;
        }

        return IsDependencyFailureOnlyExpectationMismatch(dependencyResult.FailureReasons);
    }

    private static bool IsDependencyFailureOnlyExpectationMismatch(IReadOnlyCollection<ValidationFailureModel> failures)
    {
        if (failures == null || failures.Count == 0)
        {
            return true;
        }

        var allowedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STATUS_CODE_MISMATCH",
            "RESPONSE_SCHEMA_MISMATCH",
        };

        var normalizedCodes = failures
            .Select(failure => failure?.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            return true;
        }

        return normalizedCodes.All(allowedCodes.Contains);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
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
