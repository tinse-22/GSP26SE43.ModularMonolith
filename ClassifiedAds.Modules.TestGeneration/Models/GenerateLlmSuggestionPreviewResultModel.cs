using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Result model for LLM suggestion preview generation (FE-15).
/// </summary>
public class GenerateLlmSuggestionPreviewResultModel
{
    public Guid TestSuiteId { get; set; }
    public int TotalSuggestions { get; set; }
    public int EndpointsCovered { get; set; }
    public string LlmModel { get; set; }
    public int? LlmTokensUsed { get; set; }
    public bool FromCache { get; set; }
    public string Source { get; set; }
    public string RefinementStatus { get; set; }
    public Guid? RefinementJobId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public List<LlmSuggestionModel> Suggestions { get; set; } = new ();
}
