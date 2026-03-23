using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.LlmAssistant.Models;

public class FailureExplanationModel
{
    public Guid TestSuiteId { get; set; }

    public Guid TestRunId { get; set; }

    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string SummaryVi { get; set; }

    public IReadOnlyList<string> PossibleCauses { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> SuggestedNextActions { get; set; } = Array.Empty<string>();

    public string Confidence { get; set; }

    public string Source { get; set; }

    public string Provider { get; set; }

    public string Model { get; set; }

    public int TokensUsed { get; set; }

    public int LatencyMs { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public IReadOnlyList<string> FailureCodes { get; set; } = Array.Empty<string>();
}
