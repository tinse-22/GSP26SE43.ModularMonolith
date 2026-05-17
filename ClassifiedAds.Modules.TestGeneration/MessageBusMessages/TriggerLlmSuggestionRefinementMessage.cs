using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;

namespace ClassifiedAds.Modules.TestGeneration.MessageBusMessages;

/// <summary>
/// Message to trigger n8n LLM suggestion refinement after local draft suggestions are returned.
/// </summary>
public class TriggerLlmSuggestionRefinementMessage : IMessageBusCommand
{
    public Guid JobId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid TriggeredById { get; set; }

    public string WebhookName { get; set; }

    public string CallbackUrl { get; set; }

    public N8nBoundaryNegativePayload Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
