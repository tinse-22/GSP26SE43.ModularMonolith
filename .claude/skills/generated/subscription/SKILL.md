---
name: subscription
description: "Skill for the Subscription area of GSP26SE43.ModularMonolith. 211 symbols across 62 files."
---

# Subscription

211 symbols | 62 files | Cohesion: 67%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how PayOsWebhookPayload, PayOsWebhookData, HandlePayOsWebhookCommand work
- Modifying subscription-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/Subscription/PlanModelsTests.cs` | CreateUpdatePlanModel_Should_BeValid_WithCorrectData, CreateUpdatePlanModel_Should_RequireName, CreateUpdatePlanModel_Should_RequireDisplayName, CreateUpdatePlanModel_Should_EnforceNameMaxLength, CreateUpdatePlanModel_Should_EnforceDisplayNameMaxLength (+13) |
| `ClassifiedAds.UnitTests/Subscription/PlanMappingTests.cs` | ToEntity_Should_MapFromModelCorrectly, ToEntity_Should_DefaultCurrencyToUSD_WhenNull, ToLimitEntities_Should_ReturnEmptyList_WhenLimitsIsNull, ToLimitEntities_Should_MapValidLimitTypes, ToLimitEntities_Should_ThrowForNullLimitType (+10) |
| `ClassifiedAds.UnitTests/Subscription/HandlePayOsWebhookCommandHandlerTests.cs` | HandleAsync_NullPayloadData_Should_ReturnIgnored, HandleAsync_InvalidSignature_Should_ReturnIgnored, HandleAsync_ValidSignature_Should_NotReturnIgnoredDueToSignature, HandleAsync_OrderCodeNotFound_Should_ReturnIgnored, HandleAsync_DuplicateTransaction_Should_ReturnIgnored (+7) |
| `ClassifiedAds.UnitTests/Subscription/AddUpdatePlanCommandHandlerTests.cs` | HandleAsync_Should_ThrowValidationException_ForDuplicateLimitTypes, HandleAsync_Should_ThrowValidationException_ForZeroLimitValue_WhenNotUnlimited, HandleAsync_Should_ThrowValidationException_ForNullLimitValue_WhenNotUnlimited, HandleAsync_Should_NullifyLimitValue_WhenUnlimited, HandleAsync_CreatePlan_Should_CallAddOrUpdate (+7) |
| `ClassifiedAds.UnitTests/Subscription/SubscriptionEntitiesTests.cs` | UserSubscription_Should_HaveDefaultValues, UserSubscription_Should_SetAllProperties, UsageTracking_Should_HaveZeroDefaults, UsageTracking_Should_TrackUsage, SubscriptionPlan_Should_HaveDefaultValues (+6) |
| `ClassifiedAds.UnitTests/Subscription/PlanEventHandlerTests.cs` | PlanCreatedEventHandler_Should_CreateAuditLog, PlanCreatedEventHandler_Should_CreateOutboxMessages, PlanCreatedEventHandler_Should_SaveChanges, PlanCreatedEventHandler_Should_UseEmptyGuid_WhenNotAuthenticated, CreateSamplePlan (+5) |
| `ClassifiedAds.UnitTests/Subscription/PlansControllerTests.cs` | Delete_Should_ReturnOk, Delete_Should_DispatchDeleteCommand, Get_Should_ReturnOkWithPlans, Get_Should_PassFilterParameters, GetById_Should_ReturnOkWithPlan (+4) |
| `ClassifiedAds.Modules.Subscription/Commands/AddPaymentTransactionCommand.cs` | AddPaymentTransactionCommand, HandleAsync, ResolveAmount, GetSnapshotAmount, GetPlanAmount (+2) |
| `ClassifiedAds.UnitTests/Subscription/PlanQueryHandlerTests.cs` | HandleAsync_Should_ReturnAllPlans_WhenNoFilters, HandleAsync_Should_ReturnEmptyList_WhenNoPlansExist, HandleAsync_Should_IncludeLimitsForEachPlan, HandleAsync_Should_ReturnPlanModel_WhenPlanExists, HandleAsync_Should_ReturnNull_WhenPlanNotFound_AndThrowNotFoundIfNullIsFalse (+2) |
| `ClassifiedAds.Modules.Subscription/Commands/HandlePayOsWebhookCommand.cs` | HandlePayOsWebhookCommand, HandleAsync, ResolveProviderRef, IsSucceeded, ResolveFailureStatus (+1) |

## Entry Points

Start here when exploring this area:

- **`PayOsWebhookPayload`** (Class) — `ClassifiedAds.Modules.Subscription/Models/PayOsModels.cs:80`
- **`PayOsWebhookData`** (Class) — `ClassifiedAds.Modules.Subscription/Models/PayOsModels.cs:98`
- **`HandlePayOsWebhookCommand`** (Class) — `ClassifiedAds.Modules.Subscription/Commands/HandlePayOsWebhookCommand.cs:12`
- **`CreateUpdatePlanModel`** (Class) — `ClassifiedAds.Modules.Subscription/Models/CreateUpdatePlanModel.cs:5`
- **`AddPaymentTransactionModel`** (Class) — `ClassifiedAds.Modules.Subscription/Models/AddPaymentTransactionModel.cs:4`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `PayOsWebhookPayload` | Class | `ClassifiedAds.Modules.Subscription/Models/PayOsModels.cs` | 80 |
| `PayOsWebhookData` | Class | `ClassifiedAds.Modules.Subscription/Models/PayOsModels.cs` | 98 |
| `HandlePayOsWebhookCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/HandlePayOsWebhookCommand.cs` | 12 |
| `CreateUpdatePlanModel` | Class | `ClassifiedAds.Modules.Subscription/Models/CreateUpdatePlanModel.cs` | 5 |
| `AddPaymentTransactionModel` | Class | `ClassifiedAds.Modules.Subscription/Models/AddPaymentTransactionModel.cs` | 4 |
| `UserSubscription` | Class | `ClassifiedAds.Modules.Subscription/Entities/UserSubscription.cs` | 8 |
| `AddPaymentTransactionCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/AddPaymentTransactionCommand.cs` | 12 |
| `PlanLimitModel` | Class | `ClassifiedAds.Modules.Subscription/Models/PlanLimitModel.cs` | 7 |
| `UpsertUsageTrackingModel` | Class | `ClassifiedAds.Modules.Subscription/Models/UpsertUsageTrackingModel.cs` | 5 |
| `UsageTracking` | Class | `ClassifiedAds.Modules.Subscription/Entities/UsageTracking.cs` | 8 |
| `UpsertUsageTrackingCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/UpsertUsageTrackingCommand.cs` | 12 |
| `DeletePlanCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/DeletePlanCommand.cs` | 12 |
| `SubscriptionPurchaseResultModel` | Class | `ClassifiedAds.Modules.Subscription/Models/SubscriptionPurchaseResultModel.cs` | 4 |
| `PayOsGetPaymentData` | Class | `ClassifiedAds.Modules.Subscription/Models/PayOsModels.cs` | 56 |
| `CreateSubscriptionPaymentModel` | Class | `ClassifiedAds.Modules.Subscription/Models/CreateSubscriptionPaymentModel.cs` | 5 |
| `PaymentIntent` | Class | `ClassifiedAds.Modules.Subscription/Entities/PaymentIntent.cs` | 8 |
| `CreateSubscriptionPaymentCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/CreateSubscriptionPaymentCommand.cs` | 14 |
| `ConsumeLimitAtomicallyCommand` | Class | `ClassifiedAds.Modules.Subscription/Commands/ConsumeLimitAtomicallyCommand.cs` | 16 |
| `ConsumeLimitAtomicallyCommandHandler` | Class | `ClassifiedAds.Modules.Subscription/Commands/ConsumeLimitAtomicallyCommand.cs` | 30 |
| `GetPlansQuery` | Class | `ClassifiedAds.Modules.Subscription/Queries/GetPlansQuery.cs` | 12 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → IsManualResetEventDisposed` | cross_community | 4 |
| `HandleAsync → DispatchAsync` | cross_community | 4 |
| `HandleAsync → GetCurrentSubscriptionByUserQuery` | cross_community | 4 |
| `HandleAsync → LimitCheckResultDTO` | cross_community | 4 |
| `HandleAsync → GetPlanQuery` | cross_community | 4 |
| `Cancel → ValidationException` | cross_community | 4 |
| `UpsertUsage → ValidationException` | cross_community | 4 |
| `HandleAsync → IsManualResetEventDisposed` | cross_community | 4 |
| `HandleAsync → DispatchAsync` | cross_community | 4 |
| `HandleAsync → GetCurrentSubscriptionByUserQuery` | cross_community | 4 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Commands | 20 calls |
| Controllers | 17 calls |
| Services | 10 calls |
| Queries | 6 calls |
| Entities | 3 calls |
| TestGeneration | 2 calls |
| ClassifiedAds.Persistence.PostgreSQL | 1 calls |

## How to Explore

1. `gitnexus_context({name: "PayOsWebhookPayload"})` — see callers and callees
2. `gitnexus_query({query: "subscription"})` — find related execution flows
3. Read key files listed above for implementation details
