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
    /// Snapshot of selected endpoint IDs for this test suite scope (stored as jsonb).
    /// </summary>
    public List<Guid> SelectedEndpointIds { get; set; } = new();

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

    /// <summary>
    /// Approval status for AI-generated test order: PendingReview, Approved, Rejected.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; }

    /// <summary>
    /// User who approved the test order.
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// When the test order was approved.
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Current version number (incremented on each change).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Last user who modified this test suite.
    /// </summary>
    public Guid? LastModifiedById { get; set; }

    // Navigation properties
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
    public ICollection<TestSuiteVersion> Versions { get; set; } = new List<TestSuiteVersion>();
    public ICollection<TestOrderProposal> OrderProposals { get; set; } = new List<TestOrderProposal>();
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

/// <summary>
/// Approval status for AI-generated content.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Not applicable (manual creation).
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// AI generated, waiting for human review.
    /// </summary>
    PendingReview = 1,

    /// <summary>
    /// Human approved the AI suggestion.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Human rejected the AI suggestion.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Human modified the AI suggestion.
    /// </summary>
    ModifiedAndApproved = 4
}
