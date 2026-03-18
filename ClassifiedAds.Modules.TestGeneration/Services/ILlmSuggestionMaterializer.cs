using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Materializes LLM suggestions into TestCase domain entities.
/// Shared between FE-06 (direct generation) and FE-15 (review-approve).
/// </summary>
public interface ILlmSuggestionMaterializer
{
    /// <summary>
    /// Materializes an LlmSuggestedScenario (from the LLM pipeline) into a TestCase entity.
    /// Used by FE-06 BoundaryNegativeTestCaseGenerator.
    /// </summary>
    TestCase MaterializeFromScenario(
        LlmSuggestedScenario scenario,
        Guid testSuiteId,
        ApiOrderItemModel orderItem,
        int orderIndex);

    /// <summary>
    /// Materializes a persisted LlmSuggestion entity into a TestCase entity.
    /// Used by FE-15 approve flow.
    /// </summary>
    TestCase MaterializeFromSuggestion(
        LlmSuggestion suggestion,
        ApiOrderItemModel orderItem,
        int orderIndex);

    /// <summary>
    /// Materializes a persisted LlmSuggestion using user-modified content.
    /// Used by FE-15 modify-and-approve flow.
    /// </summary>
    TestCase MaterializeFromModifiedContent(
        LlmSuggestion suggestion,
        EditableLlmSuggestionInput modified,
        ApiOrderItemModel orderItem,
        int orderIndex);
}
