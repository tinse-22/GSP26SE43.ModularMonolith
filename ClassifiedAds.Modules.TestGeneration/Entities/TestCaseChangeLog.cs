using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Audit trail for all TestCase modifications.
/// Records every change made to a test case for traceability.
/// </summary>
public class TestCaseChangeLog : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Reference to the TestCase that was changed.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// User who made the change.
    /// </summary>
    public Guid ChangedById { get; set; }

    /// <summary>
    /// Type of change made.
    /// </summary>
    public TestCaseChangeType ChangeType { get; set; }

    /// <summary>
    /// Name of the field that was changed (for field-level changes).
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    /// Previous value (JSON serialized for complex types).
    /// </summary>
    public string OldValue { get; set; }

    /// <summary>
    /// New value (JSON serialized for complex types).
    /// </summary>
    public string NewValue { get; set; }

    /// <summary>
    /// Optional reason for the change.
    /// </summary>
    public string ChangeReason { get; set; }

    /// <summary>
    /// Version number after this change.
    /// </summary>
    public int VersionAfterChange { get; set; }

    /// <summary>
    /// IP address of the user (for audit purposes).
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// User agent string (for audit purposes).
    /// </summary>
    public string UserAgent { get; set; }

    // Navigation properties
    public TestCase TestCase { get; set; }
}

/// <summary>
/// Types of changes that can be made to a TestCase.
/// </summary>
public enum TestCaseChangeType
{
    /// <summary>
    /// Test case was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Test case name changed.
    /// </summary>
    NameChanged = 1,

    /// <summary>
    /// Test case description changed.
    /// </summary>
    DescriptionChanged = 2,

    /// <summary>
    /// Execution order changed.
    /// </summary>
    OrderChanged = 3,

    /// <summary>
    /// Test type changed (HappyPath, Boundary, etc.).
    /// </summary>
    TestTypeChanged = 4,

    /// <summary>
    /// Priority changed.
    /// </summary>
    PriorityChanged = 5,

    /// <summary>
    /// Test case enabled/disabled.
    /// </summary>
    EnabledStatusChanged = 6,

    /// <summary>
    /// Dependency changed.
    /// </summary>
    DependencyChanged = 7,

    /// <summary>
    /// Request configuration changed.
    /// </summary>
    RequestChanged = 8,

    /// <summary>
    /// Expectation/assertion changed.
    /// </summary>
    ExpectationChanged = 9,

    /// <summary>
    /// Variables changed.
    /// </summary>
    VariablesChanged = 10,

    /// <summary>
    /// Tags changed.
    /// </summary>
    TagsChanged = 11,

    /// <summary>
    /// Test case was deleted.
    /// </summary>
    Deleted = 12,

    /// <summary>
    /// Test case was restored.
    /// </summary>
    Restored = 13,

    /// <summary>
    /// User customized the AI-suggested order.
    /// </summary>
    UserCustomizedOrder = 14
}
