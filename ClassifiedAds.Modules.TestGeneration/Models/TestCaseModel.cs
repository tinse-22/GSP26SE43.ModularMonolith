using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// API view model for test case details (used in GET endpoints).
/// </summary>
public class TestCaseModel
{
    public Guid Id { get; set; }
    public Guid TestSuiteId { get; set; }
    public Guid? EndpointId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string TestType { get; set; }
    public string Priority { get; set; }
    public bool IsEnabled { get; set; }
    public List<Guid> DependsOnIds { get; set; } = new();
    public int OrderIndex { get; set; }
    public int? CustomOrderIndex { get; set; }
    public bool IsOrderCustomized { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Version { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset? UpdatedDateTime { get; set; }
    public string RowVersion { get; set; }

    // Nested models
    public TestCaseRequestModel Request { get; set; }
    public TestCaseExpectationModel Expectation { get; set; }
    public List<TestCaseVariableModel> Variables { get; set; } = new();

    public static TestCaseModel FromEntity(TestCase entity)
    {
        return new TestCaseModel
        {
            Id = entity.Id,
            TestSuiteId = entity.TestSuiteId,
            EndpointId = entity.EndpointId,
            Name = entity.Name,
            Description = entity.Description,
            TestType = entity.TestType.ToString(),
            Priority = entity.Priority.ToString(),
            IsEnabled = entity.IsEnabled,
            DependsOnIds = entity.Dependencies?.Select(d => d.DependsOnTestCaseId).ToList() ?? new List<Guid>(),
            OrderIndex = entity.OrderIndex,
            CustomOrderIndex = entity.CustomOrderIndex,
            IsOrderCustomized = entity.IsOrderCustomized,
            Tags = DeserializeTags(entity.Tags),
            Version = entity.Version,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
            RowVersion = entity.RowVersion != null ? Convert.ToBase64String(entity.RowVersion) : null,
            Request = entity.Request != null ? TestCaseRequestModel.FromEntity(entity.Request) : null,
            Expectation = entity.Expectation != null ? TestCaseExpectationModel.FromEntity(entity.Expectation) : null,
            Variables = MapVariables(entity.Variables),
        };
    }

    private static List<string> DeserializeTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private static List<TestCaseVariableModel> MapVariables(ICollection<TestCaseVariable> variables)
    {
        if (variables == null || variables.Count == 0) return new List<TestCaseVariableModel>();
        var result = new List<TestCaseVariableModel>();
        foreach (var v in variables)
            result.Add(TestCaseVariableModel.FromEntity(v));
        return result;
    }
}

public class TestCaseRequestModel
{
    public Guid Id { get; set; }
    public string HttpMethod { get; set; }
    public string Url { get; set; }
    public string Headers { get; set; }
    public string PathParams { get; set; }
    public string QueryParams { get; set; }
    public string BodyType { get; set; }
    public string Body { get; set; }
    public int Timeout { get; set; }

    public static TestCaseRequestModel FromEntity(TestCaseRequest entity)
    {
        return new TestCaseRequestModel
        {
            Id = entity.Id,
            HttpMethod = entity.HttpMethod.ToString(),
            Url = entity.Url,
            Headers = entity.Headers,
            PathParams = entity.PathParams,
            QueryParams = entity.QueryParams,
            BodyType = entity.BodyType.ToString(),
            Body = entity.Body,
            Timeout = entity.Timeout,
        };
    }
}

public class TestCaseExpectationModel
{
    public Guid Id { get; set; }
    public string ExpectedStatus { get; set; }
    public string ResponseSchema { get; set; }
    public string HeaderChecks { get; set; }
    public string BodyContains { get; set; }
    public string BodyNotContains { get; set; }
    public string JsonPathChecks { get; set; }
    public int? MaxResponseTime { get; set; }

    public static TestCaseExpectationModel FromEntity(TestCaseExpectation entity)
    {
        return new TestCaseExpectationModel
        {
            Id = entity.Id,
            ExpectedStatus = entity.ExpectedStatus,
            ResponseSchema = entity.ResponseSchema,
            HeaderChecks = entity.HeaderChecks,
            BodyContains = entity.BodyContains,
            BodyNotContains = entity.BodyNotContains,
            JsonPathChecks = entity.JsonPathChecks,
            MaxResponseTime = entity.MaxResponseTime,
        };
    }
}

public class TestCaseVariableModel
{
    public Guid Id { get; set; }
    public string VariableName { get; set; }
    public string ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }

    public static TestCaseVariableModel FromEntity(TestCaseVariable entity)
    {
        return new TestCaseVariableModel
        {
            Id = entity.Id,
            VariableName = entity.VariableName,
            ExtractFrom = entity.ExtractFrom.ToString(),
            JsonPath = entity.JsonPath,
            HeaderName = entity.HeaderName,
            Regex = entity.Regex,
            DefaultValue = entity.DefaultValue,
        };
    }
}
