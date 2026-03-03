using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Result model returned after boundary/negative test case generation.
/// </summary>
public class GenerateBoundaryNegativeResultModel
{
    public Guid TestSuiteId { get; set; }

    public int TotalGenerated { get; set; }

    public int PathMutationCount { get; set; }

    public int BodyMutationCount { get; set; }

    public int LlmSuggestionCount { get; set; }

    public int EndpointsCovered { get; set; }

    public string LlmModel { get; set; }

    public int? LlmTokensUsed { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public List<GeneratedTestCaseSummary> TestCases { get; set; } = new();
}
