using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class TriggerSrsRefinementCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>Output: the created refinement job ID.</summary>
    public Guid JobId { get; set; }
}

public class TriggerSrsRefinementCommandHandler : ICommandHandler<TriggerSrsRefinementCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsRequirementClarification, Guid> _clarificationRepository;
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;
    private readonly IN8nIntegrationService _n8nService;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<TriggerSrsRefinementCommandHandler> _logger;

    private const string WebhookName = "refine-srs-requirements";

    public TriggerSrsRefinementCommandHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsRequirementClarification, Guid> clarificationRepository,
        IRepository<SrsAnalysisJob, Guid> jobRepository,
        IN8nIntegrationService n8nService,
        IOptions<N8nIntegrationOptions> n8nOptions,
        Dispatcher dispatcher,
        ILogger<TriggerSrsRefinementCommandHandler> logger)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _requirementRepository = requirementRepository;
        _clarificationRepository = clarificationRepository;
        _jobRepository = jobRepository;
        _n8nService = n8nService;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task HandleAsync(TriggerSrsRefinementCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        var req = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == command.RequirementId && x.SrsDocumentId == command.SrsDocumentId));

        if (req == null)
        {
            throw new NotFoundException($"SrsRequirement {command.RequirementId} khong tim thay.");
        }

        // Validate: all critical clarifications must be answered
        var clarifications = await _clarificationRepository.ToListAsync(
            _clarificationRepository.GetQueryableSet()
                .Where(x => x.SrsRequirementId == command.RequirementId));

        var unansweredCritical = clarifications.Any(x => x.IsCritical && !x.IsAnswered);
        if (unansweredCritical)
        {
            throw new ValidationException(
                "Can phai tra loi tat ca cac cau hoi critical truoc khi refine requirement.");
        }

        // Create refinement job
        var job = new SrsAnalysisJob
        {
            SrsDocumentId = command.SrsDocumentId,
            Status = SrsAnalysisJobStatus.Queued,
            JobType = SrsAnalysisJobType.ClarificationRefinement,
            TriggeredById = command.CurrentUserId,
            QueuedAt = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.JobId = job.Id;

        // Build payload
        var userAnswers = clarifications
            .Where(c => c.IsAnswered)
            .Select(c => new N8nSrsUserAnswer
            {
                ClarificationId = c.Id,
                Question = c.Question,
                AmbiguitySource = c.AmbiguitySource,
                UserAnswer = c.UserAnswer,
            })
            .ToList();

        var requirementToRefine = new N8nSrsRequirementToRefine
        {
            RequirementId = req.Id,
            RequirementCode = req.RequirementCode,
            OriginalDescription = req.Description,
            ExistingTestableConstraints = req.TestableConstraints,
            ExistingAmbiguities = req.Ambiguities,
            OriginalConfidenceScore = req.ConfidenceScore ?? 0,
            UserAnswers = userAnswers,
        };

        var payload = new N8nSrsRefinementPayload
        {
            SrsDocumentId = doc.Id,
            JobId = job.Id,
            RequirementsToRefine = new List<N8nSrsRequirementToRefine> { requirementToRefine },
        };

        // Call n8n synchronously — wait for full result (like LLM suggestion / analyze-srs flow)
        job.Status = SrsAnalysisJobStatus.Triggering;
        job.TriggeredAt = DateTimeOffset.UtcNow;
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        SrsRefinementCallbackRequest callbackData;
        try
        {
            callbackData = await _n8nService.TriggerWebhookAsync<N8nSrsRefinementPayload, SrsRefinementCallbackRequest>(
                WebhookName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            // For refinement, the local fallback is always safe: it merges user answers into
            // constraints locally. Fall back for any n8n failure (404, 500/502/503/504, bad JSON,
            // missing webhook config, network errors, etc.) so the endpoint never returns 400
            // due to infrastructure unavailability.
            _logger.LogWarning(
                ex,
                "SRS refinement n8n call failed — applying local fallback. JobId={JobId}, Error={Error}",
                job.Id, ex.Message);
            await ApplyLocalFallbackAsync(job, req, clarifications, cancellationToken);
            return;
        }

        // n8n returned results — process them via the callback handler
        await _dispatcher.DispatchAsync(new ProcessSrsRefinementCallbackCommand
        {
            JobId = job.Id,
            RefinedRequirements = callbackData?.RefinedRequirements ?? new List<N8nSrsRefinedRequirement>(),
        }, cancellationToken);

        _logger.LogInformation(
            "SRS refinement completed synchronously. JobId={JobId}, RequirementId={RequirementId}",
            job.Id, command.RequirementId);
    }

    private async Task ApplyLocalFallbackAsync(
        SrsAnalysisJob job,
        SrsRequirement req,
        IReadOnlyCollection<SrsRequirementClarification> clarifications,
        CancellationToken cancellationToken)
    {
        var refinedRequirement = new N8nSrsRefinedRequirement
        {
            RequirementId = req.Id,
            RequirementCode = req.RequirementCode,
            RefinedConstraints = BuildRefinedConstraints(req, clarifications),
            RefinedConfidenceScore = Math.Min(1f, Math.Max(req.ConfidenceScore ?? 0.65f, 0.65f) + (clarifications.Count * 0.1f)),
            RefinementSummary = "Local fallback refinement applied because the SRS refinement webhook is unavailable.",
        };

        _logger.LogWarning(
            "SRS refinement webhook is unavailable. Falling back to local refinement. JobId={JobId}, RequirementId={RequirementId}",
            job.Id,
            req.Id);

        await _dispatcher.DispatchAsync(new ProcessSrsRefinementCallbackCommand
        {
            JobId = job.Id,
            RefinedRequirements = new List<N8nSrsRefinedRequirement> { refinedRequirement },
        }, cancellationToken);
    }


    private static string BuildRefinedConstraints(
        SrsRequirement requirement,
        IReadOnlyCollection<SrsRequirementClarification> clarifications)
    {
        var refined = TryDeserializeConstraints(requirement.RefinedConstraints)
            ?? TryDeserializeConstraints(requirement.TestableConstraints)
            ?? new List<Dictionary<string, string>>();

        if (refined.Count == 0)
        {
            refined.Add(new Dictionary<string, string>
            {
                ["constraint"] = requirement.Description ?? requirement.Title,
                ["priority"] = "High",
            });
        }

        foreach (var clarification in clarifications.Where(x => x.IsAnswered))
        {
            refined.Add(new Dictionary<string, string>
            {
                ["constraint"] = $"Clarified: {clarification.Question} => {clarification.UserAnswer}",
                ["priority"] = clarification.IsCritical ? "High" : "Medium",
            });
        }

        return JsonSerializer.Serialize(refined);
    }

    private static List<Dictionary<string, string>> TryDeserializeConstraints(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

// n8n payload models for Phase 1.5
public class N8nSrsRefinementPayload
{
    [JsonPropertyName("srsDocumentId")]
    public Guid SrsDocumentId { get; set; }

    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("requirementsToRefine")]
    public List<N8nSrsRequirementToRefine> RequirementsToRefine { get; set; } = new();

}

public class N8nSrsRequirementToRefine
{
    [JsonPropertyName("requirementId")]
    public Guid RequirementId { get; set; }

    [JsonPropertyName("requirementCode")]
    public string RequirementCode { get; set; }

    [JsonPropertyName("originalDescription")]
    public string OriginalDescription { get; set; }

    [JsonPropertyName("existingTestableConstraints")]
    public string ExistingTestableConstraints { get; set; }

    [JsonPropertyName("existingAmbiguities")]
    public string ExistingAmbiguities { get; set; }

    [JsonPropertyName("originalConfidenceScore")]
    public float OriginalConfidenceScore { get; set; }

    [JsonPropertyName("userAnswers")]
    public List<N8nSrsUserAnswer> UserAnswers { get; set; } = new();
}

public class N8nSrsUserAnswer
{
    [JsonPropertyName("clarificationId")]
    public Guid ClarificationId { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; }

    [JsonPropertyName("ambiguitySource")]
    public string AmbiguitySource { get; set; }

    [JsonPropertyName("userAnswer")]
    public string UserAnswer { get; set; }
}
