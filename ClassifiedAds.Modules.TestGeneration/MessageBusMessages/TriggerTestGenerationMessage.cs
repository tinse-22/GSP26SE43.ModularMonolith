using ClassifiedAds.Domain.Infrastructure.Messaging;
using System;

namespace ClassifiedAds.Modules.TestGeneration.MessageBusMessages;

/// <summary>
/// Message to trigger n8n webhook for test generation in background.
/// Published to RabbitMQ when user requests test generation.
/// </summary>
public class TriggerTestGenerationMessage : IMessageBusCommand
{
    /// <summary>
    /// The generation job ID (for tracking and status updates).
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The test suite to generate tests for.
    /// </summary>
    public Guid TestSuiteId { get; set; }

    /// <summary>
    /// The approved proposal ID.
    /// </summary>
    public Guid ProposalId { get; set; }

    /// <summary>
    /// User who triggered the generation.
    /// </summary>
    public Guid TriggeredById { get; set; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
