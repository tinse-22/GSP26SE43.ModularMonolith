using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.LlmAssistant.Models;

public class FailureExplanationProviderResponse
{
    public string SummaryVi { get; set; }

    public IReadOnlyList<string> PossibleCauses { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> SuggestedNextActions { get; set; } = Array.Empty<string>();

    public string Confidence { get; set; }

    public string Model { get; set; }

    public int TokensUsed { get; set; }
}
