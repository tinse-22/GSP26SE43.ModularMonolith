using System;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class ProjectModel
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string BaseUrl { get; set; }

    public string Status { get; set; }

    public Guid? ActiveSpecId { get; set; }

    public string ActiveSpecName { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
