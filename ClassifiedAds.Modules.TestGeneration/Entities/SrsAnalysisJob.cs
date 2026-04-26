using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Tracks the async LLM analysis pipeline for an SrsDocument.
/// Mirrors the TestGenerationJob pattern: controller returns 202 immediately,
/// background process triggers n8n SRS analysis webhook, callback updates status.
/// </summary>
public class SrsAnalysisJob : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// The SRS document being analyzed.
    /// </summary>
    public Guid SrsDocumentId { get; set; }

    /// <summary>
    /// Current pipeline status.
    /// </summary>
    public SrsAnalysisJobStatus Status { get; set; }

    /// <summary>
    /// User who triggered the analysis.
    /// </summary>
    public Guid TriggeredById { get; set; }

    /// <summary>
    /// When the job was enqueued.
    /// </summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>
    /// When the n8n webhook was actually called.
    /// </summary>
    public DateTimeOffset? TriggeredAt { get; set; }

    /// <summary>
    /// When the job completed (success or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Number of SrsRequirements extracted (set on success).
    /// </summary>
    public int? RequirementsExtracted { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Error details or stack trace for debugging.
    /// </summary>
    public string ErrorDetails { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The type of analysis job: InitialAnalysis (Phase 1) or ClarificationRefinement (Phase 1.5).
    /// </summary>
    public SrsAnalysisJobType JobType { get; set; }

    // Navigation property
    public SrsDocument SrsDocument { get; set; }
}

/// <summary>
/// Distinguishes the two LLM pipeline phases for SRS analysis.
/// </summary>
public enum SrsAnalysisJobType
{
    /// <summary>Phase 1 — initial document analysis and requirement extraction.</summary>
    InitialAnalysis = 0,

    /// <summary>Phase 1.5 — re-analysis after user has answered clarification questions.</summary>
    ClarificationRefinement = 1
}

/// <summary>
/// Status of an SRS analysis job.
/// </summary>
public enum SrsAnalysisJobStatus
{
    /// <summary>Job is queued, not yet triggered.</summary>
    Queued = 0,

    /// <summary>n8n webhook is being triggered.</summary>
    Triggering = 1,

    /// <summary>n8n is processing the SRS document.</summary>
    Processing = 2,

    /// <summary>Analysis completed successfully.</summary>
    Completed = 3,

    /// <summary>Analysis failed.</summary>
    Failed = 4
}
