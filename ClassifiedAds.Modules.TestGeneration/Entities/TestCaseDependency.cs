using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Join entity representing a dependency between two test cases.
/// A test case can depend on multiple other test cases.
/// </summary>
public class TestCaseDependency : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// The test case that has the dependency.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// The test case that is depended upon.
    /// </summary>
    public Guid DependsOnTestCaseId { get; set; }

    // Navigation properties
    public TestCase TestCase { get; set; }
    public TestCase DependsOnTestCase { get; set; }
}
