namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Controls algorithm families used in LLM-assisted generation flows.
/// All flags default to true to preserve current behavior.
/// </summary>
public class GenerationAlgorithmProfile
{
    public bool UseObservationConfirmationPrompting { get; set; } = true;

    public bool UseDependencyAwareOrdering { get; set; } = true;

    public bool UseSchemaRelationshipAnalysis { get; set; } = true;

    public bool UseSemanticTokenMatching { get; set; } = true;

    public bool UseFeedbackLoopContext { get; set; } = true;
}
