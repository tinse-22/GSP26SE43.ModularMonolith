using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class TriggerSrsAnalysisCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>Output: the created job ID.</summary>
    public Guid JobId { get; set; }
}

public class TriggerSrsAnalysisCommandHandler : ICommandHandler<TriggerSrsAnalysisCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IN8nIntegrationService _n8nService;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<TriggerSrsAnalysisCommandHandler> _logger;

    private const string WebhookName = "analyze-srs";

    public TriggerSrsAnalysisCommandHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsAnalysisJob, Guid> jobRepository,
        IRepository<TestSuite, Guid> suiteRepository,
        IApiEndpointMetadataService endpointMetadataService,
        IN8nIntegrationService n8nService,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<TriggerSrsAnalysisCommandHandler> logger)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _jobRepository = jobRepository;
        _suiteRepository = suiteRepository;
        _endpointMetadataService = endpointMetadataService;
        _n8nService = n8nService;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _logger = logger;
    }

    public async Task HandleAsync(TriggerSrsAnalysisCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        // Create the analysis job
        var job = new SrsAnalysisJob
        {
            SrsDocumentId = command.SrsDocumentId,
            Status = SrsAnalysisJobStatus.Queued,
            JobType = SrsAnalysisJobType.InitialAnalysis,
            TriggeredById = command.CurrentUserId,
            QueuedAt = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _jobRepository.AddAsync(job, cancellationToken);

        // Update document status
        doc.AnalysisStatus = SrsAnalysisStatus.Processing;

        await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.JobId = job.Id;

        // Build n8n payload
        var endpoints = await GetEndpointsAsync(doc, cancellationToken);
        var rawContent = doc.ParsedMarkdown ?? doc.RawContent ?? string.Empty;
        var callbackUrl = $"{_n8nOptions.BeBaseUrl}/api/srs-analysis-callback/{job.Id}";

        var payload = new N8nSrsAnalysisPayload
        {
            SrsDocumentId = doc.Id,
            JobId = job.Id,
            RawContent = rawContent,
            ProjectContext = $"ProjectId: {doc.ProjectId}",
            Endpoints = endpoints,
            CallbackUrl = callbackUrl,
            CallbackApiKey = _n8nOptions.CallbackApiKey,
        };

        // Trigger n8n (n8n accepts quickly then processes async and calls back)
        job.Status = SrsAnalysisJobStatus.Triggering;
        job.TriggeredAt = DateTimeOffset.UtcNow;
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        var result = await _n8nService.TriggerWebhookWithResultAsync(WebhookName, payload, cancellationToken);

        if (result.Success)
        {
            job.Status = SrsAnalysisJobStatus.Processing;
            _logger.LogInformation(
                "SRS analysis job triggered. JobId={JobId}, SrsDocumentId={SrsDocumentId}",
                job.Id, command.SrsDocumentId);
        }
        else
        {
            job.Status = SrsAnalysisJobStatus.Failed;
            job.ErrorMessage = result.ErrorMessage;
            job.CompletedAt = DateTimeOffset.UtcNow;
            doc.AnalysisStatus = SrsAnalysisStatus.Failed;
            await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);

            _logger.LogError(
                "Failed to trigger SRS analysis via n8n. JobId={JobId}, Error={Error}",
                job.Id, result.ErrorMessage);
        }

        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<N8nSrsEndpointRef>> GetEndpointsAsync(SrsDocument doc, CancellationToken cancellationToken)
    {
        if (doc.TestSuiteId == null)
        {
            return new List<N8nSrsEndpointRef>();
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == doc.TestSuiteId.Value));

        if (suite?.ApiSpecId == null)
        {
            return new List<N8nSrsEndpointRef>();
        }

        try
        {
            var metadata = await _endpointMetadataService.GetEndpointMetadataAsync(
                suite.ApiSpecId.Value,
                suite.SelectedEndpointIds?.Count > 0
                    ? (IReadOnlyCollection<Guid>)suite.SelectedEndpointIds
                    : null,
                cancellationToken);

            return metadata.Select(e => new N8nSrsEndpointRef
            {
                EndpointId = e.EndpointId,
                Method = e.HttpMethod,
                Path = e.Path,
                Summary = e.OperationId,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load endpoints for SRS analysis. SrsDocumentId={SrsDocumentId}", doc.Id);
            return new List<N8nSrsEndpointRef>();
        }
    }
}

// n8n payload models for Phase 1
public class N8nSrsAnalysisPayload
{
    [JsonPropertyName("srsDocumentId")]
    public Guid SrsDocumentId { get; set; }

    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("rawContent")]
    public string RawContent { get; set; }

    [JsonPropertyName("projectContext")]
    public string ProjectContext { get; set; }

    [JsonPropertyName("endpoints")]
    public List<N8nSrsEndpointRef> Endpoints { get; set; } = new();

    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("callbackApiKey")]
    public string CallbackApiKey { get; set; }
}

public class N8nSrsEndpointRef
{
    [JsonPropertyName("endpointId")]
    public Guid EndpointId { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}
