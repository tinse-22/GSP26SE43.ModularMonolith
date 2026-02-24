using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Builds structured prompts using the Observation-Confirmation pattern for LLM-based test expectation generation.
/// Source: COmbine/RBCTest paper (arXiv:2504.17287) Section 3 - Observation-Confirmation Prompting.
///
/// Purpose: Reduce LLM hallucination when generating test expectations from OAS.
///
/// Two-phase approach:
/// Phase 1 (Observation): LLM lists all constraints it observes from the API spec.
/// Phase 2 (Confirmation): LLM confirms each constraint with evidence from the spec.
///   Only confirmed constraints become test expectations.
///
/// This dramatically reduces false positives compared to single-shot prompting.
/// </summary>
public interface IObservationConfirmationPromptBuilder
{
    /// <summary>
    /// Build a complete Observation-Confirmation prompt for a single endpoint.
    /// </summary>
    ObservationConfirmationPrompt BuildForEndpoint(EndpointPromptContext context);

    /// <summary>
    /// Build prompts for multiple endpoints in dependency order.
    /// Includes cross-endpoint context (e.g., "POST /users creates the user consumed by GET /users/{id}").
    /// </summary>
    IReadOnlyList<ObservationConfirmationPrompt> BuildForSequence(
        IReadOnlyList<EndpointPromptContext> orderedEndpoints);
}
