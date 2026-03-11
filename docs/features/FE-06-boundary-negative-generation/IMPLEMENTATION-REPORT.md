# FE-06 Implementation Report — Boundary & Negative Test Case Generation

## Quality Gates

| Gate | Result |
|------|--------|
| `dotnet build` | 0 errors |
| `dotnet test` (TestGeneration) | 165/165 passed |
| `dotnet test` (full suite) | 672/675 passed (3 failures are pre-existing `Architecture/ModuleBoundaryTests` — missing `Product` module assembly, unrelated to FE-06) |
| `dotnet ef migrations has-pending-model-changes` | No changes detected |

## Fixes Applied (gap analysis vs spec)

| Issue | File | Fix |
|-------|------|-----|
| Per-field mutations ran for GET/DELETE/HEAD/OPTIONS | `BodyMutationEngine.cs` | Added early return for non-POST/PUT/PATCH methods |
| Webhook name `"generate-boundary-negative"` didn't match spec | `N8nWebhookNames.cs` | Changed to `"generate-boundary-negative-scenarios"` |
| DI registration used Singleton instead of Scoped | `ServiceCollectionExtensions.cs` | Changed `AddSingleton<IBodyMutationEngine>` to `AddScoped` |
| Missing test: GET/DELETE/HEAD/OPTIONS return empty | `BodyMutationEngineTests.cs` | Added `GenerateMutations_Should_ReturnEmptyList_ForNonBodyMethods` (4 InlineData) |
| Missing test: overflow skips boolean/array/object | `BodyMutationEngineTests.cs` | Added `GenerateMutations_Should_SkipOverflow_ForBooleanArrayObjectFields` |
| Missing test: BuildBaseBody default value strategy | `BodyMutationEngineTests.cs` | Added `GenerateMutations_Should_UseDefaultValueFromParameter_InBaseBody` |
| Missing test: endpointsCovered distinct count | `BoundaryNegativeTestCaseGeneratorTests.cs` | Added `GenerateAsync_Should_CountDistinctEndpointsCovered` |
| Missing test: ClassifyPathMutationType logic | `BoundaryNegativeTestCaseGeneratorTests.cs` | Added `GenerateAsync_Should_ClassifyPathMutationType_Correctly` (11 InlineData) |
| Missing test: SanitizeName truncation | `BoundaryNegativeTestCaseGeneratorTests.cs` | Added `GenerateAsync_Should_TruncateName_WhenScenarioNameExceeds200Chars` |
| Missing test: SanitizeName fallback | `BoundaryNegativeTestCaseGeneratorTests.cs` | Added `GenerateAsync_Should_UseFallbackName_WhenScenarioNameIsNull` |
| Missing test: audit log failure graceful | `LlmScenarioSuggesterTests.cs` | Added `SuggestScenariosAsync_Should_NotThrow_WhenAuditLogFails` |
| Missing test: cache save failure graceful | `LlmScenarioSuggesterTests.cs` | Added `SuggestScenariosAsync_Should_NotThrow_WhenCacheSaveFails` |
| Missing test: cache key determinism | `LlmScenarioSuggesterTests.cs` | Added `SuggestScenariosAsync_Should_ProduceDeterministicCacheKey` |
| Missing test: gate fail returns 409 | `GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs` | Added `HandleAsync_Should_ThrowConflict_WhenGateFails` |
| Missing test: LLM call limit exceeded | `GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs` | Added `HandleAsync_Should_ThrowValidation_WhenLlmCallLimitExceeded` |
| Missing test: empty result skips transaction | `GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs` | Added `HandleAsync_Should_SkipTransaction_WhenGeneratorReturnsEmpty` |
| Test webhook name hardcoded wrong | `LlmScenarioSuggesterTests.cs` | Updated mock setup/verify strings to `"generate-boundary-negative-scenarios"` |

## Architecture Overview

FE-06 implements 3 independent pipelines for boundary/negative test case generation, toggled via boolean flags:

```
PIPE-01: Path Parameter Mutations   (rule-based)   → IncludePathMutations
PIPE-02: Body Mutations             (rule-based)   → IncludeBodyMutations
PIPE-03: LLM Scenario Suggestions   (llm-assisted) → IncludeLlmSuggestions
```

**API Endpoint:**
```
POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative
Authorization: [Authorize] + [Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
Success: 201 Created
```

## Files Inventory

### Models (`ClassifiedAds.Modules.TestGeneration/Models/`)

| File | Description |
|------|-------------|
| `BodyMutation.cs` | Single body mutation variant (MutationType, Label, MutatedBody, TargetFieldName, ExpectedStatusCode, SuggestedTestType) |
| `GenerateBoundaryNegativeResultModel.cs` | API response model with per-pipeline counts and test case summaries |
| `N8nBoundaryNegativePayload.cs` | n8n webhook request payload (includes `N8nBoundaryEndpointPayload`, `N8nParameterDetail`) |
| `N8nBoundaryNegativeResponse.cs` | n8n webhook response (includes `N8nSuggestedScenario`, reuses `N8nTestCaseRequest`/`N8nTestCaseExpectation`/`N8nTestCaseVariable`) |
| `Requests/GenerateBoundaryNegativeTestCasesRequest.cs` | API request model (SpecificationId, ForceRegenerate, 3 pipeline flags) |

### Interfaces (`ClassifiedAds.Modules.TestGeneration/Services/`)

| File | Description |
|------|-------------|
| `IBodyMutationEngine.cs` | Rule-based body mutation engine interface + `BodyMutationContext` |
| `ILlmScenarioSuggester.cs` | LLM scenario suggester interface + `LlmScenarioSuggestionContext`, `LlmScenarioSuggestionResult`, `LlmSuggestedScenario` |
| `IBoundaryNegativeTestCaseGenerator.cs` | Orchestrator interface + `BoundaryNegativeOptions`, `BoundaryNegativeGenerationResult` |

### Services (`ClassifiedAds.Modules.TestGeneration/Services/`)

| File | Description |
|------|-------------|
| `BodyMutationEngine.cs` | Stateless engine generating 6 mutation types: emptyBody (3 variants), malformedJson (3 variants), missingRequired (per required field), typeMismatch (per field), overflow (numeric/string only), invalidEnum (schema-based). Returns empty for GET/DELETE/HEAD/OPTIONS. Supports schema-based mutations from JSON schema. |
| `LlmScenarioSuggester.cs` | 6-step pipeline: build cache key (SHA256) → check cache (all-or-nothing) → build prompts → call n8n webhook (`generate-boundary-negative-scenarios`) → save audit (graceful) → parse and cache (graceful) |
| `BoundaryNegativeTestCaseGenerator.cs` | Orchestrator: fetch metadata → fetch parameter details → per-endpoint path/body mutations → LLM suggestions → assign sequential OrderIndex. Includes `ClassifyPathMutationType`, `SanitizeName`, `ParsePriority`, `ParseHttpMethod`, `SerializeTags` helpers. |

### Command + Handler (`ClassifiedAds.Modules.TestGeneration/Commands/`)

| File | Description |
|------|-------------|
| `GenerateBoundaryNegativeTestCasesCommand.cs` | Command (ICommand) + 10-step handler: validate inputs → load & validate suite → FE-05A gate → check existing cases → subscription limits → delete old cases → generate → empty result check → persist in transaction → post-transaction usage increment |

### Integration Points

| File | Change |
|------|--------|
| `Authorization/Permissions.cs` | Added `GenerateBoundaryNegativeTestCases` constant |
| `Constants/N8nWebhookNames.cs` | Added `GenerateBoundaryNegative = "generate-boundary-negative-scenarios"` |
| `Controllers/TestCasesController.cs` | Added `POST generate-boundary-negative` endpoint |
| `ServiceCollectionExtensions.cs` | Registered `IBodyMutationEngine` (scoped), `ILlmScenarioSuggester` (scoped), `IBoundaryNegativeTestCaseGenerator` (scoped) |

### Cross-Module Interfaces (pre-existing, not modified)

| Interface | Module |
|-----------|--------|
| `IApiEndpointMetadataService` | ApiDocumentation |
| `IApiEndpointParameterDetailService` | ApiDocumentation |
| `IPathParameterMutationGatewayService` | ApiDocumentation |
| `ILlmAssistantGatewayService` | LlmAssistant |
| `ISubscriptionLimitGatewayService` | Subscription |
| `IApiTestOrderGateService` | TestGeneration (FE-05) |
| `ITestCaseRequestBuilder` | TestGeneration (FE-05B) |
| `ITestCaseExpectationBuilder` | TestGeneration (FE-05B) |
| `IObservationConfirmationPromptBuilder` | TestGeneration (FE-05B) |
| `IN8nIntegrationService` | TestGeneration (FE-05B) |
| `EndpointPromptContextMapper` | TestGeneration (FE-05B) |

## Unit Tests (`ClassifiedAds.UnitTests/TestGeneration/`)

### BodyMutationEngineTests.cs — 16 tests

| Test | Verifies |
|------|----------|
| `GenerateMutations_Should_ReturnEmptyBodyMutations_ForPostMethod` | 3 emptyBody + 3 malformedJson mutations for POST |
| `GenerateMutations_Should_ReturnMalformedJsonMutations_ForPostMethod` | Missing closing brace, truncated value, plain text variants |
| `GenerateMutations_Should_GenerateMissingRequiredField_ForEachRequiredParam` | One mutation per required field, non-required excluded |
| `GenerateMutations_Should_GenerateTypeMismatch_ForIntegerField` | Integer field gets "not_a_number" value |
| `GenerateMutations_Should_GenerateTypeMismatch_ForStringField` | String field gets 12345 value |
| `GenerateMutations_Should_GenerateOverflow_ForInt32Field` | Int32 overflow uses 2147483648 (MaxValue + 1) |
| `GenerateMutations_Should_GenerateInvalidEnum_WhenSchemaHasEnum` | Enum fields get INVALID_ENUM_VALUE_ prefix |
| `GenerateMutations_Should_HandleEmptyParameterList_ReturningOnlyWholeBodyMutations` | Only emptyBody + malformedJson when no params |
| `GenerateMutations_Should_SetCorrectTestType_BoundaryForOverflow` | Overflow mutations are TestType.Boundary |
| `GenerateMutations_Should_ReturnEmptyList_ForNonBodyMethods` | GET/DELETE/HEAD/OPTIONS return empty list (4 InlineData) |
| `GenerateMutations_Should_SkipOverflow_ForBooleanArrayObjectFields` | Overflow skips boolean, array, object fields |
| `GenerateMutations_Should_UseDefaultValueFromParameter_InBaseBody` | BuildBaseBody uses DefaultValue → Examples → fallback |
| `GenerateMutations_Should_SetExpectedStatusCode400_ForAllMutations` | All mutations expect HTTP 400 |

### BoundaryNegativeTestCaseGeneratorTests.cs — 12 tests

| Test | Verifies |
|------|----------|
| `GenerateAsync_Should_OnlyIncludePathMutations_WhenOnlyPathFlagTrue` | Body + LLM not called when disabled |
| `GenerateAsync_Should_OnlyIncludeBodyMutations_WhenOnlyBodyFlagTrue` | Path + LLM not called when disabled |
| `GenerateAsync_Should_OnlyIncludeLlmSuggestions_WhenOnlyLlmFlagTrue` | Path + Body not called, parameter details not fetched |
| `GenerateAsync_Should_CombineAllSources_WhenAllFlagsTrue` | 2 path + 1 body + 2 LLM = 5 total |
| `GenerateAsync_Should_AssignSequentialOrderIndex` | OrderIndex = 0..N-1 |
| `GenerateAsync_Should_SetCorrectTestType_PerSource` | Path boundary_zero → Boundary, Body → Negative, LLM → per scenario |
| `GenerateAsync_Should_SetCorrectTags_PerSource` | path-mutation, body-mutation, llm-suggested tags |
| `GenerateAsync_Should_CountDistinctEndpointsCovered` | 3 test cases across 2 endpoints → EndpointsCovered = 2 |
| `GenerateAsync_Should_ClassifyPathMutationType_Correctly` | boundary/zero/max/overflow → Boundary, else → Negative (11 InlineData) |
| `GenerateAsync_Should_TruncateName_WhenScenarioNameExceeds200Chars` | Name truncated to 200 chars |
| `GenerateAsync_Should_UseFallbackName_WhenScenarioNameIsNull` | Fallback: "{HttpMethod} {Path} - Boundary/Negative" |
| `GenerateAsync_Should_HandleNoEndpointMetadata` | Empty metadata returns empty result without throwing |

### LlmScenarioSuggesterTests.cs — 10 tests

| Test | Verifies |
|------|----------|
| `SuggestScenariosAsync_Should_ReturnCachedResults_WhenCacheHit` | All endpoints cached → return immediately, n8n not called |
| `SuggestScenariosAsync_Should_CallN8n_WhenCacheMiss` | Any endpoint missing cache → full LLM call |
| `SuggestScenariosAsync_Should_SaveInteraction_AfterN8nCall` | Audit log saved with correct UserId, InteractionType, Model |
| `SuggestScenariosAsync_Should_CacheResults_AfterN8nCall` | CacheSuggestionsAsync called once per endpoint |
| `SuggestScenariosAsync_Should_HandleEmptyN8nResponse` | Null scenarios → empty list, no crash |
| `SuggestScenariosAsync_Should_PropagateN8nError` | n8n failure propagates (critical, not graceful) |
| `SuggestScenariosAsync_Should_SetFromCacheTrue_WhenCacheUsed` | FromCache=true, LlmModel/TokensUsed/LatencyMs = null |
| `SuggestScenariosAsync_Should_NotThrow_WhenAuditLogFails` | Audit log failure does NOT throw (graceful degradation) |
| `SuggestScenariosAsync_Should_NotThrow_WhenCacheSaveFails` | Cache save failure does NOT throw (graceful degradation) |
| `SuggestScenariosAsync_Should_ProduceDeterministicCacheKey` | Same input → same cache key (deterministic SHA256) |

### GenerateBoundaryNegativeTestCasesCommandHandlerTests.cs — 16 tests

| Test | Verifies |
|------|----------|
| `HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty` | Guid.Empty → ValidationException |
| `HandleAsync_Should_ThrowValidation_WhenSpecificationIdEmpty` | Guid.Empty → ValidationException |
| `HandleAsync_Should_ThrowValidation_WhenNoIncludeFlagsSet` | All 3 flags false → ValidationException |
| `HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist` | Suite not found → NotFoundException |
| `HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner` | Wrong user → ValidationException |
| `HandleAsync_Should_ThrowValidation_WhenSuiteIsArchived` | Archived suite → ValidationException |
| `HandleAsync_Should_CallGateService` | FE-05A gate invoked |
| `HandleAsync_Should_ThrowConflict_WhenGateFails` | Gate fail → ConflictException (HTTP 409) |
| `HandleAsync_Should_ThrowValidation_WhenExistingCasesAndNoForceRegenerate` | Existing cases + ForceRegenerate=false → ValidationException |
| `HandleAsync_Should_DeleteExistingCases_WhenForceRegenerate` | ForceRegenerate=true deletes existing cases |
| `HandleAsync_Should_ThrowValidation_WhenSubscriptionLimitExceeded` | Subscription denied → ValidationException |
| `HandleAsync_Should_CheckLlmCallLimit_WhenIncludeLlmSuggestions` | LLM call limit checked when LLM flag is true |
| `HandleAsync_Should_ThrowValidation_WhenLlmCallLimitExceeded` | LLM limit denied → ValidationException (HTTP 400) |
| `HandleAsync_Should_GenerateAndPersistTestCases` | TestCase, Request, Expectation, ChangeLog, SuiteVersion all persisted |
| `HandleAsync_Should_IncrementSubscriptionUsage` | Usage incremented post-transaction |
| `HandleAsync_Should_SkipTransaction_WhenGeneratorReturnsEmpty` | Empty result → no transaction, no persistence |
| `HandleAsync_Should_SetResultWithEmptyCases_WhenGeneratorReturnsNone` | Empty generation → result with TotalGenerated=0 |
| `HandleAsync_Should_CreateSuiteVersion` | SuiteVersion created with correct ChangeType |
| `HandleAsync_Should_CreateChangeLog_ForEachCase` | One ChangeLog per test case |

## Validation & Error Map

| HTTP | ReasonCode | Condition |
|------|-----------|-----------|
| 400 | INVALID_INPUT | TestSuiteId or SpecificationId is Guid.Empty |
| 400 | NO_PIPELINE_ENABLED | All 3 pipeline flags are false |
| 400 | OWNERSHIP_DENIED | CreatedById != CurrentUserId |
| 400 | SUITE_ARCHIVED | Suite.Status == Archived |
| 400 | EXISTING_CASES_FOUND | Existing boundary/negative cases AND ForceRegenerate=false |
| 400 | SUBSCRIPTION_LIMIT_EXCEEDED | Exceeds MaxTestCasesPerSuite |
| 400 | LLM_CALL_LIMIT_EXCEEDED | Exceeds MaxLlmCallsPerMonth (IncludeLlmSuggestions=true) |
| 404 | TEST_SUITE_NOT_FOUND | Suite not found |
| 409 | ORDER_CONFIRMATION_REQUIRED | FE-05A gate fail |

## Transaction & Graceful Degradation

**Transaction boundaries:**
- Main persist: all test cases + change logs + suite version + suite update in one `ExecuteInTransactionAsync` with one `SaveChangesAsync`
- Subscription usage increment: after main transaction
- Delete old cases: after subscription check, before main transaction

**Graceful failures (catch → log warning → continue):**
- LLM interaction audit save
- LLM cache save
- LLM cache deserialization (JsonException → treat as cache miss)

**Critical failures (propagate to client):**
- n8n webhook call failure → HTTP 500
- Database transaction failure → HTTP 500

## Hard Constraints Compliance

| Constraint | Status |
|-----------|--------|
| FE-05A gate required before generation | Enforced (Step 3 of handler) |
| Reuse existing entities (TestCase, etc.) | No new entities created |
| No new tables or migrations | Verified via `has-pending-model-changes` |
| No cross-module DbContext access | Communication via contract interfaces only |
| No new cross-module interfaces | All interfaces pre-existing |
| Follow FE-05B patterns | Builders, naming, transactions, DI registration match |
| Vietnamese error messages | All user-facing errors in Vietnamese |
