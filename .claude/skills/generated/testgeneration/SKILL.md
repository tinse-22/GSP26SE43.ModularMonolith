---
name: testgeneration
description: "Skill for the TestGeneration area of GSP26SE43.ModularMonolith. 904 symbols across 150 files."
---

# TestGeneration

904 symbols | 150 files | Cohesion: 82%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how N8nTransientException, LlmSuggestionFeedbackContextResult, N8nBoundaryNegativeResponse work
- Modifying testgeneration-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/TestGeneration/SemanticTokenMatcherTests.cs` | Match_Should_ReturnNull_WhenSourceIsNull, Match_Should_ReturnNull_WhenTargetIsNull, Match_Should_ReturnNull_WhenBothAreEmpty, Match_Should_ReturnNull_WhenNoMatch, Match_Should_ReturnExactMatch_WhenTokensAreIdentical (+36) |
| `ClassifiedAds.UnitTests/TestGeneration/LlmScenarioSuggesterTests.cs` | SuggestScenariosAsync_Should_UseCache_WhenAllEndpointsHaveCacheHit, SuggestScenariosAsync_Should_CallN8n_WhenCacheMiss, SuggestScenariosAsync_Should_TargetScenarioCount_ByHttpMethod, SuggestScenariosAsync_Should_NotAddFallback_ForDeleteEndpoints, SuggestScenariosAsync_Should_NotAddFallback_ForSuccessOnlyGetEndpoints (+34) |
| `ClassifiedAds.UnitTests/TestGeneration/GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs` | HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist, HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner, HandleAsync_Should_ThrowValidation_WhenSuiteIsArchived, HandleAsync_Should_CallGateService, HandleAsync_Should_ThrowValidation_WhenExistingCasesAndNoForceRegenerate (+30) |
| `ClassifiedAds.UnitTests/TestGeneration/ReviewLlmSuggestionCommandHandlerTests.cs` | HandleAsync_Should_ThrowValidation_WhenSuggestionIdEmpty, HandleAsync_Should_ThrowValidation_WhenInvalidReviewAction, HandleAsync_Should_ThrowValidation_WhenRowVersionMissing, HandleAsync_Should_ThrowValidation_WhenRowVersionIsNotBase64, HandleAsync_Should_ThrowNotFound_WhenSuiteNotFound (+29) |
| `ClassifiedAds.UnitTests/TestGeneration/SchemaRelationshipAnalyzerTests.cs` | BuildSchemaReferenceGraph_Should_ReturnEmpty_WhenInputIsNull, BuildSchemaReferenceGraph_Should_ReturnEmpty_WhenInputIsEmpty, BuildSchemaReferenceGraph_Should_CreateUnidirectionalEdges, BuildSchemaReferenceGraph_Should_IgnoreSelfReferences, BuildSchemaReferenceGraph_Should_HandleChainedReferences (+28) |
| `ClassifiedAds.UnitTests/TestGeneration/GenerateHappyPathTestCasesCommandHandlerTests.cs` | HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty, HandleAsync_Should_ThrowValidation_WhenSpecificationIdEmpty, HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist, HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner, HandleAsync_Should_ThrowValidation_WhenSuiteIsArchived (+26) |
| `ClassifiedAds.UnitTests/TestGeneration/GenerateLlmSuggestionPreviewCommandHandlerTests.cs` | HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist, HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner, HandleAsync_Should_ThrowValidation_WhenSuiteArchived, HandleAsync_Should_CallGateService, HandleAsync_Should_PassMetadataAndParameterDetails_IntoLlmContext (+24) |
| `ClassifiedAds.UnitTests/TestGeneration/BoundaryNegativeTestCaseGeneratorTests.cs` | GenerateAsync_Should_OnlyIncludePathMutations_WhenOnlyPathFlagTrue, GenerateAsync_Should_OnlyIncludeBodyMutations_WhenOnlyBodyFlagTrue, GenerateAsync_Should_PreserveBodyMutationExpectedStatuses_WhenBuildingExpectation, GenerateAsync_Should_PreservePathMutationExpectedStatuses_WhenBuildingExpectation, GenerateAsync_Should_OnlyIncludeLlmSuggestions_WhenOnlyLlmFlagTrue (+20) |
| `ClassifiedAds.UnitTests/TestGeneration/ObservationConfirmationPromptBuilderTests.cs` | BuildForEndpoint_Should_ReturnNull_WhenContextIsNull, BuildForEndpoint_Should_ReturnPrompts_WhenMinimalContextProvided, BuildForEndpoint_Should_IncludeMethodAndPath, BuildForEndpoint_Should_IncludeOperationId_WhenProvided, BuildForEndpoint_Should_IncludeSummaryAndDescription (+20) |
| `ClassifiedAds.UnitTests/TestGeneration/BulkReviewLlmSuggestionsCommandHandlerTests.cs` | HandleAsync_Should_ApproveMatchingPendingSuggestions_AndMaterializeTestCases, HandleAsync_Should_RejectMatchingPendingSuggestions_WithoutMaterializing, HandleAsync_Should_OnlyProcessSuggestionsMatchingFilters, HandleAsync_Should_IgnoreSuggestionsThatAreNotPending, HandleAsync_Should_AppendAfterHighestExistingOrderIndex_WhenApprovingBatch (+18) |

## Entry Points

Start here when exploring this area:

- **`N8nTransientException`** (Class) — `ClassifiedAds.Modules.TestGeneration/Services/N8nIntegrationService.cs:34`
- **`LlmSuggestionFeedbackContextResult`** (Class) — `ClassifiedAds.Modules.TestGeneration/Services/ILlmSuggestionFeedbackContextService.cs:15`
- **`N8nBoundaryNegativeResponse`** (Class) — `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativeResponse.cs:8`
- **`N8nSuggestedScenario`** (Class) — `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativeResponse.cs:17`
- **`CachedSuggestionsDto`** (Class) — `ClassifiedAds.Contracts/LlmAssistant/DTOs/SaveLlmInteractionRequest.cs:30`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `N8nTransientException` | Class | `ClassifiedAds.Modules.TestGeneration/Services/N8nIntegrationService.cs` | 34 |
| `LlmSuggestionFeedbackContextResult` | Class | `ClassifiedAds.Modules.TestGeneration/Services/ILlmSuggestionFeedbackContextService.cs` | 15 |
| `N8nBoundaryNegativeResponse` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativeResponse.cs` | 8 |
| `N8nSuggestedScenario` | Class | `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativeResponse.cs` | 17 |
| `CachedSuggestionsDto` | Class | `ClassifiedAds.Contracts/LlmAssistant/DTOs/SaveLlmInteractionRequest.cs` | 30 |
| `EndpointParameterDetailDto` | Class | `ClassifiedAds.Contracts/ApiDocumentation/DTOs/EndpointParameterDetailDto.cs` | 8 |
| `HappyPathGenerationResult` | Class | `ClassifiedAds.Modules.TestGeneration/Services/IHappyPathTestCaseGenerator.cs` | 33 |
| `GenerateHappyPathResultModel` | Class | `ClassifiedAds.Modules.TestGeneration/Models/GenerateHappyPathResultModel.cs` | 8 |
| `TestCaseRequest` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/TestCaseRequest.cs` | 8 |
| `TestCaseExpectation` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/TestCaseExpectation.cs` | 8 |
| `TestCase` | Class | `ClassifiedAds.Modules.TestGeneration/Entities/TestCase.cs` | 9 |
| `GenerateHappyPathTestCasesCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/GenerateHappyPathTestCasesCommand.cs` | 18 |
| `EditableLlmSuggestionInput` | Class | `ClassifiedAds.Modules.TestGeneration/Models/Requests/ReviewLlmSuggestionRequest.cs` | 33 |
| `LlmSuggestedScenario` | Class | `ClassifiedAds.Modules.TestGeneration/Services/ILlmScenarioSuggester.cs` | 73 |
| `BoundaryNegativeOptions` | Class | `ClassifiedAds.Modules.TestGeneration/Services/IBoundaryNegativeTestCaseGenerator.cs` | 25 |
| `PathParameterMutationDto` | Class | `ClassifiedAds.Contracts/ApiDocumentation/DTOs/PathParameterMutationDto.cs` | 8 |
| `ParameterDetailDto` | Class | `ClassifiedAds.Contracts/ApiDocumentation/DTOs/EndpointParameterDetailDto.cs` | 22 |
| `AiTestCaseRequestDto` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` | 67 |
| `AiTestCaseExpectationDto` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` | 79 |
| `SaveAiGeneratedTestCasesCommand` | Class | `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` | 138 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `HandleAsync → LimitCheckResultDTO` | cross_community | 4 |
| `GenerateAsync → ValidationException` | cross_community | 4 |
| `HandleAsync → ApiOrderItemModel` | cross_community | 4 |
| `HandleAsync → LimitCheckResultDTO` | cross_community | 4 |
| `BuildProposalOrder → ExtractSchemaRefsFromPayload` | cross_community | 4 |
| `BuildProposalOrder → IsBareSingleRef` | cross_community | 4 |
| `HandleAsync → ApiOrderItemModel` | cross_community | 4 |
| `HandleAsync → ValidationException` | cross_community | 4 |
| `ApproveManyAsync → LimitCheckResultDTO` | cross_community | 3 |
| `GenerateAsync → NotFoundException` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 158 calls |
| Queries | 26 calls |
| Commands | 25 calls |
| Controllers | 23 calls |
| Models | 10 calls |
| TestExecution | 10 calls |
| ApiDocumentation | 6 calls |
| MessageBusMessages | 3 calls |

## How to Explore

1. `gitnexus_context({name: "N8nTransientException"})` — see callers and callees
2. `gitnexus_query({query: "testgeneration"})` — find related execution flows
3. Read key files listed above for implementation details
