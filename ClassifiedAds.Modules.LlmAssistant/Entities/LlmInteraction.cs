using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.LlmAssistant.Entities;

/// <summary>
/// Record of LLM API interaction.
/// </summary>
public class LlmInteraction : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User who made the interaction.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of interaction: ScenarioSuggestion, FailureExplanation, DocParsing.
    /// </summary>
    public InteractionType InteractionType { get; set; }

    /// <summary>
    /// Context sent to LLM.
    /// </summary>
    public string InputContext { get; set; }

    /// <summary>
    /// Response from LLM.
    /// </summary>
    public string LlmResponse { get; set; }

    /// <summary>
    /// Model used (e.g., "gpt-4", "claude-3").
    /// </summary>
    public string ModelUsed { get; set; }

    /// <summary>
    /// Number of tokens used.
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Response latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }
}

public enum InteractionType
{
    ScenarioSuggestion = 0,
    FailureExplanation = 1,
    DocumentationParsing = 2
}
