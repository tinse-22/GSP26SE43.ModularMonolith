using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Materializes LLM suggestions into TestCase domain entities.
/// Extracts shared mapping logic from BoundaryNegativeTestCaseGenerator.BuildLlmSuggestionTestCase().
/// </summary>
public class LlmSuggestionMaterializer : ILlmSuggestionMaterializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ITestCaseRequestBuilder _requestBuilder;
    private readonly ITestCaseExpectationBuilder _expectationBuilder;

    public LlmSuggestionMaterializer(
        ITestCaseRequestBuilder requestBuilder,
        ITestCaseExpectationBuilder expectationBuilder)
    {
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        _expectationBuilder = expectationBuilder ?? throw new ArgumentNullException(nameof(expectationBuilder));
    }

    public TestCase MaterializeFromScenario(
        LlmSuggestedScenario scenario,
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        int orderIndex)
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
            OrderIndex = orderIndex,
        };

        // Build request from LLM suggestion
        var n8nRequest = new N8nTestCaseRequest
        {
            HttpMethod = orderItem?.HttpMethod,
            Url = orderItem?.Path,
            BodyType = scenario.SuggestedBodyType,
            Body = scenario.SuggestedBody,
            PathParams = scenario.SuggestedPathParams,
            QueryParams = scenario.SuggestedQueryParams,
            Headers = scenario.SuggestedHeaders,
        };
        testCase.Request = _requestBuilder.Build(testCaseId, n8nRequest, orderItem);

        var n8nExpectation = BuildScenarioExpectation(scenario);
        testCase.Expectation = _expectationBuilder.Build(testCaseId, n8nExpectation);
        if (n8nExpectation?.PrimaryRequirementId.HasValue == true)
        {
            testCase.PrimaryRequirementId = n8nExpectation.PrimaryRequirementId;
        }

        // Build variables from LLM suggestion
        if (scenario.Variables != null)
        {
            foreach (var v in scenario.Variables)
            {
                if (string.IsNullOrWhiteSpace(v.VariableName))
                {
                    continue;
                }

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

    public TestCase MaterializeFromSuggestion(
        LlmSuggestion suggestion,
        ApiOrderItemModel orderItem,
        int orderIndex)
    {
        var testCaseId = Guid.NewGuid();

        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = suggestion.TestSuiteId,
            EndpointId = suggestion.EndpointId,
            Name = SanitizeName(suggestion.SuggestedName, orderItem),
            Description = suggestion.SuggestedDescription,
            TestType = suggestion.TestType,
            Priority = suggestion.Priority,
            IsEnabled = true,
            Tags = suggestion.SuggestedTags,
            Version = 1,
            OrderIndex = orderIndex,
        };

        var n8nRequest = DeserializeOrDefault<N8nTestCaseRequest>(suggestion.SuggestedRequest);
        testCase.Request = _requestBuilder.Build(testCaseId, n8nRequest, orderItem);

        var n8nExpectation = DeserializeOrDefault<N8nTestCaseExpectation>(suggestion.SuggestedExpectation);
        testCase.Expectation = _expectationBuilder.Build(testCaseId, n8nExpectation);
        if (n8nExpectation?.PrimaryRequirementId.HasValue == true)
        {
            testCase.PrimaryRequirementId = n8nExpectation.PrimaryRequirementId;
        }

        var variables = DeserializeOrDefault<List<N8nTestCaseVariable>>(suggestion.SuggestedVariables);
        if (variables != null)
        {
            foreach (var v in variables)
            {
                if (string.IsNullOrWhiteSpace(v.VariableName))
                {
                    continue;
                }

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

    public TestCase MaterializeFromModifiedContent(
        LlmSuggestion suggestion,
        EditableLlmSuggestionInput modified,
        ApiOrderItemModel orderItem,
        int orderIndex)
    {
        var testCaseId = Guid.NewGuid();

        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = suggestion.TestSuiteId,
            EndpointId = suggestion.EndpointId,
            Name = SanitizeName(modified.Name ?? suggestion.SuggestedName, orderItem),
            Description = modified.Description ?? suggestion.SuggestedDescription,
            TestType = ParseTestType(modified.TestType) ?? suggestion.TestType,
            Priority = ParsePriorityOrDefault(modified.Priority) ?? suggestion.Priority,
            IsEnabled = true,
            Tags = modified.Tags != null
                ? JsonSerializer.Serialize(modified.Tags, JsonOpts)
                : suggestion.SuggestedTags,
            Version = 1,
            OrderIndex = orderIndex,
        };

        // Use modified request if provided, else fall back to original suggestion
        N8nTestCaseRequest n8nRequest;
        if (modified.Request != null)
        {
            n8nRequest = new N8nTestCaseRequest
            {
                HttpMethod = modified.Request.HttpMethod,
                Url = modified.Request.Url,
                Body = modified.Request.Body,
                Headers = modified.Request.Headers,
                PathParams = modified.Request.PathParams,
                QueryParams = modified.Request.QueryParams,
            };
        }
        else
        {
            n8nRequest = DeserializeOrDefault<N8nTestCaseRequest>(suggestion.SuggestedRequest);
        }

        testCase.Request = _requestBuilder.Build(testCaseId, n8nRequest, orderItem);

        var originalExpectation = DeserializeOrDefault<N8nTestCaseExpectation>(suggestion.SuggestedExpectation);

        // Use modified expectation if provided, else fall back to original suggestion
        N8nTestCaseExpectation n8nExpectation;
        if (modified.Expectation != null)
        {
            n8nExpectation = new N8nTestCaseExpectation
            {
                ExpectedStatus = modified.Expectation.ExpectedStatus ?? originalExpectation?.ExpectedStatus,
                BodyContains = modified.Expectation.BodyContains ?? originalExpectation?.BodyContains,
                BodyNotContains = modified.Expectation.BodyNotContains ?? originalExpectation?.BodyNotContains,
                ResponseSchema = modified.Expectation.ResponseSchema ?? originalExpectation?.ResponseSchema,
                HeaderChecks = modified.Expectation.HeaderChecks ?? originalExpectation?.HeaderChecks,
                JsonPathChecks = modified.Expectation.JsonPathChecks ?? originalExpectation?.JsonPathChecks,
                MaxResponseTime = modified.Expectation.MaxResponseTime ?? originalExpectation?.MaxResponseTime,
                ExpectationSource = originalExpectation?.ExpectationSource,
                RequirementCode = originalExpectation?.RequirementCode,
                PrimaryRequirementId = originalExpectation?.PrimaryRequirementId,
            };
        }
        else
        {
            n8nExpectation = originalExpectation;
        }

        testCase.Expectation = _expectationBuilder.Build(testCaseId, n8nExpectation);
        if (n8nExpectation?.PrimaryRequirementId.HasValue == true)
        {
            testCase.PrimaryRequirementId = n8nExpectation.PrimaryRequirementId;
        }

        // Use modified variables if provided, else fall back to original suggestion
        List<N8nTestCaseVariable> variables;
        if (modified.Variables != null)
        {
            variables = new List<N8nTestCaseVariable>();
            foreach (var v in modified.Variables)
            {
                variables.Add(new N8nTestCaseVariable
                {
                    VariableName = v.VariableName,
                    ExtractFrom = v.ExtractFrom,
                    JsonPath = v.JsonPath,
                    HeaderName = v.HeaderName,
                    Regex = v.Regex,
                    DefaultValue = v.DefaultValue,
                });
            }
        }
        else
        {
            variables = DeserializeOrDefault<List<N8nTestCaseVariable>>(suggestion.SuggestedVariables);
        }

        if (variables != null)
        {
            foreach (var v in variables)
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

    // --- Static helpers (shared with BoundaryNegativeTestCaseGenerator) ---
    internal static string SanitizeName(string name, ApiOrderItemModel orderItem)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Length > 200 ? name[..200] : name;
        }

        return orderItem != null
            ? $"{orderItem.HttpMethod} {orderItem.Path} - Boundary/Negative"
            : "Boundary/Negative Test Case";
    }

    internal static TestPriority ParsePriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return TestPriority.Medium;
        }

        return priority.Trim().ToLowerInvariant() switch
        {
            "critical" => TestPriority.Critical,
            "high" => TestPriority.High,
            "medium" => TestPriority.Medium,
            "low" => TestPriority.Low,
            _ => TestPriority.Medium,
        };
    }

    internal static ExtractFrom ParseExtractFrom(string extractFrom)
    {
        if (string.IsNullOrWhiteSpace(extractFrom))
        {
            return ExtractFrom.ResponseBody;
        }

        return extractFrom.Trim().ToLowerInvariant() switch
        {
            "responsebody" or "response_body" or "body" => ExtractFrom.ResponseBody,
            "requestbody" or "request_body" => ExtractFrom.RequestBody,
            "responseheader" or "response_header" or "header" => ExtractFrom.ResponseHeader,
            "status" => ExtractFrom.Status,
            _ => ExtractFrom.ResponseBody,
        };
    }

    internal static string SerializeTags(TestType testType, string source, params string[] extraTags)
    {
        var tags = new List<string>();

        tags.Add(testType switch
        {
            TestType.HappyPath => "happy-path",
            TestType.Boundary => "boundary",
            TestType.Negative => "negative",
            TestType.Security => "security",
            TestType.Performance => "performance",
            _ => "negative",
        });
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

    private static N8nTestCaseExpectation BuildScenarioExpectation(LlmSuggestedScenario scenario)
    {
        if (scenario == null)
        {
            return new N8nTestCaseExpectation();
        }

        var bodyContains = scenario.SuggestedBodyContains?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
            ?? new List<string>();

        if (bodyContains.Count == 0
            && scenario.SuggestedTestType == TestType.HappyPath
            && !string.IsNullOrWhiteSpace(scenario.ExpectedBehavior))
        {
            bodyContains.Add(scenario.ExpectedBehavior);
        }

        return new N8nTestCaseExpectation
        {
            ExpectedStatus = scenario.GetEffectiveExpectedStatusCodes(),
            BodyContains = bodyContains,
            BodyNotContains = scenario.SuggestedBodyNotContains?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>(),
            HeaderChecks = scenario.SuggestedHeaderChecks != null
                ? new Dictionary<string, string>(scenario.SuggestedHeaderChecks, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            JsonPathChecks = scenario.SuggestedJsonPathChecks != null
                ? new Dictionary<string, string>(scenario.SuggestedJsonPathChecks, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ExpectationSource = scenario.ExpectationSource,
            RequirementCode = scenario.RequirementCode,
            PrimaryRequirementId = scenario.PrimaryRequirementId,
        };
    }

    private static T DeserializeOrDefault<T>(string json)
        where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpts) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    private static TestType? ParseTestType(string testType)
    {
        if (string.IsNullOrWhiteSpace(testType))
        {
            return null;
        }

        return Enum.TryParse<TestType>(testType, true, out var parsed) ? parsed : null;
    }

    private static TestPriority? ParsePriorityOrDefault(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return null;
        }

        return priority.Trim().ToLowerInvariant() switch
        {
            "critical" => TestPriority.Critical,
            "high" => TestPriority.High,
            "medium" => TestPriority.Medium,
            "low" => TestPriority.Low,
            _ => null,
        };
    }
}
