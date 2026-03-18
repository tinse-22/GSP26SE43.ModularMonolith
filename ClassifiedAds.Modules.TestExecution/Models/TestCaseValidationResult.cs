using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestCaseValidationResult
{
    public bool IsPassed { get; set; }

    public bool StatusCodeMatched { get; set; }

    public bool? SchemaMatched { get; set; }

    public bool? HeaderChecksPassed { get; set; }

    public bool? BodyContainsPassed { get; set; }

    public bool? BodyNotContainsPassed { get; set; }

    public bool? JsonPathChecksPassed { get; set; }

    public bool? ResponseTimePassed { get; set; }

    public List<ValidationFailureModel> Failures { get; set; } = new();
}
