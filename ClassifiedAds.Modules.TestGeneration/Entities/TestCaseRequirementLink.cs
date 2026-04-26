using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Traceability link between a TestCase and the SrsRequirement it covers.
/// Enables requirement-to-test-case coverage reports.
/// Created automatically when LLM generates tests from SRS context,
/// or manually by users in the review UI.
/// </summary>
public class TestCaseRequirementLink : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// The test case that covers the requirement.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// The requirement being covered.
    /// </summary>
    public Guid SrsRequirementId { get; set; }

    /// <summary>
    /// LLM confidence 0.0–1.0 that this test case truly covers the requirement.
    /// Null when the link was created manually by a user.
    /// </summary>
    public float? TraceabilityScore { get; set; }

    /// <summary>
    /// LLM-generated rationale explaining why this test case covers the requirement.
    /// Null when the link was created manually.
    /// </summary>
    public string MappingRationale { get; set; }

    // Navigation properties
    public TestCase TestCase { get; set; }
    public SrsRequirement SrsRequirement { get; set; }
}
