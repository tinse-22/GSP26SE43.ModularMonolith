using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// Project containing API specifications and test configurations.
/// </summary>
public class Project : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User who owns this project.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Currently active API specification for this project.
    /// </summary>
    public Guid? ActiveSpecId { get; set; }

    /// <summary>
    /// Project name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Project description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Base URL for API calls.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Project status: Active, Archived.
    /// </summary>
    public ProjectStatus Status { get; set; }

    // Navigation properties
    public ApiSpecification ActiveSpec { get; set; }
    public ICollection<ApiSpecification> ApiSpecifications { get; set; } = new List<ApiSpecification>();
}

public enum ProjectStatus
{
    Active = 0,
    Archived = 1
}
