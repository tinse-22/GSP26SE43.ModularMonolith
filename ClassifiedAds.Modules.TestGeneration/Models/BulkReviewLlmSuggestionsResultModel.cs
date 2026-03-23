using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Result model for FE-17 bulk review operations.
/// </summary>
public class BulkReviewLlmSuggestionsResultModel
{
    public Guid TestSuiteId { get; set; }

    public string Action { get; set; }

    public int MatchedCount { get; set; }

    public int ProcessedCount { get; set; }

    public int MaterializedCount { get; set; }

    public DateTimeOffset ReviewedAt { get; set; }

    public List<Guid> SuggestionIds { get; set; } = new();

    public List<Guid> AppliedTestCaseIds { get; set; } = new();
}
