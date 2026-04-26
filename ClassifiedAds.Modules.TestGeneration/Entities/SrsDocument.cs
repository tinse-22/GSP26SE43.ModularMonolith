using ClassifiedAds.Domain.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Software Requirements Specification (SRS) document uploaded or entered by the user.
/// Provides structured requirement input for LLM-driven test case generation.
/// </summary>
public class SrsDocument : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Project this SRS document belongs to.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional link to a test suite this document is associated with.
    /// Set after the user chooses which suite to generate tests for.
    /// </summary>
    public Guid? TestSuiteId { get; set; }

    /// <summary>
    /// User-provided title for this SRS document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// How the document was provided: TextInput | FileUpload | Url.
    /// </summary>
    public SrsSourceType SourceType { get; set; }

    /// <summary>
    /// Raw content as entered by the user (for TextInput source type).
    /// Stored as text; may be plain text, Markdown, or structured prose.
    /// </summary>
    public string RawContent { get; set; }

    /// <summary>
    /// Reference to the uploaded file in the Storage module (for FileUpload source type).
    /// </summary>
    public Guid? StorageFileId { get; set; }

    /// <summary>
    /// Normalized/parsed Markdown representation after file extraction.
    /// Populated by file parsing pipeline before LLM analysis.
    /// </summary>
    public string ParsedMarkdown { get; set; }

    /// <summary>
    /// Analysis pipeline status: Pending | Processing | Completed | Failed.
    /// </summary>
    public SrsAnalysisStatus AnalysisStatus { get; set; }

    /// <summary>
    /// When the LLM analysis completed.
    /// </summary>
    public DateTimeOffset? AnalyzedAt { get; set; }

    /// <summary>
    /// User who created this SRS document.
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Whether this document has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this document was soft-deleted.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<SrsRequirement> Requirements { get; set; } = new List<SrsRequirement>();
    public ICollection<SrsAnalysisJob> AnalysisJobs { get; set; } = new List<SrsAnalysisJob>();
}

/// <summary>
/// How the SRS document content was provided.
/// </summary>
public enum SrsSourceType
{
    /// <summary>User typed or pasted text directly into the UI.</summary>
    TextInput = 0,

    /// <summary>User uploaded a file (PDF, DOCX, TXT, MD).</summary>
    FileUpload = 1,

    /// <summary>User provided a URL to a publicly accessible document.</summary>
    Url = 2
}

/// <summary>
/// Current LLM analysis pipeline status for an SRS document.
/// </summary>
public enum SrsAnalysisStatus
{
    /// <summary>Document created, not yet analyzed.</summary>
    Pending = 0,

    /// <summary>LLM analysis job is running.</summary>
    Processing = 1,

    /// <summary>Requirements successfully extracted.</summary>
    Completed = 2,

    /// <summary>Analysis failed; see SrsAnalysisJob.ErrorMessage.</summary>
    Failed = 3
}
