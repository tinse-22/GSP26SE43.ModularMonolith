using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Tracks the status of a test generation job triggered via n8n webhook.
/// Enables true async behavior: controller returns 202 immediately,
/// background process triggers n8n, and callback updates job status.
/// </summary>
public class TestGenerationJob : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// The test suite this generation job belongs to.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// The test order proposal used for generation.
    /// </summary>
    public Guid? ProposalId { get; set; }

    /// <summary>
    /// Current status of the generation job.
    /// </summary>
    public GenerationJobStatus Status { get; set; }

    /// <summary>
    /// User who triggered the generation.
    /// </summary>
    public Guid TriggeredById { get; set; }

    /// <summary>
    /// When the job was queued for processing.
    /// </summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>
    /// When the n8n webhook was actually triggered.
    /// </summary>
    public DateTimeOffset? TriggeredAt { get; set; }

    /// <summary>
    /// When the job completed (success or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Number of test cases generated (set on success).
    /// </summary>
    public int? TestCasesGenerated { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Error details/stack trace for debugging.
    /// </summary>
    public string ErrorDetails { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The webhook name used for this job.
    /// </summary>
    public string WebhookName { get; set; }

    /// <summary>
    /// The resolved webhook URL (for debugging).
    /// </summary>
    public string WebhookUrl { get; set; }

    /// <summary>
    /// The callback URL sent to n8n.
    /// </summary>
    public string CallbackUrl { get; set; }

    // Navigation
    public TestSuite TestSuite { get; set; }
}

/// <summary>
/// Status of a test generation job.
/// </summary>
public enum GenerationJobStatus
{
    /// <summary>
    /// Job is queued and waiting to be processed.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// n8n webhook is being triggered.
    /// </summary>
    Triggering = 1,

    /// <summary>
    /// n8n webhook was triggered, waiting for callback.
    /// </summary>
    WaitingForCallback = 2,

    /// <summary>
    /// Generation completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Generation failed (timeout, n8n error, etc).
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Job was cancelled by user.
    /// </summary>
    Cancelled = 5
}
