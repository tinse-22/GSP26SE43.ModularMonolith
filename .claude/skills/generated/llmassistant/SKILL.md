---
name: llmassistant
description: "Skill for the LlmAssistant area of GSP26SE43.ModularMonolith. 39 symbols across 17 files."
---

# LlmAssistant

39 symbols | 17 files | Cohesion: 64%

## When to Use

- Working with code in `ClassifiedAds.UnitTests/`
- Understanding how GetFailureExplanationQuery, FailureExplanationPromptBuilder, FailureExplanationPrompt work
- Modifying llmassistant-related functionality

## Key Files

| File | Symbols |
|------|---------|
| `ClassifiedAds.UnitTests/LlmAssistant/ExplainTestFailureCommandHandlerTests.cs` | HandleAsync_MetadataAvailable_ShouldLoadMetadataAndReturnResult, HandleAsync_MetadataMissing_ShouldStillWork, HandleAsync_MetadataOptionalPath_ShouldSkipMetadataLookupAndInvokeExplainer, HandleAsync_OwnerMismatch_ShouldThrowValidationException, CreateCommand (+2) |
| `ClassifiedAds.UnitTests/LlmAssistant/GetFailureExplanationQueryHandlerTests.cs` | HandleAsync_CacheMiss_ShouldThrowNotFoundExceptionWithPrefix, HandleAsync_CacheHit_ShouldReturnModel, HandleAsync_OwnerMismatch_ShouldThrowValidationException, CreateQuery, CreateContext |
| `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationTestData.cs` | CreateEndpointMetadata, CreatePrompt, CreateContext, CreateProviderResponse |
| `ClassifiedAds.Modules.LlmAssistant/Services/FailureExplanationPromptBuilder.cs` | FailureExplanationPromptBuilder, Build, BuildPrompt |
| `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs` | TestFailureExplanationContextDto, FailureExplanationActualResultDto, FailureExplanationFailureReasonDto |
| `ClassifiedAds.UnitTests/LlmAssistant/LlmFailureExplainerTests.cs` | ExplainAsync_CacheMiss_ShouldCallProvider, ExplainAsync_AuditFailure_ShouldReturnExplanationGracefully, ExplainAsync_CacheSaveFailure_ShouldReturnExplanationGracefully |
| `ClassifiedAds.Modules.LlmAssistant/Services/ILlmFailureExplainer.cs` | GetCachedAsync, ExplainAsync |
| `ClassifiedAds.Modules.LlmAssistant/Queries/GetFailureExplanationQuery.cs` | GetFailureExplanationQuery, HandleAsync |
| `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationPromptBuilderTests.cs` | Build_ShouldContainDeterministicSections, Build_ShouldNotContainRawSecrets |
| `ClassifiedAds.Modules.LlmAssistant/Controllers/FailureExplanationsController.cs` | GetFailureExplanation |

## Entry Points

Start here when exploring this area:

- **`GetFailureExplanationQuery`** (Class) — `ClassifiedAds.Modules.LlmAssistant/Queries/GetFailureExplanationQuery.cs:11`
- **`FailureExplanationPromptBuilder`** (Class) — `ClassifiedAds.Modules.LlmAssistant/Services/FailureExplanationPromptBuilder.cs:10`
- **`FailureExplanationPrompt`** (Class) — `ClassifiedAds.Modules.LlmAssistant/Models/FailureExplanationPrompt.cs:5`
- **`TestFailureExplanationContextDto`** (Class) — `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs:6`
- **`FailureExplanationActualResultDto`** (Class) — `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs:52`

## Key Symbols

| Symbol | Type | File | Line |
|--------|------|------|------|
| `GetFailureExplanationQuery` | Class | `ClassifiedAds.Modules.LlmAssistant/Queries/GetFailureExplanationQuery.cs` | 11 |
| `FailureExplanationPromptBuilder` | Class | `ClassifiedAds.Modules.LlmAssistant/Services/FailureExplanationPromptBuilder.cs` | 10 |
| `FailureExplanationPrompt` | Class | `ClassifiedAds.Modules.LlmAssistant/Models/FailureExplanationPrompt.cs` | 5 |
| `TestFailureExplanationContextDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs` | 6 |
| `FailureExplanationActualResultDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs` | 52 |
| `FailureExplanationFailureReasonDto` | Class | `ClassifiedAds.Contracts/TestExecution/DTOs/TestFailureExplanationContextDto.cs` | 91 |
| `IFailureExplanationPromptBuilder` | Interface | `ClassifiedAds.Modules.LlmAssistant/Services/IFailureExplanationPromptBuilder.cs` | 6 |
| `HandleAsync_CacheMiss_ShouldThrowNotFoundExceptionWithPrefix` | Method | `ClassifiedAds.UnitTests/LlmAssistant/GetFailureExplanationQueryHandlerTests.cs` | 28 |
| `HandleAsync_CacheHit_ShouldReturnModel` | Method | `ClassifiedAds.UnitTests/LlmAssistant/GetFailureExplanationQueryHandlerTests.cs` | 53 |
| `HandleAsync_OwnerMismatch_ShouldThrowValidationException` | Method | `ClassifiedAds.UnitTests/LlmAssistant/GetFailureExplanationQueryHandlerTests.cs` | 95 |
| `HandleAsync` | Method | `ClassifiedAds.Modules.LlmAssistant/Queries/GetFailureExplanationQuery.cs` | 35 |
| `GetFailureExplanation` | Method | `ClassifiedAds.Modules.LlmAssistant/Controllers/FailureExplanationsController.cs` | 30 |
| `CreateEndpointMetadata` | Method | `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationTestData.cs` | 131 |
| `CreatePrompt` | Method | `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationTestData.cs` | 159 |
| `Build_ShouldContainDeterministicSections` | Method | `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationPromptBuilderTests.cs` | 8 |
| `Build_ShouldNotContainRawSecrets` | Method | `ClassifiedAds.UnitTests/LlmAssistant/FailureExplanationPromptBuilderTests.cs` | 31 |
| `Build` | Method | `ClassifiedAds.Modules.LlmAssistant/Services/FailureExplanationPromptBuilder.cs` | 25 |
| `HandleAsync_MetadataAvailable_ShouldLoadMetadataAndReturnResult` | Method | `ClassifiedAds.UnitTests/LlmAssistant/ExplainTestFailureCommandHandlerTests.cs` | 35 |
| `HandleAsync_MetadataMissing_ShouldStillWork` | Method | `ClassifiedAds.UnitTests/LlmAssistant/ExplainTestFailureCommandHandlerTests.cs` | 73 |
| `HandleAsync_MetadataOptionalPath_ShouldSkipMetadataLookupAndInvokeExplainer` | Method | `ClassifiedAds.UnitTests/LlmAssistant/ExplainTestFailureCommandHandlerTests.cs` | 105 |

## Execution Flows

| Flow | Type | Steps |
|------|------|-------|
| `GetCachedAsync → FailureExplanationActualResultDto` | cross_community | 4 |
| `GetCachedAsync → FailureExplanationFailureReasonDto` | cross_community | 4 |
| `ExplainAsync → FailureExplanationActualResultDto` | cross_community | 4 |
| `ExplainAsync → FailureExplanationFailureReasonDto` | cross_community | 4 |
| `GetCachedAsync → TestFailureExplanationContextDto` | cross_community | 3 |
| `ExplainAsync → TestFailureExplanationContextDto` | cross_community | 3 |

## Connected Areas

| Area | Connections |
|------|-------------|
| Services | 19 calls |
| TestExecution | 6 calls |
| TestGeneration | 3 calls |
| ConfigurationOptions | 2 calls |
| Controllers | 2 calls |
| Queries | 1 calls |

## How to Explore

1. `gitnexus_context({name: "GetFailureExplanationQuery"})` — see callers and callees
2. `gitnexus_query({query: "llmassistant"})` — find related execution flows
3. Read key files listed above for implementation details
