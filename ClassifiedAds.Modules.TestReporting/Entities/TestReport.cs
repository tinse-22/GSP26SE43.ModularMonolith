using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestReporting.Entities;

/// <summary>
/// Generated test report file.
/// </summary>
public class TestReport : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test run this report is for.
    /// </summary>
    public Guid TestRunId { get; set; }

    /// <summary>
    /// User who generated this report.
    /// </summary>
    public Guid GeneratedById { get; set; }

    /// <summary>
    /// Reference to the generated file in Storage module.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Report type: Summary, Detailed, Coverage.
    /// </summary>
    public ReportType ReportType { get; set; }

    /// <summary>
    /// Report format: PDF, CSV, JSON, HTML.
    /// </summary>
    public ReportFormat Format { get; set; }

    /// <summary>
    /// When the report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// Optional expiration time for auto-deletion.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

public enum ReportType
{
    Summary = 0,
    Detailed = 1,
    Coverage = 2
}

public enum ReportFormat
{
    PDF = 0,
    CSV = 1,
    JSON = 2,
    HTML = 3
}
