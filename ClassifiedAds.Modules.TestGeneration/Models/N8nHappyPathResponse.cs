using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Response from n8n webhook after LLM-assisted happy-path test case generation.
/// The n8n workflow parses LLM output into this structured format.
/// </summary>
public class N8nHappyPathResponse
{
    public List<N8nGeneratedTestCase> TestCases { get; set; } = new();
    public string Model { get; set; }
    public int? TokensUsed { get; set; }
    public string Reasoning { get; set; }
}

public class N8nGeneratedTestCase
{
    public Guid EndpointId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public N8nTestCaseRequest Request { get; set; }
    public N8nTestCaseExpectation Expectation { get; set; }
    public List<N8nTestCaseVariable> Variables { get; set; } = new();
}

public class N8nTestCaseRequest
{
    public string HttpMethod { get; set; }
    public string Url { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> PathParams { get; set; } = new();
    public Dictionary<string, string> QueryParams { get; set; } = new();
    public string BodyType { get; set; }
    public string Body { get; set; }
    public int? Timeout { get; set; }
}

public class N8nTestCaseExpectation
{
    public List<int> ExpectedStatus { get; set; } = new();
    public string ResponseSchema { get; set; }
    public Dictionary<string, string> HeaderChecks { get; set; } = new();
    public List<string> BodyContains { get; set; } = new();
    public List<string> BodyNotContains { get; set; } = new();
    public Dictionary<string, string> JsonPathChecks { get; set; } = new();
    public int? MaxResponseTime { get; set; }
}

public class N8nTestCaseVariable
{
    public string VariableName { get; set; }
    public string ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
}
