using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

public sealed class JsonPathResolutionOptions
{
    public List<string> WrapperNames { get; set; } = new();

    public Dictionary<string, List<string>> FieldAliases { get; set; } = new();
}
