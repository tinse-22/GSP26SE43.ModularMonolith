using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

/// <summary>
/// Processes the Phase 1.5 callback from n8n after SRS requirement refinement is complete.
/// Updates RefinedConstraints, RefinedConfidenceScore, RefinementRound and marks IsReviewed = true.
/// </summary>
public class ProcessSrsRefinementCallbackCommand : ICommand
{
    public Guid JobId { get; set; }

    public List<N8nSrsRefinedRequirement> RefinedRequirements { get; set; } = new();
}

public class ProcessSrsRefinementCallbackCommandHandler : ICommandHandler<ProcessSrsRefinementCallbackCommand>
{
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly ILogger<ProcessSrsRefinementCallbackCommandHandler> _logger;

    public ProcessSrsRefinementCallbackCommandHandler(
        IRepository<SrsAnalysisJob, Guid> jobRepository,
        IRepository<SrsRequirement, Guid> requirementRepository,
        ILogger<ProcessSrsRefinementCallbackCommandHandler> logger)
    {
        _jobRepository = jobRepository;
        _requirementRepository = requirementRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ProcessSrsRefinementCallbackCommand command, CancellationToken cancellationToken = default)
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
                "SrsAnalysisJob {JobId} is already in terminal state {Status}. Ignoring refinement callback.",
                job.Id, job.Status);
            return;
        }

        foreach (var refinedDto in command.RefinedRequirements)
        {
            var req = await _requirementRepository.FirstOrDefaultAsync(
                _requirementRepository.GetQueryableSet().Where(x => x.Id == refinedDto.RequirementId));

            if (req == null)
            {
                _logger.LogWarning(
                    "SrsRequirement {RequirementId} not found during refinement callback. JobId={JobId}",
                    refinedDto.RequirementId, command.JobId);
                continue;
            }

            req.RefinedConstraints = refinedDto.RefinedConstraints;
            req.RefinedConfidenceScore = refinedDto.RefinedConfidenceScore;
            req.RefinementRound += 1;
            req.IsReviewed = true;
            req.ReviewedAt = DateTimeOffset.UtcNow;

            await _requirementRepository.UpdateAsync(req, cancellationToken);
        }

        await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Complete the job
        job.Status = SrsAnalysisJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.RequirementsExtracted = command.RefinedRequirements.Count;
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SRS refinement callback processed. JobId={JobId}, RefinedCount={Count}",
            job.Id, command.RefinedRequirements.Count);
    }
}

// Refinement callback payload models
public class SrsRefinementCallbackRequest
{
    [JsonPropertyName("refinedRequirements")]
    public List<N8nSrsRefinedRequirement> RefinedRequirements { get; set; } = new();
}

public class N8nSrsRefinedRequirement
{
    [JsonPropertyName("requirementId")]
    public Guid RequirementId { get; set; }

    [JsonPropertyName("requirementCode")]
    public string RequirementCode { get; set; }

    [JsonPropertyName("refinedConstraints")]
    public string RefinedConstraints { get; set; }

    [JsonPropertyName("refinedConfidenceScore")]
    public float RefinedConfidenceScore { get; set; }

    [JsonPropertyName("refinementSummary")]
    public string RefinementSummary { get; set; }
}
