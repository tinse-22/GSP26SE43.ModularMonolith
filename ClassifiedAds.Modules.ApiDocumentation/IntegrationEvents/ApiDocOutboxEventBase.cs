using System;

namespace ClassifiedAds.Modules.ApiDocumentation.IntegrationEvents;

public abstract class ApiDocOutboxEventBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string Version { get; set; } = "1.0";
}
