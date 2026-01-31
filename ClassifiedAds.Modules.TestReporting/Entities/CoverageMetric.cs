using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestReporting.Entities;

/// <summary>
/// API coverage metrics for a test run.
/// </summary>
public class CoverageMetric : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test run this metric is for.
    /// </summary>
    public Guid TestRunId { get; set; }

    /// <summary>
    /// Total number of API endpoints.
    /// </summary>
    public int TotalEndpoints { get; set; }

    /// <summary>
    /// Number of endpoints that were tested.
    /// </summary>
    public int TestedEndpoints { get; set; }

    /// <summary>
    /// Coverage percentage (0-100).
    /// </summary>
    public decimal CoveragePercent { get; set; }

    /// <summary>
    /// Coverage breakdown by HTTP method as JSON.
    /// Example: {"GET": 90, "POST": 85, "PUT": 70}
    /// </summary>
    public string ByMethod { get; set; }

    /// <summary>
    /// Coverage breakdown by tag as JSON.
    /// Example: {"users": 100, "orders": 75}
    /// </summary>
    public string ByTag { get; set; }

    /// <summary>
    /// List of uncovered paths as JSON array.
    /// </summary>
    public string UncoveredPaths { get; set; }

    /// <summary>
    /// When the coverage was calculated.
    /// </summary>
    public DateTimeOffset CalculatedAt { get; set; }
}
