using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.MessageBusConsumers;

/// <summary>
/// Background consumer that triggers n8n webhook for test generation.
/// This decouples the API request from the actual n8n trigger,
/// allowing the API to return 202 Accepted immediately.
/// </summary>
public class TriggerTestGenerationConsumer : IMessageBusConsumer<TriggerTestGenerationConsumer, TriggerTestGenerationMessage>
{
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly ITestGenerationPayloadBuilder _payloadBuilder;
    private readonly IN8nIntegrationService _n8nService;
    private readonly ILogger<TriggerTestGenerationConsumer> _logger;

    public TriggerTestGenerationConsumer(
        IRepository<TestGenerationJob, Guid> jobRepository,
        ITestGenerationPayloadBuilder payloadBuilder,
        IN8nIntegrationService n8nService,
        ILogger<TriggerTestGenerationConsumer> logger)
    {
        _jobRepository = jobRepository;
        _payloadBuilder = payloadBuilder;
        _n8nService = n8nService;
        _logger = logger;
    }

    public async Task HandleAsync(
        TriggerTestGenerationMessage data,
        MetaData metaData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing test generation message. JobId={JobId}, TestSuiteId={TestSuiteId}, ProposalId={ProposalId}",
            data.JobId, data.TestSuiteId, data.ProposalId);

        // Load the job
        var job = await _jobRepository.FirstOrDefaultAsync(
            _jobRepository.GetQueryableSet().Where(x => x.Id == data.JobId));

        if (job == null)
        {
            _logger.LogWarning(
                "TestGenerationJob not found. JobId={JobId}, TestSuiteId={TestSuiteId}",
                data.JobId, data.TestSuiteId);
            return;
        }

        // Check if job is still in Queued state (not already processed or cancelled)
        if (job.Status != GenerationJobStatus.Queued)
        {
            _logger.LogWarning(
                "TestGenerationJob is not in Queued state. JobId={JobId}, Status={Status}",
                data.JobId, job.Status);
            return;
        }

        // Update job to Triggering
        job.Status = GenerationJobStatus.Triggering;
        job.TriggeredAt = DateTimeOffset.UtcNow;
        job.RowVersion = Guid.NewGuid().ToByteArray();
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            // Build the payload
            var payload = await _payloadBuilder.BuildPayloadAsync(
                data.TestSuiteId,
                data.ProposalId,
                cancellationToken);

            // Resolve webhook URL for logging
            var webhookUrl = _n8nService.GetResolvedWebhookUrl(_payloadBuilder.WebhookName);

            // Mark the job as waiting before invoking n8n. Some workflows POST the callback
            // before returning their trigger response, so the callback handler must be able
            // to find a WaitingForCallback job immediately.
            job.WebhookName = _payloadBuilder.WebhookName;
            job.WebhookUrl = webhookUrl;
            job.CallbackUrl = payload.CallbackUrl;
            job.Status = GenerationJobStatus.WaitingForCallback;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Triggering n8n webhook. JobId={JobId}, WebhookName={WebhookName}, WebhookUrl={WebhookUrl}, CallbackUrl={CallbackUrl}",
                data.JobId, _payloadBuilder.WebhookName, webhookUrl, payload.CallbackUrl);

            // Trigger the webhook using the new result-based method
            var result = await _n8nService.TriggerWebhookWithResultAsync(
                _payloadBuilder.WebhookName,
                payload,
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "n8n webhook triggered successfully. JobId={JobId}, TestSuiteId={TestSuiteId}. Waiting for callback.",
                    data.JobId, data.TestSuiteId);
            }
            else
            {
                // Failed - update job with error
                job.Status = GenerationJobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.ErrorMessage = result.ErrorMessage;
                job.ErrorDetails = result.ErrorDetails;
                job.RetryCount++;
                job.RowVersion = Guid.NewGuid().ToByteArray();
                await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

                var errorType = result.IsTimeout ? "timeout" : result.IsNetworkError ? "network" : "webhook";
                _logger.LogError(
                    "n8n webhook trigger failed ({ErrorType}). JobId={JobId}, TestSuiteId={TestSuiteId}, Error={Error}",
                    errorType, data.JobId, data.TestSuiteId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Unexpected error - update job
            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = $"Lỗi không mong đợi: {ex.Message}";
            job.ErrorDetails = ex.ToString();
            job.RetryCount++;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Unexpected error triggering n8n webhook. JobId={JobId}, TestSuiteId={TestSuiteId}",
                data.JobId, data.TestSuiteId);

            throw;
        }
    }
}
