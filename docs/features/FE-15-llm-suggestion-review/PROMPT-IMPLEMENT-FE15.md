# TASK: Implement FE-15 - LLM Suggestion Review Interface

## CONTEXT

You are implementing FE-15 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Primary module: `ClassifiedAds.Modules.TestGeneration`
Supporting modules: `ClassifiedAds.Modules.LlmAssistant`, `ClassifiedAds.Modules.Subscription`
Migration project: `ClassifiedAds.Migrator`

Read these spec files first:

- `docs/features/FE-15-llm-suggestion-review/requirement.json`
- `docs/features/FE-15-llm-suggestion-review/workflow.json`
- `docs/features/FE-15-llm-suggestion-review/contracts.json`
- `docs/features/FE-15-llm-suggestion-review/implementation-map.json`
- `docs/features/FE-15-llm-suggestion-review/README.md`

## TASK CLASSIFICATION

This task is NOT docs-only.

Expected classification:

- `Application code only`
- `Touches EF model / DbContext / migration / seed / connection settings`

By default, FE-15 should NOT require new host module registration or Docker wiring because `TestGeneration` and `LlmAssistant` are already wired. Still, per `AGENTS.md`, you must explicitly verify whether Docker/compose changes are required and report that conclusion.

## HARD CONSTRAINTS

- MUST keep `ClassifiedAds.Modules.TestGeneration` as the primary FE-15 runtime module.
- MUST keep `ClassifiedAds.Modules.LlmAssistant` as support for raw LLM cache + interaction audit only.
- MUST NOT store durable review state in `LlmSuggestionCache`.
- MUST add a durable EF Core entity in `TestGeneration` for per-suggestion review lifecycle.
- MUST create the migration in `ClassifiedAds.Migrator` only.
- MUST keep FE-15 additive to FE-06. Do NOT break the existing `generate-boundary-negative` endpoint behavior.
- MUST reuse the existing FE-06 `ILlmScenarioSuggester` pipeline for raw suggestion generation.
- MUST extract and reuse shared mapping logic for `LlmSuggestedScenario` -> `TestCase` so FE-06 and FE-15 do not duplicate this conversion.
- MUST use ASCII in new files.

## ARCHITECTURE DECISION TO IMPLEMENT

Implement FE-15 inside `ClassifiedAds.Modules.TestGeneration`, not inside `ClassifiedAds.Modules.LlmAssistant`.

Reason:

- `TestGeneration` already owns `TestSuite`, `TestCase`, `TestCaseChangeLog`, `TestSuiteVersion`, ownership validation, and approval-style workflows.
- FE-15 ultimately creates `TestCase` rows, so the write-side belongs in `TestGeneration`.
- `LlmAssistant` should continue to own raw LLM audit/cache support only.

Do NOT implement FE-15 by letting `LlmAssistant` write directly into `TestGeneration` tables.

## REQUIRED API SURFACE

Implement these endpoints in `ClassifiedAds.Modules.TestGeneration`:

1. `POST /api/test-suites/{suiteId:guid}/llm-suggestions/generate`
   - Permission: reuse `Permission:GenerateBoundaryNegativeTestCases`
   - Request body: `GenerateLlmSuggestionPreviewRequest`
   - Response: `201 Created` + `GenerateLlmSuggestionPreviewResultModel`
   - Purpose: create durable pending suggestions for review

2. `GET /api/test-suites/{suiteId:guid}/llm-suggestions`
   - Permission: reuse `Permission:GetTestCases`
   - Query filters: `reviewStatus?`, `testType?`, `endpointId?`
   - Response: `200 OK` + `List<LlmSuggestionModel>`

3. `GET /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}`
   - Permission: reuse `Permission:GetTestCases`
   - Response: `200 OK` + `LlmSuggestionModel`

4. `PUT /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}/review`
   - Permission: reuse `Permission:UpdateTestCase`
   - Request body: `ReviewLlmSuggestionRequest`
   - Response: `200 OK` + `LlmSuggestionModel`
   - Action values: `Approve`, `Reject`, `Modify`

## PERSISTENCE TO ADD

Add a new entity under `ClassifiedAds.Modules.TestGeneration/Entities/`:

- `LlmSuggestion`

Recommended fields:

- `Id : Guid`
- `TestSuiteId : Guid`
- `EndpointId : Guid?`
- `CacheKey : string`
- `DisplayOrder : int`
- `SuggestionType : enum`
- `TestType : TestType`
- `SuggestedName : string`
- `SuggestedDescription : string?`
- `SuggestedRequest : string` (`jsonb`)
- `SuggestedExpectation : string` (`jsonb`)
- `SuggestedVariables : string?` (`jsonb`)
- `SuggestedTags : string?` (`jsonb`)
- `Priority : TestPriority`
- `ReviewStatus : enum`
- `ReviewedById : Guid?`
- `ReviewedAt : DateTimeOffset?`
- `ReviewNotes : string?`
- `ModifiedContent : string?` (`jsonb`)
- `AppliedTestCaseId : Guid?`
- `LlmModel : string?`
- `TokensUsed : int?`

Recommended review-status enum:

- `Pending`
- `Approved`
- `Rejected`
- `ModifiedAndApproved`
- `Superseded`

Entity rules:

- existing pending rows for the same suite should be marked `Superseded` before a new preview set is inserted
- approved/modified rows must set `AppliedTestCaseId`
- reject must NOT create `TestCase`

## REQUIRED MIGRATION WORK

Because FE-15 adds an EF entity, you MUST:

1. add the entity + configuration + DbSet
2. create a migration in `ClassifiedAds.Migrator`
3. build the migrator
4. run `--verify-migrations`

Use the repo-standard commands from `AGENTS.md`:

```powershell
dotnet ef migrations add AddLlmSuggestions `
  --context TestGenerationDbContext `
  --project ClassifiedAds.Migrator `
  --startup-project ClassifiedAds.Migrator `
  --output-dir Migrations/TestGeneration
```

Then:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
```

And:

```powershell
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If you use a different migration name, report it explicitly.

## DOCKER / HOST WIRING EXPECTATION

Default expectation:

- no new module
- no new host project reference
- no new runtime dependency
- no Dockerfile change
- no compose change

But you must still verify that conclusion per `AGENTS.md`.

Specifically inspect:

- `ClassifiedAds.Migrator/Dockerfile`
- `ClassifiedAds.WebAPI/Dockerfile`
- `ClassifiedAds.Background/Dockerfile`
- `docker-compose.yml`

If you end up adding any new project reference, module registration, or host dependency, then update Docker/compose accordingly and run the required Docker checks.

## SHARED MATERIALIZER REQUIREMENT

Do NOT keep two separate copies of the same LLM-to-TestCase mapping logic.

Current FE-06 logic already converts `LlmSuggestedScenario` into:

- `TestCase`
- `TestCaseRequest`
- `TestCaseExpectation`
- `TestCaseVariable`

Refactor that logic into a shared service, for example:

- `ILlmSuggestionMaterializer`
- `LlmSuggestionMaterializer`

This service must be reusable by:

1. `BoundaryNegativeTestCaseGenerator` (FE-06 existing flow)
2. `ReviewLlmSuggestionCommandHandler` (FE-15 approve/modify flow)

Reuse existing helpers where possible:

- `ITestCaseRequestBuilder`
- `ITestCaseExpectationBuilder`

## GENERATE PREVIEW FLOW

Implement `GenerateLlmSuggestionPreviewCommand` with this behavior:

1. validate `TestSuiteId`, `SpecificationId`
2. load suite
3. verify owner
4. reject archived suite
5. require approved API order via `IApiTestOrderGateService.RequireApprovedOrderAsync(...)`
6. check `MaxLlmCallsPerMonth` using `ISubscriptionLimitGatewayService`
7. call existing `ILlmScenarioSuggester.SuggestScenariosAsync(...)`
8. mark previous pending suggestions for this suite as `Superseded`
9. insert one `LlmSuggestion` row per suggested scenario
10. if there are live LLM results worth counting, increment LLM usage using existing subscription pattern
11. return `GenerateLlmSuggestionPreviewResultModel`

Important:

- keep behavior deterministic with respect to suite + approved order
- keep the generate-preview API independent from rule-based path/body mutations
- if suggestion count is zero, return success with zero counts and do not insert junk rows

## REVIEW FLOW

Implement `ReviewLlmSuggestionCommand` with these rules:

### Common rules

- load suite
- verify owner
- load suggestion by `suggestionId + testSuiteId`
- verify `rowVersion`
- only allow review on `Pending`

### Reject

- `reviewNotes` required
- set `ReviewStatus = Rejected`
- set `ReviewedById`, `ReviewedAt`, `ReviewNotes`
- save
- do not create `TestCase`
- do not increment suite version

### Approve

- materialize from original suggestion payload
- check `MaxTestCasesPerSuite`
- transactionally create:
  - `TestCase`
  - `TestCaseRequest`
  - `TestCaseExpectation`
  - `TestCaseVariable` rows
  - `TestCaseChangeLog`
  - `TestSuiteVersion`
  - suite version update
  - suggestion review fields update
- set `ReviewStatus = Approved`
- set `AppliedTestCaseId`
- increment test-case subscription usage after successful commit

### Modify

- `modifiedContent` required
- materialize from modified payload instead of original payload
- same transaction semantics as approve
- set `ReviewStatus = ModifiedAndApproved`
- persist `ModifiedContent`
- set `AppliedTestCaseId`

### Idempotency

If suggestion was already approved and already has `AppliedTestCaseId`, prefer controlled idempotent behavior over creating duplicate test cases.

## MODELING RULES

Use JSON payload columns for suggestion snapshots:

- `SuggestedRequest`
- `SuggestedExpectation`
- `SuggestedVariables`
- `SuggestedTags`
- `ModifiedContent`

This is preferred over exploding every preview field into many table columns.

Still keep searchable scalar columns:

- `TestSuiteId`
- `EndpointId`
- `CacheKey`
- `DisplayOrder`
- `SuggestionType`
- `TestType`
- `ReviewStatus`
- `AppliedTestCaseId`

Add indexes at least for:

- `(TestSuiteId, ReviewStatus)`
- `EndpointId`
- `CacheKey`
- `AppliedTestCaseId`

## FILES TO ADD / MODIFY

Add:

- `ClassifiedAds.Modules.TestGeneration/Entities/LlmSuggestion.cs`
- `ClassifiedAds.Modules.TestGeneration/DbConfigurations/LlmSuggestionConfiguration.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/LlmSuggestionsController.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/ReviewLlmSuggestionCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionsQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionDetailQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/GenerateLlmSuggestionPreviewResultModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/GenerateLlmSuggestionPreviewRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/ReviewLlmSuggestionRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/ILlmSuggestionMaterializer.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs`

Modify:

- `ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs`
- `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`
- `ClassifiedAds.Migrator/...` migration files for `TestGeneration`

Only modify `ILlmScenarioSuggester` / `LlmScenarioSuggester` if needed for a clean `ForceRefresh` story. Do not change them unnecessarily.

## TESTS

Add at least these unit test groups:

- `GenerateLlmSuggestionPreviewCommandHandlerTests`
- `GetLlmSuggestionsQueryHandlerTests`
- `GetLlmSuggestionDetailQueryHandlerTests`
- `ReviewLlmSuggestionCommandHandlerTests`
- `LlmSuggestionMaterializerTests`
- `BoundaryNegativeTestCaseGeneratorTests`

Test specifically:

- owner validation
- archived suite rejection
- approved-order gate failure
- zero-suggestion result
- pending suggestions become `Superseded`
- reject requires notes
- modify requires modified payload
- approve creates exactly one `TestCase`
- modify uses modified payload, not original payload
- concurrency conflict on stale `rowVersion`
- idempotent approve path does not duplicate `TestCase`
- FE-06 existing generator still passes after refactor

## VERIFICATION

At minimum run:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestGeneration'
```

And:

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

And the migration checks:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If Docker-related files actually changed, also run the corresponding compose/build checks required by `AGENTS.md`.

## DONE CRITERIA

- FE-15 preview/list/detail/review APIs exist and are wired
- durable `LlmSuggestion` persistence exists in `TestGeneration`
- migration exists in `ClassifiedAds.Migrator`
- FE-06 still works after shared materializer refactor
- approve/modify transactionally create `TestCase` graph
- reject does not create `TestCase`
- targeted tests were added and executed
- migrator verification was executed and passed
