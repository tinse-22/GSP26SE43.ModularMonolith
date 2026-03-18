using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Builds <see cref="TestCaseExpectation"/> entities from n8n/LLM-generated test case data.
/// </summary>
public interface ITestCaseExpectationBuilder
{
    /// <summary>
    /// Build a <see cref="TestCaseExpectation"/> entity from n8n response data.
    /// </summary>
    TestCaseExpectation Build(Guid testCaseId, N8nTestCaseExpectation source);
}

public class TestCaseExpectationBuilder : ITestCaseExpectationBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public TestCaseExpectation Build(Guid testCaseId, N8nTestCaseExpectation source)
    {
        if (source == null)
        {
            // Fallback: build minimal expectation (expect 200 OK)
            return new TestCaseExpectation
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCaseId,
                ExpectedStatus = JsonSerializer.Serialize(new[] { 200 }, JsonOpts),
            };
        }

        return new TestCaseExpectation
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            ExpectedStatus = SerializeList(source.ExpectedStatus),
            ResponseSchema = source.ResponseSchema,
            HeaderChecks = SerializeDict(source.HeaderChecks),
            BodyContains = SerializeStringList(source.BodyContains),
            BodyNotContains = SerializeStringList(source.BodyNotContains),
            JsonPathChecks = SerializeDict(source.JsonPathChecks),
            MaxResponseTime = source.MaxResponseTime,
        };
    }

    private static string SerializeList(List<int> list)
    {
        if (list == null || list.Count == 0)
            return JsonSerializer.Serialize(new[] { 200 }, JsonOpts);
        return JsonSerializer.Serialize(list, JsonOpts);
    }

    private static string SerializeStringList(List<string> list)
    {
        if (list == null || list.Count == 0) return null;
        return JsonSerializer.Serialize(list, JsonOpts);
    }

    private static string SerializeDict(Dictionary<string, string> dict)
    {
        if (dict == null || dict.Count == 0) return null;
        return JsonSerializer.Serialize(dict, JsonOpts);
    }
}
