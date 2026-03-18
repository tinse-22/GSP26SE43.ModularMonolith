# TASK: Implement FE-16 - User Feedback On LLM Suggestions

## CONTEXT

You are implementing FE-16 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Important current-state note:

- FE-15 already exists in the current codebase under `ClassifiedAds.Modules.TestGeneration`.
- Do not redesign FE-15 from scratch.
- Extend the existing FE-15 implementation cleanly.

Primary module: `ClassifiedAds.Modules.TestGeneration`
Supporting module: `ClassifiedAds.Modules.LlmAssistant`
Migration project: `ClassifiedAds.Migrator`

Read these spec files first:

- `docs/features/FE-16-llm-feedback/requirement.json`
- `docs/features/FE-16-llm-feedback/workflow.json`
- `docs/features/FE-16-llm-feedback/contracts.json`
- `docs/features/FE-16-llm-feedback/implementation-map.json`
- `docs/features/FE-16-llm-feedback/README.md`

## TASK CLASSIFICATION

This task is NOT docs-only.

Expected classification:

- `Application code only`
- `Touches EF model / DbContext / migration / seed / connection settings`

Default expectation:

- no new module
- no new host project reference
- no new runtime dependency
- no Dockerfile change
- no compose change

But per `AGENTS.md`, you must still verify Docker/compose conclusions and report them.

## HARD CONSTRAINTS

- MUST build on the existing FE-15 `LlmSuggestion` workflow already implemented in `ClassifiedAds.Modules.TestGeneration`.
- MUST add durable feedback persistence in `ClassifiedAds.Modules.TestGeneration`.
- MUST NOT store durable feedback state in `LlmSuggestionCache`.
- MUST keep `ClassifiedAds.Modules.LlmAssistant` as cache/audit support only.
- MUST expose feedback through the existing FE-15 suggestion list/detail surface instead of inventing duplicate suggestion read APIs.
- MUST make prompt refinement cache-safe: if feedback changes prompt input, cache key/fingerprint must also change.
- MUST keep FE-16 additive to FE-06 and FE-15.
- MUST use ASCII in new files.

## RECOMMENDED FEATURE SHAPE

Implement FE-16 v1 as:

1. one durable feedback entity per suggestion/user
2. one idempotent feedback write endpoint
3. feedback summary embedded in FE-15 suggestion models
4. one internal service that aggregates feedback into endpoint-scoped prompt context
5. one feedback fingerprint included in `LlmScenarioSuggester` cache-key material

Recommended feedback signals:

- `Helpful`
- `NotHelpful`

Notes should be optional but trimmed and length-limited.

## REQUIRED API SURFACE

Add:

1. `PUT /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}/feedback`
   - Permission: reuse `Permission:UpdateTestCase`
   - Request body: `UpsertLlmSuggestionFeedbackRequest`
   - Response: `200 OK` + `LlmSuggestionFeedbackModel`
   - Behavior: create or update the current user's feedback for that suggestion

Also extend existing FE-15 responses:

2. `GET /api/test-suites/{suiteId:guid}/llm-suggestions`
3. `GET /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}`

Both should include:

- `CurrentUserFeedback`
- `FeedbackSummary`

## PERSISTENCE TO ADD

Add a new entity under `ClassifiedAds.Modules.TestGeneration/Entities/`:

- `LlmSuggestionFeedback`

Recommended fields:

- `Id : Guid`
- `SuggestionId : Guid`
- `TestSuiteId : Guid`
- `EndpointId : Guid?`
- `UserId : Guid`
- `FeedbackSignal : enum`
- `Notes : string?`
- `CreatedDateTime : DateTimeOffset`
- `UpdatedDateTime : DateTimeOffset?`
- `RowVersion : byte[]`

Recommended rules:

- unique row per `(SuggestionId, UserId)`
- reject feedback for `Superseded` suggestions
- trim notes
- limit notes length
- no test-case creation side effects

## PROMPT-REFINEMENT REQUIREMENT

Implement one internal service, for example:

- `ILlmSuggestionFeedbackContextService`
- `LlmSuggestionFeedbackContextService`

Responsibilities:

1. aggregate recent feedback by `TestSuiteId + EndpointId`
2. produce a compact, sanitized text summary per endpoint
3. produce a stable fingerprint/hash for the aggregated feedback input
4. return empty context safely when no feedback exists

Then update `LlmScenarioSuggester`:

1. load feedback context before cache lookup
2. include the feedback fingerprint in cache-key construction
3. include feedback context in prompt/payload generation
4. keep graceful fallback when feedback aggregation fails

## FILES TO ADD / MODIFY

Add:

- `ClassifiedAds.Modules.TestGeneration/Entities/LlmSuggestionFeedback.cs`
- `ClassifiedAds.Modules.TestGeneration/DbConfigurations/LlmSuggestionFeedbackConfiguration.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/UpsertLlmSuggestionFeedbackCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionFeedbackModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionFeedbackSummaryModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/UpsertLlmSuggestionFeedbackRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/ILlmSuggestionFeedbackContextService.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionFeedbackContextService.cs`

Modify:

- `ClassifiedAds.Modules.TestGeneration/Persistence/TestGenerationDbContext.cs`
- `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/LlmSuggestionsController.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/LlmSuggestionModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionsQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetLlmSuggestionDetailQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/N8nBoundaryNegativePayload.cs`
- optionally prompt-context builder files if you choose repo-owned prompt enrichment instead of payload-only enrichment
- `ClassifiedAds.Migrator/...` migration files for `TestGeneration`

## TESTS

Add at least these unit test groups:

- `UpsertLlmSuggestionFeedbackCommandHandlerTests`
- `LlmSuggestionFeedbackContextServiceTests`
- `GetLlmSuggestionsQueryHandlerTests`
- `GetLlmSuggestionDetailQueryHandlerTests`
- `LlmScenarioSuggesterTests`

Test specifically:

- owner validation
- archived suite rejection
- suggestion not found
- superseded suggestion cannot be feedbacked
- new feedback row created
- existing feedback row updated
- helpful/not-helpful summary counts
- feedback notes are trimmed/truncated
- feedback fingerprint changes when feedback changes
- cache key changes when feedback context changes
- no-feedback path still works

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

If Docker-related files actually changed, also run the compose/build checks required by `AGENTS.md`.

## DONE CRITERIA

- FE-16 feedback persistence exists in `TestGeneration`
- feedback write API exists and is wired
- FE-15 list/detail surfaces include feedback metadata
- feedback context is consumable by `LlmScenarioSuggester`
- feedback-aware cache fingerprinting is implemented
- migration exists in `ClassifiedAds.Migrator`
- targeted tests were added and executed
- migrator verification was executed and passed

## PHASED EXECUTION

Do not implement FE-16 in one giant step unless explicitly requested.

Preferred order:

1. `PROMPT-PHASE-1-FEEDBACK-PERSISTENCE.md`
2. `PROMPT-PHASE-2-FEEDBACK-CONTEXT-AND-PROMPT-REFINEMENT.md`
3. `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
4. `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`
