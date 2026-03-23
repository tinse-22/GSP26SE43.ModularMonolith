using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Shared review workflow for FE-15 single review and FE-17 bulk review.
/// Keeps materialization, audit, and suite versioning logic in one place.
/// </summary>
public interface ILlmSuggestionReviewService
{
    Task RejectAsync(
        LlmSuggestion suggestion,
        Guid currentUserId,
        string reviewNotes,
        CancellationToken cancellationToken = default);

    Task RejectManyAsync(
        IReadOnlyCollection<LlmSuggestion> suggestions,
        Guid currentUserId,
        string reviewNotes,
        CancellationToken cancellationToken = default);

    Task<LlmSuggestionApprovalBatchResult> ApproveManyAsync(
        TestSuite suite,
        IReadOnlyCollection<LlmSuggestionApprovalItem> approvals,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class LlmSuggestionApprovalItem
{
    public LlmSuggestion Suggestion { get; set; }

    public EditableLlmSuggestionInput ModifiedContent { get; set; }

    public string ReviewNotes { get; set; }
}

public class LlmSuggestionApprovalBatchResult
{
    public IReadOnlyList<LlmSuggestion> Suggestions { get; set; } = Array.Empty<LlmSuggestion>();

    public IReadOnlyList<TestCase> TestCases { get; set; } = Array.Empty<TestCase>();
}
