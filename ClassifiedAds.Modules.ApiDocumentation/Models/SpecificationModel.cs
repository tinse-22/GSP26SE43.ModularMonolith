using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class SpecificationModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string SourceType { get; set; }

    public string Version { get; set; }

    public bool IsActive { get; set; }

    public string ParseStatus { get; set; }

    public DateTimeOffset? ParsedAt { get; set; }

    public Guid? OriginalFileId { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
