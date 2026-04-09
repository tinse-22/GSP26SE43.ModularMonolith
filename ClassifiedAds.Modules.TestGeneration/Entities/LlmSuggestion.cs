using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Durable pending LLM suggestion for review before materializing into a TestCase.
/// Part of FE-15: LLM Suggestion Review workflow.
/// </summary>
public class LlmSuggestion : Entity<Guid>, IAggregateRoot
{
    public Guid TestSuiteId { get; set; }

    public Guid? EndpointId { get; set; }

    public string CacheKey { get; set; }

    public int DisplayOrder { get; set; }

    public LlmSuggestionType SuggestionType { get; set; }

    public TestType TestType { get; set; }

    public string SuggestedName { get; set; }

    public string SuggestedDescription { get; set; }

    /// <summary>
    /// Serialized N8nTestCaseRequest JSON (jsonb).
    /// </summary>
    public string SuggestedRequest { get; set; }

    /// <summary>
    /// Serialized N8nTestCaseExpectation JSON (jsonb).
    /// </summary>
    public string SuggestedExpectation { get; set; }

    /// <summary>
    /// Serialized List&lt;N8nTestCaseVariable&gt; JSON (jsonb).
    /// </summary>
    public string SuggestedVariables { get; set; }

    /// <summary>
    /// Serialized List&lt;string&gt; tags JSON (jsonb).
    /// </summary>
    public string SuggestedTags { get; set; }

    public TestPriority Priority { get; set; }

    public ReviewStatus ReviewStatus { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string ReviewNotes { get; set; }

    /// <summary>
    /// Serialized modified content JSON when user modifies before approving (jsonb).
    /// </summary>
    public string ModifiedContent { get; set; }

    /// <summary>
    /// ID of the TestCase created when this suggestion was approved.
    /// </summary>
    public Guid? AppliedTestCaseId { get; set; }

    public string LlmModel { get; set; }

    public int? TokensUsed { get; set; }

    // Navigation
    public TestSuite TestSuite { get; set; }
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    ModifiedAndApproved = 3,
    Superseded = 4,
}

public enum LlmSuggestionType
{
    BoundaryNegative = 0,
    HappyPath = 1,
    Security = 2,
    Performance = 3,
}
