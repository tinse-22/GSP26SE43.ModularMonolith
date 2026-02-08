using ClassifiedAds.CrossCuttingConcerns.ExtensionMethods;
using ClassifiedAds.Modules.Subscription.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Outbox;

internal static class OutboxMessageFactory
{
    public static OutboxMessage Create<TPayload>(
        string eventType,
        Guid triggeredById,
        Guid objectId,
        TPayload payload)
    {
        return Create(eventType, triggeredById, objectId.ToString(), payload);
    }

    public static OutboxMessage Create<TPayload>(
        string eventType,
        Guid triggeredById,
        string objectId,
        TPayload payload)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new ArgumentException("Object id is required.", nameof(objectId));
        }

        return new OutboxMessage
        {
            EventType = eventType,
            TriggeredById = triggeredById,
            ObjectId = objectId,
            Payload = payload.AsJsonString(),
        };
    }
}
