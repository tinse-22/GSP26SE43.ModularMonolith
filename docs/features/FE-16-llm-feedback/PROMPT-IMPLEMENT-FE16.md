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

Expected overall classification:

- `Application code only`
- `Touches EF model / DbContext / migration / seed / connection settings`

Default expectation:

- no new module
- no new host project reference
- no new runtime dependency
- no Dockerfile change
- no compose change

Per `AGENTS.md`, you must still verify the Docker/compose conclusion and report it explicitly.

## DEFAULT IMPLEMENTATION CHOICES

Use these choices unless real code constraints force a different path:

- Keep FE-16 entirely inside `ClassifiedAds.Modules.TestGeneration`.
- Add a dedicated entity `LlmSuggestionFeedback` with a dedicated enum such as `LlmSuggestionFeedbackSignal`.
- Store `FeedbackSignal` as a string-backed enum in EF, consistent with existing `LlmSuggestion` enum mapping style.
- Copy `TestSuiteId` and `EndpointId` from the owning suggestion onto the feedback row for cheap aggregation.
- Normalize `Notes` with trim + null-if-empty behavior, validate a reasonable max length before persistence, and store the column as `text`.
- Follow the repo's current concurrency pattern: `RowVersion` is application-managed and refreshed with `Guid.NewGuid().ToByteArray()`.
- Build the feedback fingerprint from canonical aggregated output, not from raw DB row order, timestamps, or `UserId`.
- Prefer payload enrichment by adding `FeedbackContext` to `N8nBoundaryEndpointPayload`.
- Keep `ObservationConfirmationPromptBuilder` unchanged unless payload enrichment proves impossible.
- Follow the repo pattern where commands/queries and their handlers live in the same file.

## HARD CONSTRAINTS

- MUST build on the existing FE-15 `LlmSuggestion` workflow already implemented in `ClassifiedAds.Modules.TestGeneration`.
- MUST add durable feedback persistence in `ClassifiedAds.Modules.TestGeneration`.
- MUST NOT store durable feedback state in `LlmSuggestionCache`.
- MUST keep `ClassifiedAds.Modules.LlmAssistant` as cache/audit support only.
- MUST expose feedback through the existing FE-15 suggestion list/detail surface instead of inventing duplicate suggestion read APIs.
- MUST make prompt refinement cache-safe: if feedback changes prompt input, cache key/fingerprint must also change.
- MUST keep FE-16 additive to FE-06 and FE-15.
- MUST use ASCII in new files.

## ARCHITECTURE TO IMPLEMENT

### 1. Durable Feedback Persistence

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

Persistence rules:

- unique row per `(SuggestionId, UserId)`
- FK to `LlmSuggestion`
- cascade delete is acceptable because feedback lifecycle follows suggestion lifecycle
- `Notes` stored as `text`
- no separate durable state in `LlmAssistant`
- no test-case creation side effects

### 2. Feedback Context And Cache Fingerprint

Implement one internal service:

- `ILlmSuggestionFeedbackContextService`
- `LlmSuggestionFeedbackContextService`

Responsibilities:

1. aggregate feedback for the requested `TestSuiteId + EndpointId` set
2. ignore suggestions that are `Superseded`
3. ignore suggestions without `EndpointId`
4. sanitize and truncate notes before they become prompt context
5. build a compact endpoint-scoped context string
6. build a stable feedback fingerprint from canonical aggregated output
7. return an empty context plus a stable empty fingerprint when no usable feedback exists

Recommended aggregation behavior:

- group by `EndpointId`
- include helpful and not-helpful counts
- include only a bounded number of recent note snippets per endpoint
- sort endpoint groups and note snippets deterministically before hashing
- do not include raw `UserId`, `RowVersion`, or raw timestamps in the fingerprint material

Then update `LlmScenarioSuggester`:

1. load feedback context before cache lookup
2. include the feedback fingerprint in cache-key construction
3. enrich `N8nBoundaryEndpointPayload` with endpoint-scoped `FeedbackContext`
4. keep graceful fallback when feedback aggregation fails

### 3. HTTP + CQRS Surface

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

Write-path rules:

- reuse `LlmSuggestionsController`; do not add a second controller
- validate suite existence and suite ownership using the current FE-15 pattern
- reject archived suites
- load suggestion by `suggestionId + testSuiteId`
- reject `Superseded` suggestions
- upsert the row for `(SuggestionId, CurrentUserId)`
- refresh `UpdatedDateTime` and `RowVersion` on update

### 4. Read-Model Wiring

Extend `LlmSuggestionModel` with:

- `CurrentUserFeedback`
- `FeedbackSummary`

Query rules:

- batch-load feedback rows for the selected suggestions
- compute current-user feedback and summary counts in memory
- do not query feedback per suggestion in a loop
- keep FE-15 filtering semantics intact

### 5. Leave These Areas Unchanged Unless Blocked

- `ClassifiedAds.Modules.LlmAssistant` should not gain durable feedback entities.
- `ObservationConfirmationPromptBuilder` should stay focused on spec/business-rule prompting.
- `ClassifiedAds.WebAPI/Program.cs`, Dockerfiles, and `docker-compose.yml` should remain unchanged unless the code truly adds a new dependency or reference.

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
- feedback notes are trimmed and truncated before prompt use
- feedback fingerprint is stable for semantically identical feedback
- feedback fingerprint changes when aggregated feedback changes
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
- one feedback row per suggestion/user is enforced
- feedback write API exists and is wired through the existing controller surface
- FE-15 list/detail surfaces include current-user feedback and summary metadata
- feedback context is consumable by `LlmScenarioSuggester`
- feedback-aware cache fingerprinting is implemented
- the solution uses payload enrichment rather than broad prompt-builder rewrites unless a blocker forced otherwise
- migration exists in `ClassifiedAds.Migrator`
- targeted tests were added and executed
- migrator verification was executed and passed
- Docker/compose conclusions were explicitly checked and reported

## PHASED EXECUTION

Do not implement FE-16 in one giant step unless explicitly requested.

Preferred order:

1. `PROMPT-PHASE-1-FEEDBACK-PERSISTENCE.md`
2. `PROMPT-PHASE-2-FEEDBACK-CONTEXT-AND-PROMPT-REFINEMENT.md`
3. `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
4. `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`
