using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Individual test case definition.
/// </summary>
public class TestCase : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test suite this test case belongs to.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// Optional reference to API endpoint being tested.
    /// </summary>
    public Guid? EndpointId { get; set; }

    /// <summary>
    /// Test case name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Test case description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Test type: HappyPath, Boundary, Negative.
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// Priority: Critical, High, Medium, Low.
    /// </summary>
    public TestPriority Priority { get; set; }

    /// <summary>
    /// Whether this test case is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Dependencies on other test cases (many-to-many via join table).
    /// </summary>
    public ICollection<TestCaseDependency> Dependencies { get; set; } = new List<TestCaseDependency>();

    /// <summary>
    /// Execution order within the suite.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// User-customized order (NULL = use AI/default order).
    /// </summary>
    public int? CustomOrderIndex { get; set; }

    /// <summary>
    /// Whether the order was customized by user (vs AI-suggested).
    /// </summary>
    public bool IsOrderCustomized { get; set; }

    /// <summary>
    /// Tags for categorization (stored as JSON array).
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Last user who modified this test case.
    /// </summary>
    public Guid? LastModifiedById { get; set; }

    /// <summary>
    /// Current version number.
    /// </summary>
    public int Version { get; set; } = 1;

    // Navigation properties
    public TestSuite TestSuite { get; set; }
    public TestCaseRequest Request { get; set; }
    public TestCaseExpectation Expectation { get; set; }
    public ICollection<TestCaseVariable> Variables { get; set; } = new List<TestCaseVariable>();
    public ICollection<TestDataSet> DataSets { get; set; } = new List<TestDataSet>();
    public ICollection<TestCaseChangeLog> ChangeLogs { get; set; } = new List<TestCaseChangeLog>();
}

public enum TestType
{
    HappyPath = 0,
    Boundary = 1,
    Negative = 2,
    Performance = 3,
    Security = 4
}

public enum TestPriority
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3
}
