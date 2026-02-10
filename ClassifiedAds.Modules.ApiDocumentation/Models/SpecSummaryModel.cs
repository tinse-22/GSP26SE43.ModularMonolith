using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class SpecSummaryModel
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string SourceType { get; set; }

    public string Version { get; set; }

    public string ParseStatus { get; set; }

    public int EndpointCount { get; set; }
}
