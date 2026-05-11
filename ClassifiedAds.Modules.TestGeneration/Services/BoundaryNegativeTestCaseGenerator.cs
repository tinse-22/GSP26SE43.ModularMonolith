using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private static readonly Regex HttpMethodTokenRegex = new(
        @"(?<![A-Za-z])(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IApiEndpointParameterDetailService _parameterDetailService;
    private readonly IPathParameterMutationGatewayService _pathMutationService;
    private readonly IBodyMutationEngine _bodyMutationEngine;
    private readonly ILlmScenarioSuggester _llmSuggester;
    private readonly ITestCaseRequestBuilder _requestBuilder;
    private readonly ITestCaseExpectationBuilder _expectationBuilder;
    private readonly ILlmSuggestionMaterializer _materializer;
    private readonly IExpectationResolver _expectationResolver;
    private readonly ILogger<BoundaryNegativeTestCaseGenerator> _logger;

    public BoundaryNegativeTestCaseGenerator(
        IApiEndpointMetadataService endpointMetadataService,
        IApiEndpointParameterDetailService parameterDetailService,
        IPathParameterMutationGatewayService pathMutationService,
        IBodyMutationEngine bodyMutationEngine,
        ILlmScenarioSuggester llmSuggester,
        ITestCaseRequestBuilder requestBuilder,
        ITestCaseExpectationBuilder expectationBuilder,
        ILlmSuggestionMaterializer materializer,
        IExpectationResolver expectationResolver,
        ILogger<BoundaryNegativeTestCaseGenerator> logger)
    {
        _endpointMetadataService = endpointMetadataService ?? throw new ArgumentNullException(nameof(endpointMetadataService));
        _parameterDetailService = parameterDetailService ?? throw new ArgumentNullException(nameof(parameterDetailService));
        _pathMutationService = pathMutationService ?? throw new ArgumentNullException(nameof(pathMutationService));
        _bodyMutationEngine = bodyMutationEngine ?? throw new ArgumentNullException(nameof(bodyMutationEngine));
        _llmSuggester = llmSuggester ?? throw new ArgumentNullException(nameof(llmSuggester));
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _expectationBuilder = expectationBuilder ?? throw new ArgumentNullException(nameof(expectationBuilder));
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
        _expectationResolver = expectationResolver ?? throw new ArgumentNullException(nameof(expectationResolver));
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
                            suite.Id,
                            orderItem,
                            metadata,
                            pathParam,
                            mutation,
                            pathParams,
                            options.SrsRequirements);
                        testCases.Add(tc);
                        pathMutationCount++;
                    }
                }
            }

            // 3b: Body mutations
            if (options.IncludeBodyMutations)
            {
                var allPathParams = paramDetail?.Parameters?
                    .Where(p => string.Equals(p.Location, "Path", StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<ParameterDetailDto>();

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
                    var tc = BuildBodyMutationTestCase(
                        suite.Id,
                        orderItem,
                        metadata,
                        mutation,
                        allPathParams,
                        options.SrsRequirements);
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
                SrsDocument = options.SrsDocument,
                SrsRequirements = options.SrsRequirements ?? Array.Empty<SrsRequirement>(),
            };

            var llmResult = await _llmSuggester.SuggestScenariosAsync(llmContext, cancellationToken);
            llmModel = llmResult.LlmModel;
            llmTokensUsed = llmResult.TokensUsed;

            var orderItemMap = orderedEndpoints.ToDictionary(e => e.EndpointId);
            foreach (var scenario in llmResult.Scenarios)
            {
                orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);
                metadataMap.TryGetValue(scenario.EndpointId, out var metadata);

                var tc = _materializer.MaterializeFromScenario(scenario, suite.Id, orderItem, 0);
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
        PathParameterMutationDto mutation,
        IReadOnlyList<ParameterDetailDto> allPathParams,
        IReadOnlyList<SrsRequirement> srsRequirements)
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

        // Build request with ALL path params - baseline values for non-mutated, mutation value for target
        var pathParams = BuildBaselinePathParams(allPathParams, pathParam.Name, mutation.Value);
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

        var resolvedExpectation = _expectationResolver.ResolveToN8nExpectation(new GeneratedScenarioContext
        {
            EndpointId = orderItem.EndpointId,
            TestType = testType,
            HttpMethod = orderItem.HttpMethod,
            SwaggerResponses = metadata?.Responses ?? Array.Empty<ApiEndpointResponseDescriptorDto>(),
            SrsRequirements = srsRequirements ?? Array.Empty<SrsRequirement>(),
            PreferredDefaultStatuses = mutation.GetEffectiveExpectedStatusCodes(),
            TargetFieldName = pathParam?.Name,
        });

        testCase.Expectation = _expectationBuilder.Build(testCaseId, resolvedExpectation);
        if (resolvedExpectation?.PrimaryRequirementId.HasValue == true)
        {
            testCase.PrimaryRequirementId = resolvedExpectation.PrimaryRequirementId;
        }

        return testCase;
    }

    private TestCase BuildBodyMutationTestCase(
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        ApiEndpointMetadataDto metadata,
        BodyMutation mutation,
        IReadOnlyList<ParameterDetailDto> allPathParams,
        IReadOnlyList<SrsRequirement> srsRequirements)
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

        // Build baseline path params so URL route tokens are resolved
        var pathParams = BuildBaselinePathParams(allPathParams, mutateParamName: null, mutateValue: null);
        var pathParamsJson = pathParams.Count > 0 ? JsonSerializer.Serialize(pathParams, JsonOpts) : null;

        testCase.Request = new TestCaseRequest
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            HttpMethod = ParseHttpMethod(orderItem.HttpMethod),
            Url = orderItem.Path,
            PathParams = pathParamsJson,
            BodyType = BodyType.JSON,
            Body = mutation.MutatedBody,
            Timeout = 30000,
        };

        var resolvedExpectation = _expectationResolver.ResolveToN8nExpectation(new GeneratedScenarioContext
        {
            EndpointId = orderItem.EndpointId,
            TestType = mutation.SuggestedTestType,
            HttpMethod = orderItem.HttpMethod,
            SwaggerResponses = metadata?.Responses ?? Array.Empty<ApiEndpointResponseDescriptorDto>(),
            SrsRequirements = srsRequirements ?? Array.Empty<SrsRequirement>(),
            PreferredDefaultStatuses = mutation.GetEffectiveExpectedStatusCodes(),
            TargetFieldName = mutation.TargetFieldName,
        });

        testCase.Expectation = _expectationBuilder.Build(testCaseId, resolvedExpectation);
        if (resolvedExpectation?.PrimaryRequirementId.HasValue == true)
        {
            testCase.PrimaryRequirementId = resolvedExpectation.PrimaryRequirementId;
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

    private static Entities.HttpMethod ParseHttpMethod(string method)
    {
        if (TryParseHttpMethod(method, out var parsed))
        {
            return parsed;
        }

        return Entities.HttpMethod.GET;
    }

    private static bool TryParseHttpMethod(string method, out Entities.HttpMethod parsed)
    {
        parsed = Entities.HttpMethod.GET;

        if (string.IsNullOrWhiteSpace(method))
        {
            return false;
        }

        if (MapHttpMethod(method, out parsed))
        {
            return true;
        }

        var match = HttpMethodTokenRegex.Match(method);
        return match.Success && MapHttpMethod(match.Groups[1].Value, out parsed);
    }

    private static bool MapHttpMethod(string method, out Entities.HttpMethod parsed)
    {
        switch (method?.Trim().ToUpperInvariant())
        {
            case "GET":
                parsed = Entities.HttpMethod.GET;
                return true;
            case "POST":
                parsed = Entities.HttpMethod.POST;
                return true;
            case "PUT":
                parsed = Entities.HttpMethod.PUT;
                return true;
            case "DELETE":
                parsed = Entities.HttpMethod.DELETE;
                return true;
            case "PATCH":
                parsed = Entities.HttpMethod.PATCH;
                return true;
            case "HEAD":
                parsed = Entities.HttpMethod.HEAD;
                return true;
            case "OPTIONS":
                parsed = Entities.HttpMethod.OPTIONS;
                return true;
            default:
                parsed = Entities.HttpMethod.GET;
                return false;
        }
    }

    /// <summary>
    /// Builds baseline path parameters dictionary.
    /// All params get default/example values; optionally override one param with a mutation value.
    /// </summary>
    private static Dictionary<string, string> BuildBaselinePathParams(
        IReadOnlyList<ParameterDetailDto> allPathParams,
        string mutateParamName,
        string mutateValue)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (allPathParams == null || allPathParams.Count == 0)
        {
            return result;
        }

        foreach (var param in allPathParams)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                continue;
            }

            string value;
            if (!string.IsNullOrEmpty(mutateParamName) &&
                string.Equals(param.Name, mutateParamName, StringComparison.OrdinalIgnoreCase))
            {
                // This is the param being mutated
                value = mutateValue ?? GetBaselineValue(param);
            }
            else
            {
                // Use baseline value for non-mutated params
                value = GetBaselineValue(param);
            }

            result[param.Name] = value;
        }

        return result;
    }

    /// <summary>
    /// Gets a baseline value for a path parameter from DefaultValue, Examples, or type-based fallback.
    /// </summary>
    private static string GetBaselineValue(ParameterDetailDto param)
    {
        // Prefer DefaultValue
        if (!string.IsNullOrWhiteSpace(param.DefaultValue))
        {
            return param.DefaultValue;
        }

        // Try Examples (JSON array or single value)
        if (!string.IsNullOrWhiteSpace(param.Examples))
        {
            try
            {
                var examples = JsonSerializer.Deserialize<List<string>>(param.Examples);
                if (examples != null && examples.Count > 0 && !string.IsNullOrWhiteSpace(examples[0]))
                {
                    return examples[0];
                }
            }
            catch
            {
                // Examples might be single value string
                var trimmed = param.Examples.Trim('"', ' ');
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    return trimmed;
                }
            }
        }

        // Type-based fallback
        var dataType = (param.DataType ?? string.Empty).ToLowerInvariant();
        var format = (param.Format ?? string.Empty).ToLowerInvariant();

        return (dataType, format) switch
        {
            ("integer", "int64") => "1",
            ("integer", _) => "1",
            ("number", _) => "1.0",
            ("string", "uuid") => "00000000-0000-0000-0000-000000000001",
            ("string", "date") => "2024-01-01",
            ("string", "date-time") => "2024-01-01T00:00:00Z",
            ("boolean", _) => "true",
            _ => "1", // Sensible default for most path params (often IDs)
        };
    }

    private static string SerializeTags(TestType testType, string source, params string[] extraTags)
        => LlmSuggestionMaterializer.SerializeTags(testType, source, extraTags);
}
