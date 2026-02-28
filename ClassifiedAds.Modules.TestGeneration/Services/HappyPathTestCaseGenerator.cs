using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Constants;
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
/// Generates happy-path test cases using the pipeline:
/// 1. Fetch endpoint metadata (via cross-module contract)
/// 2. Map to <see cref="EndpointPromptContext"/> with business context
/// 3. Build Observation-Confirmation prompts (COmbine/RBCTest paper §3)
/// 4. Send payload to n8n webhook for LLM orchestration
/// 5. Parse n8n response into domain entities using builders
/// 6. Wire dependency chains between test cases
/// </summary>
public class HappyPathTestCaseGenerator : IHappyPathTestCaseGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IObservationConfirmationPromptBuilder _promptBuilder;
    private readonly IN8nIntegrationService _n8nService;
    private readonly ITestCaseRequestBuilder _requestBuilder;
    private readonly ITestCaseExpectationBuilder _expectationBuilder;
    private readonly ILogger<HappyPathTestCaseGenerator> _logger;

    public HappyPathTestCaseGenerator(
        IApiEndpointMetadataService endpointMetadataService,
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        ITestCaseRequestBuilder requestBuilder,
        ITestCaseExpectationBuilder expectationBuilder,
        ILogger<HappyPathTestCaseGenerator> logger)
    {
        _endpointMetadataService = endpointMetadataService ?? throw new ArgumentNullException(nameof(endpointMetadataService));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _n8nService = n8nService ?? throw new ArgumentNullException(nameof(n8nService));
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _expectationBuilder = expectationBuilder ?? throw new ArgumentNullException(nameof(expectationBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HappyPathGenerationResult> GenerateAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Guid specificationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting happy-path test case generation. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}",
            suite.Id, orderedEndpoints.Count);

        // Step 1: Fetch detailed endpoint metadata
        var endpointIds = orderedEndpoints.Select(e => e.EndpointId).ToList();
        var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
            specificationId, endpointIds, cancellationToken);

        var metadataMap = endpointMetadata.ToDictionary(e => e.EndpointId);

        // Step 2: Map to EndpointPromptContext with business context
        // Order the metadata in the same sequence as the approved order
        var orderedMetadata = orderedEndpoints
            .Where(oe => metadataMap.ContainsKey(oe.EndpointId))
            .Select(oe => metadataMap[oe.EndpointId])
            .ToList();

        var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, suite);

        // Step 3: Build Observation-Confirmation prompts for the ordered sequence
        var prompts = _promptBuilder.BuildForSequence(promptContexts);

        // Step 4: Build n8n webhook payload
        var payload = BuildN8nPayload(suite, orderedEndpoints, metadataMap, prompts, promptContexts);

        // Step 5: Call n8n webhook
        _logger.LogInformation(
            "Calling n8n webhook '{WebhookName}' for test case generation. TestSuiteId={TestSuiteId}",
            N8nWebhookNames.GenerateHappyPath, suite.Id);

        var n8nResponse = await _n8nService.TriggerWebhookAsync<N8nHappyPathPayload, N8nHappyPathResponse>(
            N8nWebhookNames.GenerateHappyPath, payload, cancellationToken);

        if (n8nResponse?.TestCases == null || n8nResponse.TestCases.Count == 0)
        {
            _logger.LogWarning(
                "n8n webhook returned no test cases. TestSuiteId={TestSuiteId}", suite.Id);

            return new HappyPathGenerationResult
            {
                TestCases = Array.Empty<TestCase>(),
                LlmModel = n8nResponse?.Model,
                TokensUsed = n8nResponse?.TokensUsed,
                Reasoning = n8nResponse?.Reasoning,
                EndpointsCovered = 0,
            };
        }

        // Step 6: Parse n8n response into domain entities
        var orderItemMap = orderedEndpoints.ToDictionary(e => e.EndpointId);
        var testCases = BuildTestCaseEntities(suite.Id, n8nResponse, orderItemMap);

        // Step 7: Wire dependency chains between test cases
        WireDependencyChains(testCases, orderItemMap);

        _logger.LogInformation(
            "Happy-path test case generation complete. TestSuiteId={TestSuiteId}, TestCasesGenerated={Count}, Model={Model}, TokensUsed={Tokens}",
            suite.Id, testCases.Count, n8nResponse.Model, n8nResponse.TokensUsed);

        return new HappyPathGenerationResult
        {
            TestCases = testCases,
            LlmModel = n8nResponse.Model,
            TokensUsed = n8nResponse.TokensUsed,
            Reasoning = n8nResponse.Reasoning,
            EndpointsCovered = testCases.Select(tc => tc.EndpointId).Distinct().Count(),
        };
    }

    private N8nHappyPathPayload BuildN8nPayload(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Dictionary<Guid, Contracts.ApiDocumentation.DTOs.ApiEndpointMetadataDto> metadataMap,
        IReadOnlyList<ObservationConfirmationPrompt> prompts,
        IReadOnlyList<EndpointPromptContext> promptContexts)
    {
        var endpointPayloads = new List<N8nEndpointPayload>();

        for (int i = 0; i < orderedEndpoints.Count; i++)
        {
            var orderItem = orderedEndpoints[i];
            metadataMap.TryGetValue(orderItem.EndpointId, out var metadata);

            suite.EndpointBusinessContexts.TryGetValue(orderItem.EndpointId, out var businessContext);

            // Get matching prompt if available
            N8nPromptPayload promptPayload = null;
            if (i < prompts.Count && prompts[i] != null)
            {
                var prompt = prompts[i];
                promptPayload = new N8nPromptPayload
                {
                    SystemPrompt = prompt.SystemPrompt,
                    CombinedPrompt = prompt.CombinedPrompt,
                    ObservationPrompt = prompt.ObservationPrompt,
                    ConfirmationPromptTemplate = prompt.ConfirmationPromptTemplate,
                };
            }

            endpointPayloads.Add(new N8nEndpointPayload
            {
                EndpointId = orderItem.EndpointId,
                HttpMethod = orderItem.HttpMethod,
                Path = orderItem.Path,
                OperationId = metadata?.OperationId,
                OrderIndex = orderItem.OrderIndex,
                DependsOnEndpointIds = orderItem.DependsOnEndpointIds ?? new List<Guid>(),
                IsAuthRelated = orderItem.IsAuthRelated,
                BusinessContext = businessContext,
                Prompt = promptPayload,
                ParameterSchemaPayloads = metadata?.ParameterSchemaPayloads?.ToList() ?? new List<string>(),
                ResponseSchemaPayloads = metadata?.ResponseSchemaPayloads?.ToList() ?? new List<string>(),
            });
        }

        return new N8nHappyPathPayload
        {
            TestSuiteId = suite.Id,
            TestSuiteName = suite.Name,
            GlobalBusinessRules = suite.GlobalBusinessRules,
            Endpoints = endpointPayloads,
        };
    }

    private IReadOnlyList<TestCase> BuildTestCaseEntities(
        Guid testSuiteId,
        N8nHappyPathResponse response,
        Dictionary<Guid, ApiOrderItemModel> orderItemMap)
    {
        var testCases = new List<TestCase>();

        foreach (var generated in response.TestCases)
        {
            var testCaseId = Guid.NewGuid();
            orderItemMap.TryGetValue(generated.EndpointId, out var orderItem);

            var testCase = new TestCase
            {
                Id = testCaseId,
                TestSuiteId = testSuiteId,
                EndpointId = generated.EndpointId,
                Name = SanitizeName(generated.Name, orderItem),
                Description = generated.Description,
                TestType = TestType.HappyPath,
                Priority = ParsePriority(generated.Priority),
                IsEnabled = true,
                OrderIndex = orderItem?.OrderIndex ?? 0,
                Tags = SerializeTags(generated.Tags),
                Version = 1,
            };

            // Build request
            testCase.Request = _requestBuilder.Build(testCaseId, generated.Request, orderItem);

            // Build expectation
            testCase.Expectation = _expectationBuilder.Build(testCaseId, generated.Expectation);

            // Build variables
            if (generated.Variables != null)
            {
                foreach (var v in generated.Variables)
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

            testCases.Add(testCase);
        }

        return testCases;
    }

    /// <summary>
    /// Wire dependency relationships between test cases based on endpoint dependency chains.
    /// If a test case's endpoint depends on other endpoints, link to ALL matching test cases.
    /// </summary>
    private static void WireDependencyChains(
        IReadOnlyList<TestCase> testCases,
        Dictionary<Guid, ApiOrderItemModel> orderItemMap)
    {
        // Build endpointId → testCaseId map
        var endpointToTestCase = new Dictionary<Guid, Guid>();
        foreach (var tc in testCases)
        {
            if (tc.EndpointId.HasValue && !endpointToTestCase.ContainsKey(tc.EndpointId.Value))
            {
                endpointToTestCase[tc.EndpointId.Value] = tc.Id;
            }
        }

        foreach (var tc in testCases)
        {
            if (!tc.EndpointId.HasValue) continue;
            if (!orderItemMap.TryGetValue(tc.EndpointId.Value, out var orderItem)) continue;
            if (orderItem.DependsOnEndpointIds == null || orderItem.DependsOnEndpointIds.Count == 0) continue;

            // Link to ALL dependencies that have generated test cases
            foreach (var depEndpointId in orderItem.DependsOnEndpointIds)
            {
                if (endpointToTestCase.TryGetValue(depEndpointId, out var depTestCaseId))
                {
                    tc.Dependencies.Add(new TestCaseDependency
                    {
                        Id = Guid.NewGuid(),
                        TestCaseId = tc.Id,
                        DependsOnTestCaseId = depTestCaseId,
                    });
                }
            }
        }
    }

    private static string SanitizeName(string name, ApiOrderItemModel orderItem)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Length > 200 ? name[..200] : name;

        // Fallback name from order item
        return orderItem != null
            ? $"{orderItem.HttpMethod} {orderItem.Path} - Happy Path"
            : "Happy Path Test Case";
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

    private static string SerializeTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0)
            return JsonSerializer.Serialize(new[] { "happy-path", "auto-generated" }, JsonOpts);

        // Ensure happy-path tag is present
        if (!tags.Contains("happy-path", StringComparer.OrdinalIgnoreCase))
            tags.Insert(0, "happy-path");

        if (!tags.Contains("auto-generated", StringComparer.OrdinalIgnoreCase))
            tags.Add("auto-generated");

        return JsonSerializer.Serialize(tags, JsonOpts);
    }
}
