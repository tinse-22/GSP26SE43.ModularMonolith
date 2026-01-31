using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// AI-proposed test execution order for human review.
/// Users can approve, reject, or customize the proposed order.
/// </summary>
public class TestOrderProposal : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Reference to the TestSuite this proposal is for.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// Sequential proposal number for this suite.
    /// </summary>
    public int ProposalNumber { get; set; }

    /// <summary>
    /// Source of the proposal: AI, User, System.
    /// </summary>
    public ProposalSource Source { get; set; }

    /// <summary>
    /// Status: Pending, Approved, Rejected, Modified, Superseded.
    /// </summary>
    public ProposalStatus Status { get; set; }

    /// <summary>
    /// Proposed test execution order (JSON array of {TestCaseId, OrderIndex, Reason}).
    /// </summary>
    public string ProposedOrder { get; set; }

    /// <summary>
    /// AI's explanation of why this order was proposed.
    /// </summary>
    public string AiReasoning { get; set; }

    /// <summary>
    /// Factors considered by AI (dependencies, auth flow, data setup, etc.).
    /// </summary>
    public string ConsideredFactors { get; set; }

    /// <summary>
    /// User who reviewed this proposal.
    /// </summary>
    public Guid? ReviewedById { get; set; }

    /// <summary>
    /// When the proposal was reviewed.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// User's feedback or reason for approval/rejection.
    /// </summary>
    public string ReviewNotes { get; set; }

    /// <summary>
    /// User's modified order if they customized (JSON array).
    /// </summary>
    public string UserModifiedOrder { get; set; }

    /// <summary>
    /// Final order that was applied (JSON array).
    /// </summary>
    public string AppliedOrder { get; set; }

    /// <summary>
    /// When the order was applied to the suite.
    /// </summary>
    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>
    /// LLM model used for generating this proposal.
    /// </summary>
    public string LlmModel { get; set; }

    /// <summary>
    /// Tokens used for generating this proposal.
    /// </summary>
    public int? TokensUsed { get; set; }

    // Navigation properties
    public TestSuite TestSuite { get; set; }
}

/// <summary>
/// Source of the test order proposal.
/// </summary>
public enum ProposalSource
{
    /// <summary>
    /// AI/LLM generated proposal.
    /// </summary>
    Ai = 0,

    /// <summary>
    /// User created proposal manually.
    /// </summary>
    User = 1,

    /// <summary>
    /// System default (e.g., alphabetical, by endpoint).
    /// </summary>
    System = 2,

    /// <summary>
    /// Imported from previous test run.
    /// </summary>
    Imported = 3
}

/// <summary>
/// Status of the test order proposal.
/// </summary>
public enum ProposalStatus
{
    /// <summary>
    /// Waiting for human review.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Human approved the proposal as-is.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Human rejected the proposal.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Human modified the proposal and approved.
    /// </summary>
    ModifiedAndApproved = 3,

    /// <summary>
    /// Superseded by a newer proposal.
    /// </summary>
    Superseded = 4,

    /// <summary>
    /// Applied to the test suite.
    /// </summary>
    Applied = 5,

    /// <summary>
    /// Expired without review.
    /// </summary>
    Expired = 6
}
