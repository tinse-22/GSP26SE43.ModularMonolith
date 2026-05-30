using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    public string ScenarioKey { get; set; }

    public Guid EndpointId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string TestType { get; set; }
    public string Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public N8nTestCaseRequest Request { get; set; }
    public N8nTestCaseExpectation Expectation { get; set; }
    public List<N8nTestCaseVariable> Variables { get; set; } = new();

    /// <summary>
    /// Optional credential rewrite intent from n8n.
    /// Supported values: preserve, rewrite_email, rewrite_password, rewrite_both.
    /// </summary>
    public string CredentialPolicy { get; set; }

    /// <summary>
    /// Optional list of request fields that must never be rewritten by BE resolver.
    /// Example: ["request.body.password", "request.body.email"].
    /// </summary>
    public List<string> LockedFields { get; set; } = new();

    /// <summary>
    /// Optional auth mode hint from n8n.
    /// Supported values: none, optional, required.
    /// </summary>
    public string AuthMode { get; set; }

    /// <summary>
    /// Optional structured execution hints to keep callback extensible.
    /// </summary>
    public N8nExecutionHints ExecutionHints { get; set; }
}

public class N8nExecutionHints
{
    public string AuthMode { get; set; }

    public string CredentialPolicy { get; set; }

    public List<string> LockedFields { get; set; } = new();

    public bool? FlowRequired { get; set; }

    public string FlowId { get; set; }

    public List<string> DependsOn { get; set; } = new();

    public List<string> Produces { get; set; } = new();

    public List<string> Consumes { get; set; } = new();

    public bool? AbortIfDependencyFailed { get; set; }
}

public class N8nTestCaseRequest
{
    public string HttpMethod { get; set; }
    public string Url { get; set; }
    [JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    public Dictionary<string, string> Headers { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    public Dictionary<string, string> PathParams { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    public Dictionary<string, string> QueryParams { get; set; } = new();
    public string BodyType { get; set; }
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Body { get; set; }
    public int? Timeout { get; set; }
}

public class N8nTestCaseExpectation
{
    public List<int> ExpectedStatus { get; set; } = new();
    public string ResponseSchema { get; set; }
    [JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    public Dictionary<string, string> HeaderChecks { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public List<string> BodyContains { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public List<string> BodyNotContains { get; set; } = new();
    [JsonConverter(typeof(FlexibleStringDictionaryConverter))]
    public Dictionary<string, string> JsonPathChecks { get; set; } = new();
    public int? MaxResponseTime { get; set; }
    public string ExpectationSource { get; set; }
    public string RequirementCode { get; set; }
    public Guid? PrimaryRequirementId { get; set; }
    public string ExpectedProvenance { get; set; }
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
