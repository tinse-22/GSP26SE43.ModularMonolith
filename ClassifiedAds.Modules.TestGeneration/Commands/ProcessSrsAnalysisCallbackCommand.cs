using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

/// <summary>
/// Processes the Phase 1 callback from n8n after SRS analysis is complete.
/// Creates SrsRequirement records and SrsRequirementClarification records from LLM output.
/// </summary>
public class ProcessSrsAnalysisCallbackCommand : ICommand
{
    public Guid JobId { get; set; }

    public List<N8nSrsRequirementResult> Requirements { get; set; } = new();

    public List<N8nSrsClarificationQuestion> ClarificationQuestions { get; set; } = new();

    /// <summary>Non-null when n8n reports an internal workflow error.</summary>
    public string ErrorMessage { get; set; }
}

public class ProcessSrsAnalysisCallbackCommandHandler : ICommandHandler<ProcessSrsAnalysisCallbackCommand>
{
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsRequirementClarification, Guid> _clarificationRepository;
    private readonly ILogger<ProcessSrsAnalysisCallbackCommandHandler> _logger;

    public ProcessSrsAnalysisCallbackCommandHandler(
        IRepository<SrsAnalysisJob, Guid> jobRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsRequirementClarification, Guid> clarificationRepository,
        ILogger<ProcessSrsAnalysisCallbackCommandHandler> logger)
    {
        _jobRepository = jobRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _requirementRepository = requirementRepository;
        _clarificationRepository = clarificationRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ProcessSrsAnalysisCallbackCommand command, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.FirstOrDefaultAsync(
            _jobRepository.GetQueryableSet().Where(x => x.Id == command.JobId));

        if (job == null)
        {
            throw new NotFoundException($"SrsAnalysisJob {command.JobId} khong tim thay.");
        }

        if (job.Status == SrsAnalysisJobStatus.Completed || job.Status == SrsAnalysisJobStatus.Failed)
        {
            _logger.LogWarning(
                "SrsAnalysisJob {JobId} is already in terminal state {Status}. Ignoring callback.",
                job.Id, job.Status);
            return;
        }

        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet().Where(x => x.Id == job.SrsDocumentId));
        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {job.SrsDocumentId} khong tim thay.");
        }

        // If n8n reports an error, mark job as Failed immediately
        if (!string.IsNullOrWhiteSpace(command.ErrorMessage))
        {
            job.Status = SrsAnalysisJobStatus.Failed;
            job.ErrorMessage = command.ErrorMessage;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            doc.AnalysisStatus = SrsAnalysisStatus.Failed;
            await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
            await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "SRS analysis reported n8n error. JobId={JobId}, Error={Error}",
                job.Id, command.ErrorMessage);
            return;
        }

        // Map clarification questions by requirementCode for lookup
        var normalizedRequirements = NormalizeRequirements(command.Requirements);
        var clarificationsByCode = command.ClarificationQuestions
            .Where(c => !string.IsNullOrWhiteSpace(c?.RequirementCode) && !string.IsNullOrWhiteSpace(c?.Question))
            .GroupBy(c => c.RequirementCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Replace-snapshot mode: clear old requirements and clarifications for this SRS doc
        // so each analysis result is consistent and does not accumulate stale data.
        var existingRequirements = await _requirementRepository.ToListAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.SrsDocumentId == job.SrsDocumentId));
        if (existingRequirements.Count > 0)
        {
            var existingRequirementIds = existingRequirements.Select(x => x.Id).ToHashSet();
            var existingClarifications = await _clarificationRepository.ToListAsync(
                _clarificationRepository.GetQueryableSet()
                    .Where(x => existingRequirementIds.Contains(x.SrsRequirementId)));
            if (existingClarifications.Count > 0)
            {
                await _clarificationRepository.BulkDeleteAsync(existingClarifications, cancellationToken);
            }

            await _requirementRepository.BulkDeleteAsync(existingRequirements, cancellationToken);
            await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (normalizedRequirements.Count == 0)
        {
            job.Status = SrsAnalysisJobStatus.Failed;
            job.ErrorMessage = "SRS analysis produced no valid requirements after quality filtering.";
            job.CompletedAt = DateTimeOffset.UtcNow;
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            doc.AnalysisStatus = SrsAnalysisStatus.Failed;
            await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
            await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        int requirementsExtracted = 0;

        for (int i = 0; i < normalizedRequirements.Count; i++)
        {
            var reqDto = normalizedRequirements[i];

            var req = new SrsRequirement
            {
                SrsDocumentId = job.SrsDocumentId,
                RequirementCode = reqDto.RequirementCode,
                Title = reqDto.Title,
                Description = reqDto.Description,
                RequirementType = ParseRequirementType(reqDto.Type),
                TestableConstraints = reqDto.TestableConstraints,
                Assumptions = reqDto.Assumptions,
                Ambiguities = reqDto.Ambiguities,
                ConfidenceScore = reqDto.ConfidenceScore,
                MappedEndpointPath = reqDto.MappedEndpointPath,
                DisplayOrder = i + 1,
                IsReviewed = false,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };

            await _requirementRepository.AddAsync(req, cancellationToken);
            await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            requirementsExtracted++;

            // Create clarification questions for this requirement
            if (clarificationsByCode.TryGetValue(reqDto.RequirementCode, out var clarifications))
            {
                for (int j = 0; j < clarifications.Count; j++)
                {
                    var clarDto = clarifications[j];
                    var clar = new SrsRequirementClarification
                    {
                        SrsRequirementId = req.Id,
                        AmbiguitySource = clarDto.AmbiguitySource,
                        Question = clarDto.Question,
                        SuggestedOptions = clarDto.SuggestedOptions,
                        IsCritical = clarDto.IsCritical,
                        DisplayOrder = j + 1,
                        IsAnswered = false,
                        RowVersion = Guid.NewGuid().ToByteArray(),
                    };

                    await _clarificationRepository.AddAsync(clar, cancellationToken);
                }

                await _clarificationRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // Update job status
        job.Status = SrsAnalysisJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.RequirementsExtracted = requirementsExtracted;
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Update document status
        doc.AnalysisStatus = SrsAnalysisStatus.Completed;
        doc.AnalyzedAt = DateTimeOffset.UtcNow;
        await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
        await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SRS analysis callback processed. JobId={JobId}, RequirementsExtracted={Count}, RawRequirements={RawCount}",
            job.Id, requirementsExtracted, command.Requirements.Count);
    }

    private static List<N8nSrsRequirementResult> NormalizeRequirements(List<N8nSrsRequirementResult> requirements)
    {
        if (requirements == null || requirements.Count == 0)
        {
            return new List<N8nSrsRequirementResult>();
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<N8nSrsRequirementResult>();

        for (var i = 0; i < requirements.Count; i++)
        {
            var dto = requirements[i];
            if (dto == null)
            {
                continue;
            }

            var title = (dto.Title ?? string.Empty).Trim();
            var description = (dto.Description ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = description.Length > 120 ? description[..120] : description;
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                description = title;
            }

            var requirementCode = string.IsNullOrWhiteSpace(dto.RequirementCode)
                ? $"REQ-{i + 1:000}"
                : dto.RequirementCode.Trim();

            var key = $"{title}|{description}";
            if (!seenKeys.Add(key))
            {
                continue;
            }

            var testableConstraints = string.IsNullOrWhiteSpace(dto.TestableConstraints)
                ? JsonSerializer.Serialize(new[]
                {
                    new Dictionary<string, string>
                    {
                        ["constraint"] = description,
                        ["priority"] = "Medium",
                    },
                })
                : dto.TestableConstraints.Trim();

            var assumptions = string.IsNullOrWhiteSpace(dto.Assumptions) ? "[]" : dto.Assumptions.Trim();
            var ambiguities = string.IsNullOrWhiteSpace(dto.Ambiguities) ? "[]" : dto.Ambiguities.Trim();
            var confidenceScore = Math.Clamp(dto.ConfidenceScore, 0f, 1f);

            result.Add(new N8nSrsRequirementResult
            {
                RequirementCode = requirementCode,
                Title = title,
                Description = description,
                Type = string.IsNullOrWhiteSpace(dto.Type) ? "Functional" : dto.Type.Trim(),
                TestableConstraints = testableConstraints,
                Assumptions = assumptions,
                Ambiguities = ambiguities,
                ConfidenceScore = confidenceScore,
                MappedEndpointPath = string.IsNullOrWhiteSpace(dto.MappedEndpointPath)
                    ? null
                    : dto.MappedEndpointPath.Trim(),
            });
        }

        return result;
    }

    private static SrsRequirementType ParseRequirementType(string type)
    {
        return type?.ToLowerInvariant() switch
        {
            "functional" => SrsRequirementType.Functional,
            "nonfunctional" or "non-functional" => SrsRequirementType.NonFunctional,
            "security" => SrsRequirementType.Security,
            "performance" => SrsRequirementType.Performance,
            "constraint" => SrsRequirementType.Constraint,
            _ => SrsRequirementType.Functional,
        };
    }
}

// Callback request payload models
public class SrsAnalysisCallbackRequest
{
    [JsonPropertyName("requirements")]
    public List<N8nSrsRequirementResult> Requirements { get; set; } = new();

    [JsonPropertyName("clarificationQuestions")]
    public List<N8nSrsClarificationQuestion> ClarificationQuestions { get; set; } = new();

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; }
}

public class N8nSrsRequirementResult
{
    [JsonPropertyName("requirementCode")]
    public string RequirementCode { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("testableConstraints")]
    public string TestableConstraints { get; set; }

    [JsonPropertyName("assumptions")]
    public string Assumptions { get; set; }

    [JsonPropertyName("ambiguities")]
    public string Ambiguities { get; set; }

    [JsonPropertyName("confidenceScore")]
    public float ConfidenceScore { get; set; }

    [JsonPropertyName("mappedEndpointPath")]
    public string MappedEndpointPath { get; set; }
}

public class N8nSrsClarificationQuestion
{
    [JsonPropertyName("requirementCode")]
    public string RequirementCode { get; set; }

    [JsonPropertyName("ambiguitySource")]
    public string AmbiguitySource { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; }

    [JsonPropertyName("isCritical")]
    public bool IsCritical { get; set; }

    [JsonPropertyName("suggestedOptions")]
    public string SuggestedOptions { get; set; }
}
