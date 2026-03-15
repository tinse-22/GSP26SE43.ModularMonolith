using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseRunResultModel
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public int OrderIndex { get; set; }

    public string Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public long DurationMs { get; set; }

    public string ResolvedUrl { get; set; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();

    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    public string ResponseBodyPreview { get; set; }

    public List<ValidationFailureModel> FailureReasons { get; set; } = new();

    public Dictionary<string, string> ExtractedVariables { get; set; } = new();

    public List<Guid> DependencyIds { get; set; } = new();

    public List<Guid> SkippedBecauseDependencyIds { get; set; } = new();

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }
}
