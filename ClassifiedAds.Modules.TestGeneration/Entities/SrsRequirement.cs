using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Individual requirement extracted by LLM from an SrsDocument.
/// Contains testable constraints, assumptions, and ambiguities for traceable test generation.
/// </summary>
public class SrsRequirement : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Parent SRS document.
    /// </summary>
    public Guid SrsDocumentId { get; set; }

    /// <summary>
    /// Auto-generated requirement code (e.g., REQ-001).
    /// Used as stable display identifier for traceability reports.
    /// </summary>
    public string RequirementCode { get; set; }

    /// <summary>
    /// Short title of the requirement.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Full requirement description as extracted from the SRS.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Category: Functional | NonFunctional | Security | Performance | Constraint.
    /// </summary>
    public SrsRequirementType RequirementType { get; set; }

    /// <summary>
    /// JSON array of testable constraint objects extracted by LLM.
    /// Schema: [{"constraint": "age >= 17 → 201", "priority": "High"}, ...]
    /// Stored as jsonb.
    /// </summary>
    public string TestableConstraints { get; set; }

    /// <summary>
    /// JSON array of explicit assumptions LLM identified in this requirement.
    /// Schema: ["email confirmation not required", ...]
    /// Stored as jsonb.
    /// </summary>
    public string Assumptions { get; set; }

    /// <summary>
    /// JSON array of ambiguities or unclear points LLM flagged.
    /// Schema: ["unclear: what happens if age is not provided?", ...]
    /// Stored as jsonb.
    /// </summary>
    public string Ambiguities { get; set; }

    /// <summary>
    /// LLM confidence score 0.0–1.0 for this requirement extraction.
    /// Low scores flag requirements needing human review.
    /// </summary>
    public float? ConfidenceScore { get; set; }

    /// <summary>
    /// Mapped endpoint UUID from ApiDocumentation module (nullable).
    /// Null if requirement applies globally or LLM could not resolve endpoint.
    /// </summary>
    public Guid? EndpointId { get; set; }

    /// <summary>
    /// Human-readable endpoint path for display (e.g., POST /api/users/register).
    /// Denormalized from ApiEndpoint at analysis time.
    /// </summary>
    public string MappedEndpointPath { get; set; }

    /// <summary>
    /// Display order within the document.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether a human has reviewed and confirmed this extracted requirement.
    /// </summary>
    public bool IsReviewed { get; set; }

    /// <summary>
    /// User who reviewed this requirement.
    /// </summary>
    public Guid? ReviewedById { get; set; }

    /// <summary>
    /// When the requirement was reviewed.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// LLM-refined constraints after the clarification round (Phase 1.5).
    /// If null, TestableConstraints from Phase 1 is used for generation.
    /// Schema: same as TestableConstraints — [{"constraint": "...", "priority": "High"}, ...]
    /// Stored as jsonb.
    /// </summary>
    public string RefinedConstraints { get; set; }

    /// <summary>
    /// Confidence score after the refinement round (Phase 1.5).
    /// Replaces ConfidenceScore once refinement completes.
    /// </summary>
    public float? RefinedConfidenceScore { get; set; }

    /// <summary>
    /// How many clarification refinement rounds have been completed (0 = never refined).
    /// </summary>
    public int RefinementRound { get; set; }

    // Navigation properties
    public SrsDocument SrsDocument { get; set; }
    public ICollection<TestCaseRequirementLink> TestCaseLinks { get; set; } = new List<TestCaseRequirementLink>();
    public ICollection<SrsRequirementClarification> Clarifications { get; set; } = new List<SrsRequirementClarification>();
}

/// <summary>
/// Category of a software requirement.
/// </summary>
public enum SrsRequirementType
{
    /// <summary>Business logic and use-case driven behavior.</summary>
    Functional = 0,

    /// <summary>Performance, scalability, availability.</summary>
    NonFunctional = 1,

    /// <summary>Authentication, authorization, data protection.</summary>
    Security = 2,

    /// <summary>Response time, throughput targets.</summary>
    Performance = 3,

    /// <summary>Hard constraint (e.g., format rules, business invariants).</summary>
    Constraint = 4
}
