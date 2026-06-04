using System;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseExecutionOverrideModel
{
    public Guid TestCaseId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string TestType { get; set; }

    public TestCaseExecutionRequestOverrideModel Request { get; set; }

    public TestCaseExecutionExpectationOverrideModel Expectation { get; set; }
}

public class TestCaseExecutionRequestOverrideModel
{
    public string HttpMethod { get; set; }

    public string Url { get; set; }

    public string Headers { get; set; }

    public string PathParams { get; set; }

    public string QueryParams { get; set; }

    public string BodyType { get; set; }

    public string Body { get; set; }

    public int? Timeout { get; set; }
}

public class TestCaseExecutionExpectationOverrideModel
{
    public string ExpectedStatus { get; set; }

    public string ResponseSchema { get; set; }

    public string HeaderChecks { get; set; }

    public string BodyContains { get; set; }

    public string BodyNotContains { get; set; }

    public string JsonPathChecks { get; set; }

    public int? MaxResponseTime { get; set; }

    public string ExpectedProvenance { get; set; }
}
