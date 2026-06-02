using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

public sealed class JsonPathCandidate
{
    public string Path { get; init; }

    public string Source { get; init; }

    public int Score { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = [];
}
