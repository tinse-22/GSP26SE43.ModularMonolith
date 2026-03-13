using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Orchestrates boundary/negative test case generation.
/// Pipeline:
/// 1. Fetch endpoint metadata + parameter details
/// 2. For each endpoint: generate path mutations + body mutations
/// 3. Call LLM for additional scenario suggestions (if enabled)
/// 4. Build TestCase domain entities with sequential OrderIndex
/// </summary>
public class BoundaryNegativeTestCaseGenerator : IBoundaryNegativeTestCaseGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IApiEndpointParameterDetailService _parameterDetailService;
    private readonly IPathParameterMutationGatewayService _pathMutationService;
    private readonly IBodyMutationEngine _bodyMutationEngine;
    private readonly ILlmScenarioSuggester _llmSuggester;
    private readonly ITestCaseRequestBuilder _requestBuilder;
    private readonly ITestCaseExpectationBuilder _expectationBuilder;
    private readonly ILogger<BoundaryNegativeTestCaseGenerator> _logger;

    public BoundaryNegativeTestCaseGenerator(
        IApiEndpointMetadataService endpointMetadataService,
        IApiEndpointParameterDetailService parameterDetailService,
        IPathParameterMutationGatewayService pathMutationService,
        IBodyMutationEngine bodyMutationEngine,
        ILlmScenarioSuggester llmSuggester,
        ITestCaseRequestBuilder requestBuilder,
        ITestCaseExpectationBuilder expectationBuilder,
        ILogger<BoundaryNegativeTestCaseGenerator> logger)
    {
        _endpointMetadataService = endpointMetadataService ?? throw new ArgumentNullException(nameof(endpointMetadataService));
        _parameterDetailService = parameterDetailService ?? throw new ArgumentNullException(nameof(parameterDetailService));
        _pathMutationService = pathMutationService ?? throw new ArgumentNullException(nameof(pathMutationService));
        _bodyMutationEngine = bodyMutationEngine ?? throw new ArgumentNullException(nameof(bodyMutationEngine));
        _llmSuggester = llmSuggester ?? throw new ArgumentNullException(nameof(llmSuggester));
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _expectationBuilder = expectationBuilder ?? throw new ArgumentNullException(nameof(expectationBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BoundaryNegativeGenerationResult> GenerateAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Guid specificationId,
        BoundaryNegativeOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting boundary/negative test case generation. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}, " +
            "PathMutations={PathMutations}, BodyMutations={BodyMutations}, LlmSuggestions={LlmSuggestions}",
            suite.Id, orderedEndpoints.Count, options.IncludePathMutations, options.IncludeBodyMutations, options.IncludeLlmSuggestions);

        var testCases = new List<TestCase>();
        int pathMutationCount = 0;
        int bodyMutationCount = 0;
        int llmSuggestionCount = 0;
        string llmModel = null;
        int? llmTokensUsed = null;

        // Step 1: Fetch endpoint metadata
        var endpointIds = orderedEndpoints.Select(e => e.EndpointId).ToList();
        var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
            specificationId, endpointIds, cancellationToken);
        var metadataMap = endpointMetadata.ToDictionary(e => e.EndpointId);

        // Step 2: Fetch parameter details (needed for body + path mutations)
        IReadOnlyList<EndpointParameterDetailDto> parameterDetails = Array.Empty<EndpointParameterDetailDto>();
        if (options.IncludePathMutations || options.IncludeBodyMutations)
        {
            parameterDetails = await _parameterDetailService.GetParameterDetailsAsync(
                specificationId, endpointIds, cancellationToken);
        }

        var parameterMap = parameterDetails.ToDictionary(p => p.EndpointId);

        // Step 3: Per-endpoint rule-based mutations
        foreach (var orderItem in orderedEndpoints)
        {
            metadataMap.TryGetValue(orderItem.EndpointId, out var metadata);
            parameterMap.TryGetValue(orderItem.EndpointId, out var paramDetail);

            // 3a: Path parameter mutations
            if (options.IncludePathMutations && paramDetail?.Parameters != null)
            {
                var pathParams = paramDetail.Parameters
                    .Where(p => string.Equals(p.Location, "Path", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var pathParam in pathParams)
                {
                    var mutations = _pathMutationService.GenerateMutations(
                        pathParam.Name, pathParam.DataType, pathParam.Format, pathParam.DefaultValue);

                    foreach (var mutation in mutations)
                    {
                        var tc = BuildPathMutationTestCase(
                            suite.Id, orderItem, metadata, pathParam, mutation);
                        testCases.Add(tc);
                        pathMutationCount++;
                    }
                }
            }

            // 3b: Body mutations
            if (options.IncludeBodyMutations)
            {
                var bodyParams = paramDetail?.Parameters?
                    .Where(p => string.Equals(p.Location, "Body", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var bodyContext = new BodyMutationContext
                {
                    EndpointId = orderItem.EndpointId,
                    HttpMethod = orderItem.HttpMethod,
                    Path = orderItem.Path,
                    BodyParameters = bodyParams ?? new List<ParameterDetailDto>(),
                    RequestBodySchema = metadata?.ParameterSchemaPayloads?.FirstOrDefault(),
                };

                var bodyMutations = _bodyMutationEngine.GenerateMutations(bodyContext);

                foreach (var mutation in bodyMutations)
                {
                    var tc = BuildBodyMutationTestCase(suite.Id, orderItem, metadata, mutation);
                    testCases.Add(tc);
                    bodyMutationCount++;
                }
            }
        }

        // Step 4: LLM scenario suggestions
        if (options.IncludeLlmSuggestions)
        {
            var llmContext = new LlmScenarioSuggestionContext
            {
                TestSuiteId = suite.Id,
                UserId = options.UserId,
                Suite = suite,
                EndpointMetadata = endpointMetadata,
                OrderedEndpoints = orderedEndpoints,
                SpecificationId = specificationId,
                EndpointParameterDetails = parameterMap,
            };

            var llmResult = await _llmSuggester.SuggestScenariosAsync(llmContext, cancellationToken);
            llmModel = llmResult.LlmModel;
            llmTokensUsed = llmResult.TokensUsed;

            var orderItemMap = orderedEndpoints.ToDictionary(e => e.EndpointId);
            foreach (var scenario in llmResult.Scenarios)
            {
                orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);
                metadataMap.TryGetValue(scenario.EndpointId, out var metadata);

                var tc = BuildLlmSuggestionTestCase(suite.Id, orderItem, metadata, scenario);
                testCases.Add(tc);
                llmSuggestionCount++;
            }
        }

        // Step 5: Assign sequential OrderIndex
        for (int i = 0; i < testCases.Count; i++)
        {
            testCases[i].OrderIndex = i;
        }

        _logger.LogInformation(
            "Boundary/negative test case generation complete. TestSuiteId={TestSuiteId}, " +
            "Total={Total}, PathMutations={PathMutations}, BodyMutations={BodyMutations}, LlmSuggestions={LlmSuggestions}",
            suite.Id, testCases.Count, pathMutationCount, bodyMutationCount, llmSuggestionCount);

        return new BoundaryNegativeGenerationResult
        {
            TestCases = testCases,
            PathMutationCount = pathMutationCount,
            BodyMutationCount = bodyMutationCount,
            LlmSuggestionCount = llmSuggestionCount,
            LlmModel = llmModel,
            LlmTokensUsed = llmTokensUsed,
            EndpointsCovered = testCases
                .Where(tc => tc.EndpointId.HasValue)
                .Select(tc => tc.EndpointId.Value)
                .Distinct()
                .Count(),
        };
    }

    private TestCase BuildPathMutationTestCase(
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        ApiEndpointMetadataDto metadata,
        ParameterDetailDto pathParam,
        PathParameterMutationDto mutation)
    {
        var testCaseId = Guid.NewGuid();
        var testType = ClassifyPathMutationType(mutation.MutationType);

        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = testSuiteId,
            EndpointId = orderItem.EndpointId,
            Name = $"{orderItem.HttpMethod} {orderItem.Path} - {mutation.Label}",
            Description = mutation.Description,
            TestType = testType,
            Priority = TestPriority.High,
            IsEnabled = true,
            Tags = SerializeTags(testType, "rule-based", "path-mutation"),
            Version = 1,
        };

        // Build request with mutated path param
        var pathParams = new Dictionary<string, string> { { pathParam.Name, mutation.Value } };
        testCase.Request = new TestCaseRequest
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            HttpMethod = ParseHttpMethod(orderItem.HttpMethod),
            Url = orderItem.Path,
            PathParams = JsonSerializer.Serialize(pathParams, JsonOpts),
            BodyType = BodyType.None,
            Timeout = 30000,
        };

        // Build expectation
        testCase.Expectation = new TestCaseExpectation
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            ExpectedStatus = JsonSerializer.Serialize(new[] { mutation.ExpectedStatusCode }, JsonOpts),
        };

        return testCase;
    }

    private TestCase BuildBodyMutationTestCase(
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        ApiEndpointMetadataDto metadata,
        BodyMutation mutation)
    {
        var testCaseId = Guid.NewGuid();

        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = testSuiteId,
            EndpointId = orderItem.EndpointId,
            Name = $"{orderItem.HttpMethod} {orderItem.Path} - {mutation.Label}",
            Description = mutation.Description,
            TestType = mutation.SuggestedTestType,
            Priority = TestPriority.High,
            IsEnabled = true,
            Tags = SerializeTags(mutation.SuggestedTestType, "rule-based", "body-mutation"),
            Version = 1,
        };

        testCase.Request = new TestCaseRequest
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            HttpMethod = ParseHttpMethod(orderItem.HttpMethod),
            Url = orderItem.Path,
            BodyType = BodyType.JSON,
            Body = mutation.MutatedBody,
            Timeout = 30000,
        };

        testCase.Expectation = new TestCaseExpectation
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            ExpectedStatus = JsonSerializer.Serialize(new[] { mutation.ExpectedStatusCode }, JsonOpts),
        };

        return testCase;
    }

    private TestCase BuildLlmSuggestionTestCase(
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        ApiEndpointMetadataDto metadata,
        LlmSuggestedScenario scenario)
    {
        var testCaseId = Guid.NewGuid();

        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = testSuiteId,
            EndpointId = scenario.EndpointId,
            Name = SanitizeName(scenario.ScenarioName, orderItem),
            Description = scenario.Description,
            TestType = scenario.SuggestedTestType,
            Priority = ParsePriority(scenario.Priority),
            IsEnabled = true,
            Tags = SerializeTags(scenario.SuggestedTestType, "llm-suggested", scenario.Tags?.ToArray() ?? Array.Empty<string>()),
            Version = 1,
        };

        // Build request from LLM suggestion
        var n8nRequest = new N8nTestCaseRequest
        {
            HttpMethod = orderItem?.HttpMethod,
            Url = orderItem?.Path,
            Body = scenario.SuggestedBody,
            PathParams = scenario.SuggestedPathParams,
            QueryParams = scenario.SuggestedQueryParams,
            Headers = scenario.SuggestedHeaders,
        };
        testCase.Request = _requestBuilder.Build(testCaseId, n8nRequest, orderItem);

        // Build expectation from LLM suggestion
        var n8nExpectation = new N8nTestCaseExpectation
        {
            ExpectedStatus = new List<int> { scenario.ExpectedStatusCode },
            BodyContains = !string.IsNullOrWhiteSpace(scenario.ExpectedBehavior)
                ? new List<string> { scenario.ExpectedBehavior }
                : new List<string>(),
        };
        testCase.Expectation = _expectationBuilder.Build(testCaseId, n8nExpectation);

        // Build variables from LLM suggestion (reuse FE-05B pattern)
        if (scenario.Variables != null)
        {
            foreach (var v in scenario.Variables)
            {
                testCase.Variables.Add(new TestCaseVariable
                {
                    Id = Guid.NewGuid(),
                    TestCaseId = testCaseId,
                    VariableName = v.VariableName,
                    ExtractFrom = ParseExtractFrom(v.ExtractFrom),
                    JsonPath = v.JsonPath,
                    HeaderName = v.HeaderName,
                    Regex = v.Regex,
                    DefaultValue = v.DefaultValue,
                });
            }
        }

        return testCase;
    }

    private static TestType ClassifyPathMutationType(string mutationType)
    {
        if (string.IsNullOrWhiteSpace(mutationType))
        {
            return TestType.Negative;
        }

        var lower = mutationType.ToLowerInvariant();

        // Boundary: values at limits (zero, max, overflow)
        if (lower.Contains("boundary") || lower.Contains("zero") ||
            lower.Contains("max") || lower.Contains("overflow"))
        {
            return TestType.Boundary;
        }

        // Everything else: Negative (injection, wrongType, empty, nonExistent, specialChars)
        return TestType.Negative;
    }

    private static string SanitizeName(string name, ApiOrderItemModel orderItem)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Length > 200 ? name[..200] : name;
        }

        return orderItem != null
            ? $"{orderItem.HttpMethod} {orderItem.Path} - Boundary/Negative"
            : "Boundary/Negative Test Case";
    }

    private static TestPriority ParsePriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return TestPriority.Medium;

        return priority.Trim().ToLowerInvariant() switch
        {
            "critical" => TestPriority.Critical,
            "high" => TestPriority.High,
            "medium" => TestPriority.Medium,
            "low" => TestPriority.Low,
            _ => TestPriority.Medium,
        };
    }

    private static Entities.HttpMethod ParseHttpMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) return Entities.HttpMethod.GET;

        return method.Trim().ToUpperInvariant() switch
        {
            "GET" => Entities.HttpMethod.GET,
            "POST" => Entities.HttpMethod.POST,
            "PUT" => Entities.HttpMethod.PUT,
            "DELETE" => Entities.HttpMethod.DELETE,
            "PATCH" => Entities.HttpMethod.PATCH,
            "HEAD" => Entities.HttpMethod.HEAD,
            "OPTIONS" => Entities.HttpMethod.OPTIONS,
            _ => Entities.HttpMethod.GET,
        };
    }

    private static ExtractFrom ParseExtractFrom(string extractFrom)
    {
        if (string.IsNullOrWhiteSpace(extractFrom)) return ExtractFrom.ResponseBody;

        return extractFrom.Trim().ToLowerInvariant() switch
        {
            "responsebody" or "response_body" or "body" => ExtractFrom.ResponseBody,
            "responseheader" or "response_header" or "header" => ExtractFrom.ResponseHeader,
            "status" => ExtractFrom.Status,
            _ => ExtractFrom.ResponseBody,
        };
    }

    private static string SerializeTags(TestType testType, string source, params string[] extraTags)
    {
        var tags = new List<string>();

        tags.Add(testType == TestType.Boundary ? "boundary" : "negative");
        tags.Add("auto-generated");
        tags.Add(source);

        foreach (var tag in extraTags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag);
            }
        }

        return JsonSerializer.Serialize(tags, JsonOpts);
    }
}
