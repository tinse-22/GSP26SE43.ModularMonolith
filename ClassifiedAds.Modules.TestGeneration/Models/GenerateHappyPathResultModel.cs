using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Result model returned after happy-path test case generation.
/// </summary>
public class GenerateHappyPathResultModel
{
    public Guid TestSuiteId { get; set; }
    public int TotalGenerated { get; set; }
    public int EndpointsCovered { get; set; }
    public string LlmModel { get; set; }
    public int? TokensUsed { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public List<GeneratedTestCaseSummary> TestCases { get; set; } = new();
}

public class GeneratedTestCaseSummary
{
    public Guid TestCaseId { get; set; }
    public Guid? EndpointId { get; set; }
    public string Name { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public int OrderIndex { get; set; }
    public int VariableCount { get; set; }
}
