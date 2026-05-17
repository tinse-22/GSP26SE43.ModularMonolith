---
name: models
description: "Skill for the Models area of GSP26SE43.ModularMonolith. 107 symbols across 44 files."
---

# Models

107 symbols | 44 files | Cohesion: 80%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestGeneration/`
- Understanding how AdminDashboardModel, SubscriptionSummaryModel, RevenueSummaryModel work
- Modifying models-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestGeneration/Models/TestCaseModel.cs` | TestCaseModel, FromEntity, DeserializeTags, MapVariables, TestCaseRequestModel (+5) |
| `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | AdminDashboardModel, SubscriptionSummaryModel, RevenueSummaryModel, FailedTransactionSummaryModel, FailureReasonModel (+4) |
| `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionModel.cs` | LlmSuggestionModel, FromEntity, TryDeserializeRequest, DeserializeGuidList, DeserializeGuidListStatic (+3) |
| `ClassifiedAds.Modules.TestReporting/Models/CoverageMetricModel.cs` | SerializeByMethod, SerializeByTag, SerializeUncoveredPaths, NormalizeDictionary, CoverageMetricModel (+3) |
| `ClassifiedAds.WebAPI/Controllers/AdminUsageController.cs` | Get, BuildPoints, BuildTopUsers, ResolveUserName, ResolveUserName |
| `ClassifiedAds.Modules.ApiDocumentation/Models/EndpointModel.cs` | EndpointModel, EndpointDetailModel, ParameterModel, ResponseModel, SecurityReqModel |
| `ClassifiedAds.Modules.Subscription/Models/SubscriptionModelMappingConfiguration.cs` | ToModel, ToModels, ToModel, ToModel, ToModel |
| `ClassifiedAds.WebAPI/Controllers/AdminDashboardController.cs` | Get, BuildRevenueSummary, BuildTopUsers, ResolveUserName |
| `ClassifiedAds.WebAPI/Models/AdminUsageModel.cs` | AdminUsageModel, UsagePointModel, UsageTotalsModel, UsageTopUsersModel |
| `ClassifiedAds.Modules.TestExecution/Models/ExecutionEnvironmentModel.cs` | ExecutionEnvironmentModel, FromEntity, DeserializeDictionary |

## Entry Points

Start here when exploring this area:

- **`AdminDashboardModel`** (Class) — `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs:5`
- **`SubscriptionSummaryModel`** (Class) — `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs:26`
- **`RevenueSummaryModel`** (Class) — `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs:33`
- **`FailedTransactionSummaryModel`** (Class) — `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs:42`
- **`FailureReasonModel`** (Class) — `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs:49`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `AdminDashboardModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 5 |
| `SubscriptionSummaryModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 26 |
| `RevenueSummaryModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 33 |
| `FailedTransactionSummaryModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 42 |
| `FailureReasonModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 49 |
| `TestRunSummaryModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 56 |
| `UsageSummaryModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 63 |
| `AdminActionModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 79 |
| `TestCaseModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/TestCaseModel.cs` | 11 |
| `TestCaseRequestModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/TestCaseModel.cs` | 121 |
| `TestCaseExpectationModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/TestCaseModel.cs` | 150 |
| `TestCaseVariableModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/TestCaseModel.cs` | 177 |
| `LlmSuggestionModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionModel.cs` | 10 |
| `SuggestionVariableModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionModel.cs` | 213 |
| `LlmSuggestionFeedbackSummaryModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionFeedbackSummaryModel.cs` | 7 |
| `AdminUsageModel` | Class | `ClassifiedAds.WebAPI/Models/AdminUsageModel.cs` | 4 |
| `UsagePointModel` | Class | `ClassifiedAds.WebAPI/Models/AdminUsageModel.cs` | 17 |
| `UsageTotalsModel` | Class | `ClassifiedAds.WebAPI/Models/AdminUsageModel.cs` | 30 |
| `UsageTopUsersModel` | Class | `ClassifiedAds.WebAPI/Models/AdminUsageModel.cs` | 41 |
| `TopUserMetricModel` | Class | `ClassifiedAds.WebAPI/Models/AdminDashboardModel.cs` | 70 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → TryAssign` | cross_community | 4 |
| `HandleAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → PathParameterInfo` | cross_community | 4 |
| `HandleAsync → ExecutionAuthConfigModel` | cross_community | 4 |
| `HandleAsync → MaskSecret` | cross_community | 4 |
| `Get → RevenueSummaryModel` | intra_community | 3 |
| `HandleAsync → LooksLikeJson` | cross_community | 3 |
| `HandleAsync → ResolvedUrlResult` | cross_community | 3 |
| `HandleAsync → TryGetValueIgnoreCase` | cross_community | 3 |
| `GenerateAsync → CoverageMetricModel` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Queries | 6 calls |
| Services | 5 calls |
| Controllers | 4 calls |
| TestExecution | 2 calls |
| ApiDocumentation | 1 calls |
| TestGeneration | 1 calls |
| Commands | 1 calls |
| Entities | 1 calls |

## How to Explore

1. `gitnexus_context({name: "AdminDashboardModel"})` — see callers and callees
2. `gitnexus_query({query: "models"})` — find related execution flows
3. Read key files listed above for implementation details
