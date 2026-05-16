---
name: messaging
description: "Skill for the Messaging area of GSP26SE43.ModularMonolith. 48 symbols across 25 files."
---

# Messaging

48 symbols | 25 files | Cohesion: 97%

## When to Use

- Working with code in `ClassifiedAds.Infrastructure/`
- Understanding how KafkaSender, RabbitMQSenderOptions, RabbitMQSender work
- Modifying messaging-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Infrastructure/Messaging/MessagingCollectionExtensions.cs` | AddAzureServiceBusSender, AddFakeSender, AddKafkaSender, AddRabbitMQSender, AddAzureServiceBusReceiver (+6) |
| `ClassifiedAds.Domain/Infrastructure/Messaging/MessageBus.cs` | AddConsumers, AddOutboxMessagePublishers, AddMessageBusConsumers, AddOutboxMessagePublishers, AddMessageBus (+3) |
| `ClassifiedAds.Infrastructure/Messaging/MessagingOptions.cs` | UsedRabbitMQ, UsedKafka, UsedAzureServiceBus, UsedFake |
| `ClassifiedAds.Domain/Infrastructure/Messaging/IMessageBus.cs` | IMessageBusMessage, IMessageBusEvent, IMessageBus |
| `ClassifiedAds.Domain/Infrastructure/Messaging/Message.cs` | SerializeObject, GetBytes |
| `ClassifiedAds.Domain/Infrastructure/Messaging/IMessageSender.cs` | IMessageSender |
| `ClassifiedAds.Infrastructure/Messaging/Kafka/KafkaSender.cs` | KafkaSender |
| `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSenderOptions.cs` | RabbitMQSenderOptions |
| `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSender.cs` | RabbitMQSender |
| `ClassifiedAds.Infrastructure/Messaging/Fake/FakeSender.cs` | FakeSender |

## Entry Points

Start here when exploring this area:

- **`KafkaSender`** (Class) — `ClassifiedAds.Infrastructure/Messaging/Kafka/KafkaSender.cs:9`
- **`RabbitMQSenderOptions`** (Class) — `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSenderOptions.cs:2`
- **`RabbitMQSender`** (Class) — `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSender.cs:11`
- **`FakeSender`** (Class) — `ClassifiedAds.Infrastructure/Messaging/Fake/FakeSender.cs:6`
- **`AzureServiceBusTopicSender`** (Class) — `ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/AzureServiceBusTopicSender.cs:9`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `KafkaSender` | Class | `ClassifiedAds.Infrastructure/Messaging/Kafka/KafkaSender.cs` | 9 |
| `RabbitMQSenderOptions` | Class | `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSenderOptions.cs` | 2 |
| `RabbitMQSender` | Class | `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQSender.cs` | 11 |
| `FakeSender` | Class | `ClassifiedAds.Infrastructure/Messaging/Fake/FakeSender.cs` | 6 |
| `AzureServiceBusTopicSender` | Class | `ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/AzureServiceBusTopicSender.cs` | 9 |
| `AzureServiceBusSender` | Class | `ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/AzureServiceBusSender.cs` | 9 |
| `KafkaReceiver` | Class | `ClassifiedAds.Infrastructure/Messaging/Kafka/KafkaReceiver.cs` | 9 |
| `RabbitMQReceiverOptions` | Class | `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQReceiverOptions.cs` | 2 |
| `RabbitMQReceiver` | Class | `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQReceiver.cs` | 16 |
| `FakeReceiver` | Class | `ClassifiedAds.Infrastructure/Messaging/Fake/FakeReceiver.cs` | 7 |
| `AzureServiceBusSubscriptionReceiver` | Class | `ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/AzureServiceBusSubscriptionReceiver.cs` | 10 |
| `AzureServiceBusReceiver` | Class | `ClassifiedAds.Infrastructure/Messaging/AzureServiceBus/AzureServiceBusReceiver.cs` | 10 |
| `RabbitMQHealthCheckOptions` | Class | `ClassifiedAds.Infrastructure/Messaging/RabbitMQ/RabbitMQHealthCheck.cs` | 41 |
| `FileUploadedEvent` | Class | `ClassifiedAds.Modules.Storage/DTOs/FileUploadedEvent.cs` | 5 |
| `FileDeletedEvent` | Class | `ClassifiedAds.Modules.Storage/DTOs/FileDeletedEvent.cs` | 5 |
| `MetaData` | Class | `ClassifiedAds.Domain/Infrastructure/Messaging/MetaData.cs` | 4 |
| `MessageBus` | Class | `ClassifiedAds.Domain/Infrastructure/Messaging/MessageBus.cs` | 14 |
| `IMessageSender` | Interface | `ClassifiedAds.Domain/Infrastructure/Messaging/IMessageSender.cs` | 5 |
| `IMessageReceiver` | Interface | `ClassifiedAds.Domain/Infrastructure/Messaging/IMessageReceiver.cs` | 6 |
| `IMessageBusMessage` | Interface | `ClassifiedAds.Domain/Infrastructure/Messaging/IMessageBus.cs` | 20 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Commands | 2 calls |

## How to Explore

1. `gitnexus_context({name: "KafkaSender"})` — see callers and callees
2. `gitnexus_query({query: "messaging"})` — find related execution flows
3. Read key files listed above for implementation details
