# TASK: Implement FE-17 - Bulk Review For LLM Suggestions

## CONTEXT

You are implementing FE-17 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Important current-state notes:

- FE-15 already exists in the current codebase under `ClassifiedAds.Modules.TestGeneration`.
- FE-16 feedback already exists and must keep working.
- Current codebase or branch may already contain preliminary FE-17 files such as:
  - `BulkReviewLlmSuggestionsCommand`
  - `BulkReviewLlmSuggestionsRequest`
  - `BulkReviewLlmSuggestionsResultModel`
  - `ILlmSuggestionReviewService`
  - `LlmSuggestionReviewService`
- Do not redesign FE-15 or FE-16 from scratch.
- Inspect and reconcile existing code before adding duplicate types.

Primary module: `ClassifiedAds.Modules.TestGeneration`
Supporting modules: `ClassifiedAds.Modules.Subscription`, indirect compatibility with `ClassifiedAds.Modules.LlmAssistant`
Migration project: `ClassifiedAds.Migrator`

Read these spec files first:

- `docs/features/FE-17-llm-suggestion-bulk-review/requirement.json`
- `docs/features/FE-17-llm-suggestion-bulk-review/workflow.json`
- `docs/features/FE-17-llm-suggestion-bulk-review/contracts.json`
- `docs/features/FE-17-llm-suggestion-bulk-review/implementation-map.json`
- `docs/features/FE-17-llm-suggestion-bulk-review/README.md`
- `docs/features/FE-15-llm-suggestion-review/requirement.json`
- `docs/features/FE-16-llm-feedback/requirement.json`

## TASK CLASSIFICATION

This task is usually NOT docs-only.

Expected overall classification:

- `Application code only`

Default expectation:

- no new migration
- no new module
- no new host project reference
- no new runtime dependency
- no Dockerfile change
- no compose change

Per `AGENTS.md`, you must still verify the migration and Docker/compose conclusion and report it explicitly.

## DEFAULT IMPLEMENTATION CHOICES

Use these choices unless real code constraints force a different path:

- Keep FE-17 entirely inside `ClassifiedAds.Modules.TestGeneration`.
- Reuse durable `LlmSuggestion` rows created by FE-15.
- Reuse `ILlmSuggestionReviewService` for both FE-15 single review and FE-17 bulk review.
- Support only `Approve` and `Reject` actions in FE-17 v1.
- Select the target batch by:
  - `TestSuiteId`
  - `ReviewStatus=Pending`
  - optional `FilterBySuggestionType`
  - optional `FilterByTestType`
  - optional `FilterByEndpointId`
- Order the matched suggestions by `DisplayOrder`.
- Return one `BulkReviewLlmSuggestionsResultModel` summary instead of replaying per-item responses.

## HARD CONSTRAINTS

- MUST build on top of the existing FE-15 durable suggestion review workflow.
- MUST stay additive to FE-16 feedback. Do not break `CurrentUserFeedback`, `FeedbackSummary`, or the feedback endpoint.
- MUST NOT create a new durable bulk-review table unless a real blocker forces it.
- MUST NOT dispatch one single-review command per suggestion from the bulk endpoint.
- MUST validate suite ownership using the current FE-15 pattern.
- MUST process only `Pending` suggestions.
- MUST require `ReviewNotes` when bulk action is `Reject`.
- MUST use ASCII in new files.

## ARCHITECTURE TO IMPLEMENT

### 1. Shared Review Service Reuse

Use one shared service:

- `ILlmSuggestionReviewService`
- `LlmSuggestionReviewService`

Responsibilities:

1. keep FE-15 single review and FE-17 bulk review aligned
2. batch-reject suggestions without materializing test cases
3. batch-approve suggestions with the same FE-15 materialization semantics
4. reuse transaction, change-log, suite-version, and subscription-limit behavior

Preferred batch methods:

- `RejectManyAsync(...)`
- `ApproveManyAsync(...)`

### 2. Bulk Review API Surface

Add or reconcile:

1. `POST /api/test-suites/{suiteId:guid}/llm-suggestions/bulk-review`
   - Permission: `Permission:UpdateTestCase`
   - Request body: `BulkReviewLlmSuggestionsRequest`
   - Response: `200 OK` + `BulkReviewLlmSuggestionsResultModel`

Behavior:

- validate suite existence and suite ownership
- validate action is `Approve` or `Reject`
- validate filter enums if supplied
- require `ReviewNotes` for `Reject`
- load matching `Pending` suggestions in one query
- order by `DisplayOrder`
- call shared review service once for the batch
- return zero-count result if nothing matched

### 3. Approve Rules

Bulk approve must:

- check `MaxTestCasesPerSuite` for the full batch size
- materialize the same `TestCase` graph FE-15 creates
- create `TestCaseChangeLog` rows
- create one `TestSuiteVersion` row for the batch
- update suggestion review fields and `AppliedTestCaseId`
- increment usage once after success

### 4. Reject Rules

Bulk reject must:

- update only review metadata on matched suggestions
- not create `TestCase` rows
- not create `TestSuiteVersion`
- still update `UpdatedDateTime` and `RowVersion`

### 5. Leave These Areas Unchanged Unless Blocked

- `ClassifiedAds.Modules.LlmAssistant`
- `LlmSuggestionFeedback` persistence and FE-16 feedback APIs
- `ClassifiedAds.Migrator` migration set
- host `Program.cs`, Dockerfiles, and `docker-compose.yml`

## FILES TO ADD / MODIFY

Add if missing:

- `ClassifiedAds.Modules.TestGeneration/Commands/BulkReviewLlmSuggestionsCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/Requests/BulkReviewLlmSuggestionsRequest.cs`
- `ClassifiedAds.Modules.TestGeneration/Models/BulkReviewLlmSuggestionsResultModel.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/ILlmSuggestionReviewService.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionReviewService.cs`
- `ClassifiedAds.UnitTests/TestGeneration/BulkReviewLlmSuggestionsCommandHandlerTests.cs`

Modify if needed:

- `ClassifiedAds.Modules.TestGeneration/Controllers/LlmSuggestionsController.cs`
- `ClassifiedAds.Modules.TestGeneration/Commands/ReviewLlmSuggestionCommand.cs`
- `ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs`

## TESTS

Add at least these unit test groups:

- `BulkReviewLlmSuggestionsCommandHandlerTests`

If shared single-review logic changed, also update:

- `ReviewLlmSuggestionCommandHandlerTests`

Test specifically:

- suite owner validation
- suite not found
- invalid action rejected
- invalid suggestion-type filter rejected
- invalid test-type filter rejected
- bulk reject requires review notes
- only matching pending suggestions are processed
- zero-match path returns zero counts
- bulk approve materializes test cases and updates suite version once
- bulk reject does not materialize test cases
- bulk approve increments subscription usage once for the batch
- result model contains processed suggestion ids and applied test case ids

## VERIFICATION

At minimum run:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~BulkReviewLlmSuggestionsCommandHandlerTests'
```

And:

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

If shared FE-15 review logic changed, also run:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ReviewLlmSuggestionCommandHandlerTests'
```

If EF-related files changed unexpectedly, run the migration gate:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If Docker-related files actually changed, also run the compose/build checks required by `AGENTS.md`.

## DONE CRITERIA

- FE-17 bulk review endpoint exists on the existing controller surface
- only `Pending` suggestions are processed
- `Approve` and `Reject` both work with the required filter semantics
- bulk approve reuses FE-15 materialization logic
- bulk reject writes review metadata only
- no unnecessary EF migration, module, Docker, or compose changes were introduced
- targeted tests were added and executed
- WebAPI build was executed
- migration/Docker conclusions were explicitly checked and reported

## PHASED EXECUTION

Do not implement FE-17 in one giant step unless explicitly requested.

Preferred order:

1. `PROMPT-PHASE-1-SHARED-REVIEW-SERVICE.md`
2. `PROMPT-PHASE-2-BULK-API-SURFACE.md`
3. `PROMPT-PHASE-3-TESTS-AND-VERIFICATION.md`
