using ClassifiedAds.Modules.TestGeneration.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models;

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

    public int OrderIndex { get; set; }

    public string Tags { get; set; }

    public TestCaseRequestModel Request { get; set; }

    public TestCaseExpectationModel Expectation { get; set; }

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
            OrderIndex = entity.OrderIndex,
            Tags = entity.Tags,
            Request = entity.Request != null ? TestCaseRequestModel.FromEntity(entity.Request) : null,
            Expectation = entity.Expectation != null ? TestCaseExpectationModel.FromEntity(entity.Expectation) : null,
        };
    }
}

public class TestCaseRequestModel
{
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
