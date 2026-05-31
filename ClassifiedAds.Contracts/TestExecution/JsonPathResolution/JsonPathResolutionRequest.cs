using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

public sealed class JsonPathResolutionRequest
{
    public string OriginalPath { get; init; }

    public string ActualResponseJson { get; init; }

    public IReadOnlyCollection<string> SwaggerResponseSchemas { get; init; } = [];

    public string HttpMethod { get; init; }

    public string EndpointPath { get; init; }
}
