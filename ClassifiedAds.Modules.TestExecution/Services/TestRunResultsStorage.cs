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

    public static TestRunResultModel ReconstructFromDatabase(TestRun run, IReadOnlyCollection<TestCaseResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return null;
        }

        var cases = results
            .OrderBy(x => x.OrderIndex)
            .Select(result => new TestCaseRunResultModel
            {
                TestCaseId = result.TestCaseId,
                EndpointId = result.EndpointId,
                Name = result.Name,
                OrderIndex = result.OrderIndex,
                Status = result.Status,
                HttpStatusCode = result.HttpStatusCode,
                DurationMs = result.DurationMs,
                ResolvedUrl = result.ResolvedUrl,
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
