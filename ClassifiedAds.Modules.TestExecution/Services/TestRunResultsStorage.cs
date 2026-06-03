using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestExecution.Services;

internal static class TestRunResultsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TestRunResultModel DeserializeCachedResult(string cached)
    {
        if (string.IsNullOrWhiteSpace(cached))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TestRunResultModel>(cached, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static TestRunResultModel ReconstructFromDatabase(
        TestRun run,
        IReadOnlyCollection<TestCaseResult> results,
        IReadOnlyList<ExecutionTestCaseDto> definitions = null)
    {
        if (results == null || results.Count == 0)
        {
            return null;
        }

        var definitionMap = BuildDefinitionMap(definitions);
        var cases = results
            .OrderBy(x => x.OrderIndex)
            .Select(result =>
            {
                definitionMap.TryGetValue(result.TestCaseId, out var definition);
                return ApplyDefinitionFallback(new TestCaseRunResultModel
                {
                    TestCaseId = result.TestCaseId,
                    EndpointId = result.EndpointId ?? definition?.EndpointId,
                    Name = result.Name,
                    Description = definition?.Description,
                    TestType = definition?.TestType ?? string.Empty,
                    OrderIndex = result.OrderIndex,
                    Status = result.Status,
                    HttpStatusCode = result.HttpStatusCode,
                    DurationMs = result.DurationMs,
                    ResolvedUrl = result.ResolvedUrl,
                    HttpMethod = string.Empty,
                    BodyType = string.Empty,
                    RequestBody = null,
                    QueryParams = new Dictionary<string, string>(),
                    TimeoutMs = 0,
                    ExpectedStatus = result.ExpectedStatus,
                    ExpectationSource = result.ExpectationSource,
                    RequirementCode = result.RequirementCode,
                    PrimaryRequirementId = result.PrimaryRequirementId,
                    ExpectedProvenance = result.ExpectedProvenance,
                    RequestHeaders = DeserializeJson<Dictionary<string, string>>(result.RequestHeaders) ?? new Dictionary<string, string>(),
                    ResponseHeaders = DeserializeJson<Dictionary<string, string>>(result.ResponseHeaders) ?? new Dictionary<string, string>(),
                    ResponseBodyPreview = result.ResponseBodyPreview,
                    FailureReasons = DeserializeJson<List<ValidationFailureModel>>(result.FailureReasons) ?? new List<ValidationFailureModel>(),
                    ExtractedVariables = DeserializeJson<Dictionary<string, string>>(result.ExtractedVariables) ?? new Dictionary<string, string>(),
                    DependencyIds = DeserializeJson<List<Guid>>(result.DependencyIds) ?? new List<Guid>(),
                    SkippedBecauseDependencyIds = DeserializeJson<List<Guid>>(result.SkippedBecauseDependencyIds) ?? new List<Guid>(),
                    StatusCodeMatched = result.StatusCodeMatched,
                    SchemaMatched = result.SchemaMatched,
                    HeaderChecksPassed = result.HeaderChecksPassed,
                    BodyContainsPassed = result.BodyContainsPassed,
                    BodyNotContainsPassed = result.BodyNotContainsPassed,
                    JsonPathChecksPassed = result.JsonPathChecksPassed,
                    ResponseTimePassed = result.ResponseTimePassed,
                }, definition);
            })
            .ToList();

        return new TestRunResultModel
        {
            Run = TestRunModel.FromEntity(run),
            ResultsSource = "database",
            ExecutedAt = run.StartedAt ?? run.CompletedAt ?? DateTimeOffset.UtcNow,
            ResolvedEnvironmentName = string.Empty,
            Cases = cases,
        };
    }

    public static TestRunResultModel ApplyDefinitionFallbacks(
        TestRunResultModel result,
        IReadOnlyList<ExecutionTestCaseDto> definitions)
    {
        if (result?.Cases == null || result.Cases.Count == 0 || definitions == null || definitions.Count == 0)
        {
            return result;
        }

        var definitionMap = BuildDefinitionMap(definitions);
        foreach (var testCase in result.Cases)
        {
            if (definitionMap.TryGetValue(testCase.TestCaseId, out var definition))
            {
                ApplyDefinitionFallback(testCase, definition);
            }
        }

        return result;
    }

    private static Dictionary<Guid, ExecutionTestCaseDto> BuildDefinitionMap(IReadOnlyList<ExecutionTestCaseDto> definitions)
    {
        return (definitions ?? Array.Empty<ExecutionTestCaseDto>())
            .GroupBy(x => x.TestCaseId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static TestCaseRunResultModel ApplyDefinitionFallback(
        TestCaseRunResultModel result,
        ExecutionTestCaseDto definition)
    {
        if (result == null || definition == null)
        {
            return result;
        }

        result.EndpointId ??= definition.EndpointId;
        result.Description ??= definition.Description;
        result.TestType = FirstNonEmpty(result.TestType, definition.TestType);
        result.HttpMethod = FirstNonEmpty(result.HttpMethod, definition.Request?.HttpMethod);
        result.ResolvedUrl = FirstNonEmpty(result.ResolvedUrl, definition.Request?.Url);
        result.BodyType = FirstNonEmpty(result.BodyType, definition.Request?.BodyType);
        result.RequestBody = FirstNonEmpty(result.RequestBody, definition.Request?.Body);
        result.TimeoutMs = result.TimeoutMs > 0 ? result.TimeoutMs : definition.Request?.Timeout ?? 30000;
        result.ExpectedStatus = FirstNonEmpty(result.ExpectedStatus, definition.Expectation?.ExpectedStatus);
        result.ExpectedBodyContains = FirstNonEmpty(result.ExpectedBodyContains, definition.Expectation?.BodyContains);
        result.ExpectedBodyNotContains = FirstNonEmpty(result.ExpectedBodyNotContains, definition.Expectation?.BodyNotContains);
        result.ExpectedHeaderChecks = FirstNonEmpty(result.ExpectedHeaderChecks, definition.Expectation?.HeaderChecks);
        result.ExpectedJsonPathChecks = FirstNonEmpty(result.ExpectedJsonPathChecks, definition.Expectation?.JsonPathChecks);
        result.ExpectedMaxResponseTime ??= definition.Expectation?.MaxResponseTime;
        result.ExpectationSource = FirstNonEmpty(result.ExpectationSource, definition.Expectation?.ExpectationSource);
        result.RequirementCode = FirstNonEmpty(result.RequirementCode, definition.Expectation?.RequirementCode);
        result.PrimaryRequirementId ??= definition.Expectation?.PrimaryRequirementId;
        result.ExpectedProvenance = FirstNonEmpty(result.ExpectedProvenance, definition.Expectation?.ExpectedProvenance);

        if (result.QueryParams == null || result.QueryParams.Count == 0)
        {
            result.QueryParams = DeserializeJson<Dictionary<string, string>>(definition.Request?.QueryParams)
                ?? new Dictionary<string, string>();
        }

        if (result.RequestHeaders == null || result.RequestHeaders.Count == 0)
        {
            result.RequestHeaders = DeserializeJson<Dictionary<string, string>>(definition.Request?.Headers)
                ?? new Dictionary<string, string>();
        }

        return result;
    }

    private static string FirstNonEmpty(string preferred, string fallback)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred
            : fallback;
    }

    private static T DeserializeJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
