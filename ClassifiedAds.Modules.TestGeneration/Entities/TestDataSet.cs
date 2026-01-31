using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Data set for data-driven testing.
/// </summary>
public class TestDataSet : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test case this data set belongs to.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// Data set name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Data as JSON object for parameter substitution.
    /// </summary>
    public string Data { get; set; }

    /// <summary>
    /// Whether this data set is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // Navigation properties
    public TestCase TestCase { get; set; }
}
