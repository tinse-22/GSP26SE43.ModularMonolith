---
name: entities
description: "Skill for the Entities area of GSP26SE43.ModularMonolith. 52 symbols across 34 files."
---

# Entities

52 symbols | 34 files | Cohesion: 45%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestGeneration/`
- Understanding how OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase work
- Modifying entities-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.Modules.TestExecution/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.Modules.Notification/Entities/SmsMessage.cs` | SmsMessage, ArchivedSmsMessage, SmsMessageBase |
| `ClassifiedAds.Modules.Notification/Entities/EmailMessage.cs` | EmailMessage, ArchivedEmailMessage, EmailMessageBase |
| `ClassifiedAds.Modules.LlmAssistant/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.Modules.Storage/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.Modules.Subscription/Entities/OutboxMessage.cs` | OutboxMessage, ArchivedOutboxMessage, OutboxMessageBase |
| `ClassifiedAds.UnitTests/Subscription/SubscriptionEntitiesTests.cs` | OutboxMessage_Should_SetProperties, OutboxMessage_Published_Should_DefaultToFalse |
| `ClassifiedAds.Modules.TestGeneration/Commands/ProcessSrsAnalysisCallbackCommand.cs` | HandleAsync, ParseRequirementType |

## Entry Points

Start here when exploring this area:

- **`OutboxMessage`** (Class) — `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs:5`
- **`ArchivedOutboxMessage`** (Class) — `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs:9`
- **`OutboxMessageBase`** (Class) — `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs:13`
- **`OutboxMessage`** (Class) — `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs:5`
- **`ArchivedOutboxMessage`** (Class) — `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs:9`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `OutboxMessage` | Class | `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs` | 5 |
| `ArchivedOutboxMessage` | Class | `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs` | 9 |
| `OutboxMessageBase` | Class | `ClassifiedAds.Modules.TestReporting/Entities/OutboxMessage.cs` | 13 |
| `OutboxMessage` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs` | 5 |
| `ArchivedOutboxMessage` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs` | 9 |
| `OutboxMessageBase` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/OutboxMessage.cs` | 13 |
| `OutboxMessage` | Class | `ClassifiedAds.Modules.TestExecution/Entities/OutboxMessage.cs` | 5 |
| `ArchivedOutboxMessage` | Class | `ClassifiedAds.Modules.TestExecution/Entities/OutboxMessage.cs` | 9 |
| `OutboxMessageBase` | Class | `ClassifiedAds.Modules.TestExecution/Entities/OutboxMessage.cs` | 13 |
| `SmsMessage` | Class | `ClassifiedAds.Modules.Notification/Entities/SmsMessage.cs` | 5 |
| `ArchivedSmsMessage` | Class | `ClassifiedAds.Modules.Notification/Entities/SmsMessage.cs` | 9 |
| `SmsMessageBase` | Class | `ClassifiedAds.Modules.Notification/Entities/SmsMessage.cs` | 13 |
| `EmailMessage` | Class | `ClassifiedAds.Modules.Notification/Entities/EmailMessage.cs` | 6 |
| `ArchivedEmailMessage` | Class | `ClassifiedAds.Modules.Notification/Entities/EmailMessage.cs` | 11 |
| `EmailMessageBase` | Class | `ClassifiedAds.Modules.Notification/Entities/EmailMessage.cs` | 15 |
| `OutboxMessage` | Class | `ClassifiedAds.Modules.LlmAssistant/Entities/OutboxMessage.cs` | 5 |
| `ArchivedOutboxMessage` | Class | `ClassifiedAds.Modules.LlmAssistant/Entities/OutboxMessage.cs` | 9 |
| `OutboxMessageBase` | Class | `ClassifiedAds.Modules.LlmAssistant/Entities/OutboxMessage.cs` | 13 |
| `AuditLogEntry` | Class | `ClassifiedAds.Modules.TestReporting/Entities/AuditLogEntry.cs` | 5 |
| `TestDataSet` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/TestDataSet.cs` | 8 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `Register → EmailMessage` | cross_community | 4 |
| `ChangePassword → EmailMessage` | cross_community | 3 |
| `UpdateProfile → EmailMessage` | cross_community | 3 |
| `ResetPassword → EmailMessage` | cross_community | 3 |
| `ForgotPassword → EmailMessage` | cross_community | 3 |
| `ResendConfirmationEmail → EmailMessage` | cross_community | 3 |
| `SetPassword → EmailMessage` | cross_community | 3 |
| `SendPasswordResetEmail → EmailMessage` | cross_community | 3 |
| `SendEmailConfirmation → EmailMessage` | cross_community | 3 |
| `LockUser → EmailMessage` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Queries | 1 calls |

## How to Explore

1. `gitnexus_context({name: "OutboxMessage"})` — see callers and callees
2. `gitnexus_query({query: "entities"})` — find related execution flows
3. Read key files listed above for implementation details
