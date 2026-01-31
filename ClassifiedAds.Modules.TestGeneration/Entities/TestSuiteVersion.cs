using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Version history for TestSuite changes.
/// Stores snapshots when significant changes occur.
/// </summary>
public class TestSuiteVersion : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Reference to the parent TestSuite.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// Version number at the time of snapshot.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// User who made this version change.
    /// </summary>
    public Guid ChangedById { get; set; }

    /// <summary>
    /// Type of change: Created, TestOrderChanged, TestCasesModified, etc.
    /// </summary>
    public VersionChangeType ChangeType { get; set; }

    /// <summary>
    /// Description of what changed.
    /// </summary>
    public string ChangeDescription { get; set; }

    /// <summary>
    /// Snapshot of test case order at this version (JSON array of TestCaseId with OrderIndex).
    /// </summary>
    public string TestCaseOrderSnapshot { get; set; }

    /// <summary>
    /// Snapshot of approval status at this version.
    /// </summary>
    public ApprovalStatus ApprovalStatusSnapshot { get; set; }

    /// <summary>
    /// Previous version data for comparison (JSON).
    /// </summary>
    public string PreviousState { get; set; }

    /// <summary>
    /// New version data (JSON).
    /// </summary>
    public string NewState { get; set; }

    // Navigation properties
    public TestSuite TestSuite { get; set; }
}

/// <summary>
/// Types of changes that create a new version.
/// </summary>
public enum VersionChangeType
{
    /// <summary>
    /// Initial creation.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Test execution order was changed.
    /// </summary>
    TestOrderChanged = 1,

    /// <summary>
    /// Test cases were added or removed.
    /// </summary>
    TestCasesModified = 2,

    /// <summary>
    /// Suite settings (name, description, etc.) changed.
    /// </summary>
    SettingsChanged = 3,

    /// <summary>
    /// Approval status changed.
    /// </summary>
    ApprovalChanged = 4,

    /// <summary>
    /// AI proposed a new test order.
    /// </summary>
    AiOrderProposed = 5,

    /// <summary>
    /// User customized the AI-proposed order.
    /// </summary>
    UserOrderCustomized = 6,

    /// <summary>
    /// Suite was archived.
    /// </summary>
    Archived = 7,

    /// <summary>
    /// Suite was restored from archive.
    /// </summary>
    Restored = 8
}
