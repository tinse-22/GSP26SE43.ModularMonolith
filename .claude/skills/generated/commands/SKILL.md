---
name: commands
description: "Skill for the Commands area of GSP26SE43.ModularMonolith. 246 symbols across 127 files."
---

# Commands

246 symbols | 127 files | Cohesion: 73%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestGeneration/`
- Understanding how UpsertLlmSuggestionFeedbackCommandHandler, UpdateTestCaseCommandHandler, UpdateSrsRequirementCommandHandler work
- Modifying commands-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsAnalysisCommand.cs` | TriggerSrsAnalysisCommandHandler, HandleAsync, ApplyLocalFallbackAsync, N8nSrsAnalysisPayload, BuildLocalRequirements (+16) |
| `ClassifiedAds.Modules.Subscription/Commands/AddUpdatePlanCommand.cs` | AddUpdatePlanCommandHandler, HandleAsync, GetExistingPlanAsync, ApplyModel, EnsureNameUniquenessAsync (+10) |
| `ClassifiedAds.Modules.Subscription/Commands/AddUpdateSubscriptionCommand.cs` | AddUpdateSubscriptionCommandHandler, HandleAsync, GetExistingSubscriptionAsync, GetOldPlanIfNeededAsync, ValidateBilling (+3) |
| `ClassifiedAds.Modules.TestExecution/Commands/AddUpdateExecutionEnvironmentCommand.cs` | AddUpdateExecutionEnvironmentCommandHandler, HandleCreate, HandleUpdate, UnsetProjectDefaults, EnsureSingleDefaultEnvironment (+2) |
| `ClassifiedAds.Modules.Subscription/Commands/HandlePayOsWebhookCommand.cs` | HandlePayOsWebhookCommandHandler, UpsertSubscriptionAsync, ResolveSnapshotMonthlyPrice, ResolveSnapshotYearlyPrice, ResolveSnapshotCurrency (+2) |
| `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsRefinementCommand.cs` | TriggerSrsRefinementCommandHandler, HandleAsync, N8nSrsRefinementPayload, N8nSrsRequirementToRefine, N8nSrsUserAnswer |
| `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` | SaveAiGeneratedTestCasesCommandHandler, ParseStatusCodesFromExpectation, ExtractExplicitStatusCodes, ParseStatusCodesFromJsonElement, ParseStatusCodesFromText |
| `ClassifiedAds.Modules.TestGeneration/Commands/ReorderApiTestOrderCommand.cs` | ReorderApiTestOrderCommandHandler, ReorderApiTestOrderCommand, HandleAsync, EnsureOwnership, EnsurePendingProposal |
| `ClassifiedAds.Modules.TestGeneration/Commands/RejectApiTestOrderCommand.cs` | RejectApiTestOrderCommandHandler, RejectApiTestOrderCommand, HandleAsync, EnsureOwnership, EnsurePendingProposal |
| `ClassifiedAds.Modules.Subscription/Commands/SyncPaymentFromPayOsCommand.cs` | SyncPaymentFromPayOsCommandHandler, HandleAsync, IsSucceededStatus, IsCanceledStatus, IsExpiredStatus |

## Entry Points

Start here when exploring this area:

- **`UpsertLlmSuggestionFeedbackCommandHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpsertLlmSuggestionFeedbackCommand.cs:29`
- **`UpdateTestCaseCommandHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpdateTestCaseCommand.cs:52`
- **`UpdateSrsRequirementCommandHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsRequirementCommand.cs:40`
- **`UpdateSrsDocumentCommandHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsDocumentCommand.cs:29`
- **`TriggerSrsRefinementCommandHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsRefinementCommand.cs:33`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `UpsertLlmSuggestionFeedbackCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpsertLlmSuggestionFeedbackCommand.cs` | 29 |
| `UpdateTestCaseCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpdateTestCaseCommand.cs` | 52 |
| `UpdateSrsRequirementCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsRequirementCommand.cs` | 40 |
| `UpdateSrsDocumentCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/UpdateSrsDocumentCommand.cs` | 29 |
| `TriggerSrsRefinementCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsRefinementCommand.cs` | 33 |
| `TriggerSrsAnalysisCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/TriggerSrsAnalysisCommand.cs` | 35 |
| `ToggleTestCaseCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ToggleTestCaseCommand.cs` | 20 |
| `SaveAiGeneratedTestCasesCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` | 144 |
| `ReviewLlmSuggestionCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ReviewLlmSuggestionCommand.cs` | 27 |
| `RestoreTestCaseCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/RestoreTestCaseCommand.cs` | 22 |
| `ReorderTestCasesCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ReorderTestCasesCommand.cs` | 20 |
| `ReorderApiTestOrderCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ReorderApiTestOrderCommand.cs` | 33 |
| `RejectApiTestOrderCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/RejectApiTestOrderCommand.cs` | 30 |
| `ProposeApiTestOrderCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ProposeApiTestOrderCommand.cs` | 34 |
| `ProcessSrsRefinementCallbackCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ProcessSrsRefinementCallbackCommand.cs` | 25 |
| `ProcessSrsAnalysisCallbackCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/ProcessSrsAnalysisCallbackCommand.cs` | 30 |
| `GenerateTestCasesCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateTestCasesCommand.cs` | 36 |
| `GenerateLlmSuggestionPreviewCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs` | 29 |
| `GenerateHappyPathTestCasesCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateHappyPathTestCasesCommand.cs` | 28 |
| `GenerateBoundaryNegativeTestCasesCommandHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateBoundaryNegativeTestCasesCommand.cs` | 39 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → GetObjectName` | cross_community | 4 |
| `HandleAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → DispatchAsync` | cross_community | 3 |
| `HandleAsync → ReadAsync` | cross_community | 3 |
| `HandleAsync → ReadAsync` | cross_community | 3 |
| `HandleAsync → Create` | cross_community | 3 |
| `HandleAsync → PaymentIntentStatusChangedOutboxEvent` | cross_community | 3 |
| `HandleAsync → ComputeHmacSha256` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 43 calls |
| Queries | 18 calls |
| Controllers | 14 calls |
| Subscription | 14 calls |
| TestGeneration | 12 calls |
| TestExecution | 2 calls |
| Models | 2 calls |
| IntegrationEvents | 2 calls |

## How to Explore

1. `gitnexus_context({name: "UpsertLlmSuggestionFeedbackCommandHandler"})` — see callers and callees
2. `gitnexus_query({query: "commands"})` — find related execution flows
3. Read key files listed above for implementation details
