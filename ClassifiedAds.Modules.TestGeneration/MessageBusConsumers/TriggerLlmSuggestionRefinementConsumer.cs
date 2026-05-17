using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.MessageBusConsumers;

/// <summary>
/// Background consumer that triggers n8n async refinement for LLM suggestion previews.
/// The API has already returned local draft suggestions before this runs.
/// </summary>
public class TriggerLlmSuggestionRefinementConsumer : IMessageBusConsumer<TriggerLlmSuggestionRefinementConsumer, TriggerLlmSuggestionRefinementMessage>
{
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IN8nIntegrationService _n8nService;
    private readonly ILogger<TriggerLlmSuggestionRefinementConsumer> _logger;

    public TriggerLlmSuggestionRefinementConsumer(
        IRepository<TestGenerationJob, Guid> jobRepository,
        IN8nIntegrationService n8nService,
        ILogger<TriggerLlmSuggestionRefinementConsumer> logger)
    {
        _jobRepository = jobRepository;
        _n8nService = n8nService;
        _logger = logger;
    }

    public async Task HandleAsync(
        TriggerLlmSuggestionRefinementMessage data,
        MetaData metaData,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var payloadBytes = data.Payload == null
            ? 0
            : JsonSerializer.SerializeToUtf8Bytes(data.Payload).Length;
        var queueDelayMs = metaData?.EnqueuedDateTime.HasValue == true
            ? (long)Math.Max(0, (DateTimeOffset.UtcNow - metaData.EnqueuedDateTime.Value).TotalMilliseconds)
            : data.CreatedAt == default
                ? 0
                : (long)Math.Max(0, (DateTimeOffset.UtcNow - data.CreatedAt).TotalMilliseconds);

        var job = await _jobRepository.FirstOrDefaultAsync(
            _jobRepository.GetQueryableSet().Where(x => x.Id == data.JobId));

        if (job == null)
        {
            _logger.LogWarning(
                "LLM suggestion refinement job not found. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}",
                data.JobId,
                data.TestSuiteId,
                data.JobId,
                queueDelayMs,
                payloadBytes);
            return;
        }

        if (job.Status != GenerationJobStatus.Queued)
        {
            _logger.LogWarning(
                "LLM suggestion refinement job is not queued. JobId={JobId}, BatchId={BatchId}, Status={Status}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}",
                job.Id,
                job.Id,
                job.Status,
                queueDelayMs,
                payloadBytes);
            return;
        }

        job.Status = GenerationJobStatus.Triggering;
        job.TriggeredAt = DateTimeOffset.UtcNow;
        job.RowVersion = Guid.NewGuid().ToByteArray();
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var webhookUrl = _n8nService.GetResolvedWebhookUrl(data.WebhookName);

            job.WebhookName = data.WebhookName;
            job.WebhookUrl = webhookUrl;
            job.CallbackUrl = data.CallbackUrl;
            job.Status = GenerationJobStatus.WaitingForCallback;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Triggering async LLM suggestion refinement. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, WebhookName={WebhookName}, CallbackUrl={CallbackUrl}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}, EndpointCount={EndpointCount}, RetryCount={RetryCount}",
                job.Id,
                data.TestSuiteId,
                job.Id,
                data.WebhookName,
                data.CallbackUrl,
                queueDelayMs,
                payloadBytes,
                data.Payload?.Endpoints?.Count ?? 0,
                job.RetryCount);

            var webhookStopwatch = Stopwatch.StartNew();
            var result = await _n8nService.TriggerWebhookWithResultAsync(
                data.WebhookName,
                data.Payload,
                cancellationToken);
            webhookStopwatch.Stop();

            if (result.Success)
            {
                totalStopwatch.Stop();
                _logger.LogInformation(
                    "Async LLM suggestion refinement accepted by n8n. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, WebhookAcceptMs={WebhookAcceptMs}, TotalTriggerMs={TotalTriggerMs}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}, EndpointCount={EndpointCount}, RetryCount={RetryCount}",
                    job.Id,
                    data.TestSuiteId,
                    job.Id,
                    webhookStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    queueDelayMs,
                    payloadBytes,
                    data.Payload?.Endpoints?.Count ?? 0,
                    job.RetryCount);
                return;
            }

            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = result.ErrorMessage;
            job.ErrorDetails = result.ErrorDetails;
            job.RetryCount++;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "Async LLM suggestion refinement trigger failed. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, WebhookAcceptMs={WebhookAcceptMs}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}, EndpointCount={EndpointCount}, RetryCount={RetryCount}, Error={Error}",
                job.Id,
                data.TestSuiteId,
                job.Id,
                webhookStopwatch.ElapsedMilliseconds,
                queueDelayMs,
                payloadBytes,
                data.Payload?.Endpoints?.Count ?? 0,
                job.RetryCount,
                result.ErrorMessage);
        }
        catch (Exception ex)
        {
            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = $"Lỗi không mong đợi khi trigger n8n refinement: {ex.Message}";
            job.ErrorDetails = ex.ToString();
            job.RetryCount++;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Unexpected error triggering async LLM suggestion refinement. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, QueueDelayMs={QueueDelayMs}, PayloadBytes={PayloadBytes}, EndpointCount={EndpointCount}, RetryCount={RetryCount}",
                job.Id,
                data.TestSuiteId,
                job.Id,
                queueDelayMs,
                payloadBytes,
                data.Payload?.Endpoints?.Count ?? 0,
                job.RetryCount);

            throw;
        }
    }
}
