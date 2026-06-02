using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

public sealed class JsonPathResolutionResult
{
    public string OriginalPath { get; init; }

    public string ResolvedPath { get; init; }

    public decimal Confidence { get; init; }

    public string ResolutionStrategy { get; init; }

    public string Source { get; init; }

    public bool IsResolved { get; init; }

    public bool IsAmbiguous { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public IReadOnlyList<JsonPathCandidate> CandidatePaths { get; init; } = [];
}
