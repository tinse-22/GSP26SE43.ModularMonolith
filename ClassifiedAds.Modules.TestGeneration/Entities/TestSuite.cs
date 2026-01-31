using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Test suite containing multiple test cases.
/// </summary>
public class TestSuite : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Project this test suite belongs to.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional reference to API specification.
    /// </summary>
    public Guid? ApiSpecId { get; set; }

    /// <summary>
    /// Test suite name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Test suite description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Generation type: Auto, Manual, LLMAssisted.
    /// </summary>
    public GenerationType GenerationType { get; set; }

    /// <summary>
    /// Status: Draft, Ready, Archived.
    /// </summary>
    public TestSuiteStatus Status { get; set; }

    /// <summary>
    /// User who created this test suite.
    /// </summary>
    public Guid CreatedById { get; set; }

    // Navigation properties
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}

public enum GenerationType
{
    Auto = 0,
    Manual = 1,
    LLMAssisted = 2
}

public enum TestSuiteStatus
{
    Draft = 0,
    Ready = 1,
    Archived = 2
}
