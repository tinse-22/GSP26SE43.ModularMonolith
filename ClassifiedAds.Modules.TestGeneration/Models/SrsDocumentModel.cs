using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class SrsDocumentModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? TestSuiteId { get; set; }

    public string Title { get; set; }

    public SrsSourceType SourceType { get; set; }

    public string RawContent { get; set; }

    public Guid? StorageFileId { get; set; }

    public SrsAnalysisStatus AnalysisStatus { get; set; }

    public DateTimeOffset? AnalyzedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public List<SrsRequirementModel> Requirements { get; set; } = new();

    public static SrsDocumentModel FromEntity(SrsDocument doc, IEnumerable<SrsRequirementModel> requirements = null)
    {
        return new SrsDocumentModel
        {
            Id = doc.Id,
            ProjectId = doc.ProjectId,
            TestSuiteId = doc.TestSuiteId,
            Title = doc.Title,
            SourceType = doc.SourceType,
            RawContent = doc.RawContent,
            StorageFileId = doc.StorageFileId,
            AnalysisStatus = doc.AnalysisStatus,
            AnalyzedAt = doc.AnalyzedAt,
            CreatedById = doc.CreatedById,
            CreatedDateTime = doc.CreatedDateTime,
            UpdatedDateTime = doc.UpdatedDateTime,
            Requirements = requirements != null ? new List<SrsRequirementModel>(requirements) : new(),
        };
    }
}

public class SrsRequirementModel
{
    public Guid Id { get; set; }

    public Guid SrsDocumentId { get; set; }

    public string RequirementCode { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public SrsRequirementType RequirementType { get; set; }

    public string TestableConstraints { get; set; }

    public string Assumptions { get; set; }

    public string Ambiguities { get; set; }

    public float? ConfidenceScore { get; set; }

    public Guid? EndpointId { get; set; }

    public string MappedEndpointPath { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsReviewed { get; set; }

    public string RefinedConstraints { get; set; }

    public float? RefinedConfidenceScore { get; set; }

    public int RefinementRound { get; set; }

    public static SrsRequirementModel FromEntity(SrsRequirement req)
    {
        return new SrsRequirementModel
        {
            Id = req.Id,
            SrsDocumentId = req.SrsDocumentId,
            RequirementCode = req.RequirementCode,
            Title = req.Title,
            Description = req.Description,
            RequirementType = req.RequirementType,
            TestableConstraints = req.TestableConstraints,
            Assumptions = req.Assumptions,
            Ambiguities = req.Ambiguities,
            ConfidenceScore = req.ConfidenceScore,
            EndpointId = req.EndpointId,
            MappedEndpointPath = req.MappedEndpointPath,
            DisplayOrder = req.DisplayOrder,
            IsReviewed = req.IsReviewed,
            RefinedConstraints = req.RefinedConstraints,
            RefinedConfidenceScore = req.RefinedConfidenceScore,
            RefinementRound = req.RefinementRound,
        };
    }
}

public class SrsAnalysisJobModel
{
    public Guid Id { get; set; }

    public Guid SrsDocumentId { get; set; }

    public SrsAnalysisJobStatus Status { get; set; }

    public SrsAnalysisJobType JobType { get; set; }

    public Guid TriggeredById { get; set; }

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset? TriggeredAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? RequirementsExtracted { get; set; }

    public string ErrorMessage { get; set; }

    public static SrsAnalysisJobModel FromEntity(SrsAnalysisJob job)
    {
        return new SrsAnalysisJobModel
        {
            Id = job.Id,
            SrsDocumentId = job.SrsDocumentId,
            Status = job.Status,
            JobType = job.JobType,
            TriggeredById = job.TriggeredById,
            QueuedAt = job.QueuedAt,
            TriggeredAt = job.TriggeredAt,
            CompletedAt = job.CompletedAt,
            RequirementsExtracted = job.RequirementsExtracted,
            ErrorMessage = job.ErrorMessage,
        };
    }
}

public class SrsRequirementClarificationModel
{
    public Guid Id { get; set; }

    public Guid SrsRequirementId { get; set; }

    public string AmbiguitySource { get; set; }

    public string Question { get; set; }

    public string SuggestedOptions { get; set; }

    public string UserAnswer { get; set; }

    public bool IsAnswered { get; set; }

    public bool IsCritical { get; set; }

    public int DisplayOrder { get; set; }

    public DateTimeOffset? AnsweredAt { get; set; }

    public Guid? AnsweredById { get; set; }

    public static SrsRequirementClarificationModel FromEntity(SrsRequirementClarification clar)
    {
        return new SrsRequirementClarificationModel
        {
            Id = clar.Id,
            SrsRequirementId = clar.SrsRequirementId,
            AmbiguitySource = clar.AmbiguitySource,
            Question = clar.Question,
            SuggestedOptions = clar.SuggestedOptions,
            UserAnswer = clar.UserAnswer,
            IsAnswered = clar.IsAnswered,
            IsCritical = clar.IsCritical,
            DisplayOrder = clar.DisplayOrder,
            AnsweredAt = clar.AnsweredAt,
            AnsweredById = clar.AnsweredById,
        };
    }
}

public class TraceabilityMatrix
{
    public Guid TestSuiteId { get; set; }

    public Guid? SrsDocumentId { get; set; }

    public List<TraceabilityRequirementRow> Requirements { get; set; } = new();

    public int TotalRequirements { get; set; }

    public int CoveredRequirements { get; set; }

    public int UncoveredRequirements { get; set; }

    public double CoveragePercent { get; set; }
}

public class TraceabilityRequirementRow
{
    public Guid RequirementId { get; set; }

    public string RequirementCode { get; set; }

    public string Title { get; set; }

    public SrsRequirementType RequirementType { get; set; }

    public float? ConfidenceScore { get; set; }

    public bool IsReviewed { get; set; }

    public bool IsCovered { get; set; }

    public List<TraceabilityTestCaseRef> TestCases { get; set; } = new();
}

public class TraceabilityTestCaseRef
{
    public Guid TestCaseId { get; set; }

    public string TestCaseName { get; set; }

    public float? TraceabilityScore { get; set; }

    public string MappingRationale { get; set; }
}
