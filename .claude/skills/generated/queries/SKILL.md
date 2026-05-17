---
name: queries
description: "Skill for the Queries area of GSP26SE43.ModularMonolith. 200 symbols across 104 files."
---

# Queries

200 symbols | 104 files | Cohesion: 71%

## When to Use

- Working with code in `ClassifiedAds.Modules.TestGeneration/`
- Understanding how GetTestSuiteScopesQueryHandler, GetTestSuiteScopeQueryHandler, GetTestCasesByTestSuiteQueryHandler work
- Modifying queries-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.Modules.TestGeneration/Models/SrsDocumentModel.cs` | SrsAnalysisJobModel, FromEntity, SrsRequirementClarificationModel, FromEntity, TraceabilityMatrix (+4) |
| `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsRequirementsQuery.cs` | GetSrsRequirementsQueryHandler, GetSrsRequirementDetailQueryHandler, GetSrsRequirementClarificationsQueryHandler, GetSrsRequirementsQuery, GetSrsRequirementDetailQuery (+3) |
| `ClassifiedAds.Modules.Subscription/Queries/GetRevenueSeriesQuery.cs` | GetRevenueSeriesQueryHandler, GetRevenueSeriesQuery, HandleAsync, NormalizeGroupBy, ResolveDefaultFrom (+3) |
| `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsTraceabilityQuery.cs` | GetSrsTraceabilityQueryHandler, GetSrsTraceabilityQuery, HandleAsync, ComputeValidationStatus, CountByStatus (+1) |
| `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsDocumentsQuery.cs` | GetSrsDocumentsQueryHandler, GetSrsDocumentDetailQueryHandler, GetSrsDocumentsQuery, GetSrsDocumentDetailQuery, HandleAsync (+1) |
| `ClassifiedAds.Modules.TestGeneration/Controllers/SrsDocumentsController.cs` | GetAll, GetById, GetJobStatus, GetRequirementById, GetRequirements (+1) |
| `ClassifiedAds.Modules.TestGeneration/Queries/GetGenerationJobStatusQuery.cs` | GetGenerationJobStatusQueryHandler, GetGenerationJobStatusQuery, GenerationJobStatusDto, HandleAsync |
| `ClassifiedAds.UnitTests/CrossCuttingConcerns/NotFoundExceptionTests.cs` | Constructor_ShouldCreateException_WithDefaultMessage, Constructor_ShouldCreateException_WithCustomMessage, Constructor_ShouldAcceptVariousMessages, Exception_ShouldBeThrowable_AndCatchableAsException |
| `ClassifiedAds.Modules.TestReporting/Queries/GetTestRunReportsQuery.cs` | GetTestRunReportsQuery, GetTestRunReportsQueryHandler, HandleAsync, GetCoverageAsync |
| `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCasesByTestSuiteQuery.cs` | GetTestCasesByTestSuiteQueryHandler, GetTestCasesByTestSuiteQuery, HandleAsync |

## Entry Points

Start here when exploring this area:

- **`GetTestSuiteScopesQueryHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopesQuery.cs:21`
- **`GetTestSuiteScopeQueryHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopeQuery.cs:21`
- **`GetTestCasesByTestSuiteQueryHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCasesByTestSuiteQuery.cs:23`
- **`GetTestCaseDetailQueryHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCaseDetailQuery.cs:20`
- **`GetSrsTraceabilityQueryHandler`** (Class) — `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsTraceabilityQuery.cs:30`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `GetTestSuiteScopesQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopesQuery.cs` | 21 |
| `GetTestSuiteScopeQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopeQuery.cs` | 21 |
| `GetTestCasesByTestSuiteQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCasesByTestSuiteQuery.cs` | 23 |
| `GetTestCaseDetailQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCaseDetailQuery.cs` | 20 |
| `GetSrsTraceabilityQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsTraceabilityQuery.cs` | 30 |
| `GetSrsRequirementsQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsRequirementsQuery.cs` | 32 |
| `GetSrsRequirementDetailQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsRequirementsQuery.cs` | 92 |
| `GetSrsRequirementClarificationsQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsRequirementsQuery.cs` | 139 |
| `GetSrsDocumentsQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsDocumentsQuery.cs` | 21 |
| `GetSrsDocumentDetailQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsDocumentsQuery.cs` | 70 |
| `GetSrsAnalysisJobQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetSrsAnalysisJobQuery.cs` | 23 |
| `GetLlmSuggestionsQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionsQuery.cs` | 24 |
| `GetLlmSuggestionDetailQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionDetailQuery.cs` | 20 |
| `GetLatestApiTestOrderProposalQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetLatestApiTestOrderProposalQuery.cs` | 20 |
| `GetGenerationJobStatusQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetGenerationJobStatusQuery.cs` | 31 |
| `GetApiTestOrderGateStatusQueryHandler` | Class | `ClassifiedAds.Modules.TestGeneration/Queries/GetApiTestOrderGateStatusQuery.cs` | 20 |
| `GetTestRunsQueryHandler` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunsQuery.cs` | 33 |
| `GetTestRunResultsQueryHandler` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunResultsQuery.cs` | 25 |
| `GetTestRunQueryHandler` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetTestRunQuery.cs` | 22 |
| `GetExecutionEnvironmentsQueryHandler` | Class | `ClassifiedAds.Modules.TestExecution/Queries/GetExecutionEnvironmentsQuery.cs` | 22 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → BuildFailureSummary` | cross_community | 6 |
| `HandleAsync → DeserializeFailureCodes` | cross_community | 5 |
| `HandleAsync → TestCaseExecutionEvidenceDto` | cross_community | 5 |
| `HandleAsync → DeserializeCachedResult` | cross_community | 4 |
| `HandleAsync → NotFoundException` | cross_community | 4 |
| `GenerateAsync → NotFoundException` | cross_community | 3 |
| `HandleAsync → NotFoundException` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Controllers | 32 calls |
| Services | 11 calls |
| TestGeneration | 4 calls |
| Models | 2 calls |
| Commands | 2 calls |

## How to Explore

1. `gitnexus_context({name: "GetTestSuiteScopesQueryHandler"})` — see callers and callees
2. `gitnexus_query({query: "queries"})` — find related execution flows
3. Read key files listed above for implementation details
