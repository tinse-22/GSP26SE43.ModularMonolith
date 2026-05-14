using System;
using System.Collections.Generic;

namespace ClassifiedAds.WebAPI.Models;

public class AdminApiDocumentationProjectModel
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string OwnerName { get; set; }

    public string OwnerEmail { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string BaseUrl { get; set; }

    public string Status { get; set; }

    public Guid? ActiveSpecId { get; set; }

    public string ActiveSpecName { get; set; }

    public int TotalSpecifications { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public List<AdminApiDocumentationSpecModel> Specifications { get; set; } = new();
}

public class AdminApiDocumentationSpecModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string SourceType { get; set; }

    public string Version { get; set; }

    public bool IsActive { get; set; }

    public string ParseStatus { get; set; }

    public DateTimeOffset? ParsedAt { get; set; }

    public int EndpointCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}
