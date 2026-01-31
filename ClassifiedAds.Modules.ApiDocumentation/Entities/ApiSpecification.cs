using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Entities;

/// <summary>
/// API specification parsed from OpenAPI, Postman, or manually entered.
/// </summary>
public class ApiSpecification : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Project this specification belongs to.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Original file reference in Storage module.
    /// </summary>
    public Guid? OriginalFileId { get; set; }

    /// <summary>
    /// Specification name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Source type: OpenAPI, Postman, Manual, cURL.
    /// </summary>
    public SourceType SourceType { get; set; }

    /// <summary>
    /// API version string.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Whether this specification is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the specification was parsed.
    /// </summary>
    public DateTimeOffset? ParsedAt { get; set; }

    /// <summary>
    /// Parse status: Pending, Success, Failed.
    /// </summary>
    public ParseStatus ParseStatus { get; set; }

    /// <summary>
    /// JSON array of parse errors if any.
    /// </summary>
    public string ParseErrors { get; set; }

    // Navigation properties
    public Project Project { get; set; }
    public ICollection<ApiEndpoint> Endpoints { get; set; } = new List<ApiEndpoint>();
    public ICollection<SecurityScheme> SecuritySchemes { get; set; } = new List<SecurityScheme>();
}

public enum SourceType
{
    OpenAPI = 0,
    Postman = 1,
    Manual = 2,
    cURL = 3
}

public enum ParseStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}
