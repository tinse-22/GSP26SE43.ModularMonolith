# PHASE 4 PROMPT - Tests And Verification For FE-16

Implement and run the missing tests for FE-16. Do not redesign the architecture in this phase unless a test reveals a real defect.

## Task Classification

Expected classification for this phase:

- `Application code only`

This phase must still satisfy the relevant completion gates from `AGENTS.md`, especially migration verification for the persistence work introduced earlier.

## Scope

Projects allowed:

- `ClassifiedAds.UnitTests`
- minimal production-code fixes only when required by failing tests

## Goal

Finish FE-16 with targeted test coverage and verification commands that prove the feature is wired correctly and does not introduce stale-cache behavior.

## Required Test Areas

1. `UpsertLlmSuggestionFeedbackCommandHandlerTests`
   - suite owner validation
   - archived suite rejection
   - suggestion not found
   - superseded suggestion rejection
   - new feedback row created
   - existing row updated instead of duplicated

2. `LlmSuggestionFeedbackContextServiceTests`
   - endpoint-scoped aggregation
   - helpful/not-helpful counts
   - notes truncation/sanitization
   - stable fingerprint for same normalized inputs
   - changed fingerprint when aggregated feedback changes

3. `GetLlmSuggestionsQueryHandlerTests`
   - current-user feedback is attached
   - summary counts are attached
   - suite ownership still enforced

4. `GetLlmSuggestionDetailQueryHandlerTests`
   - feedback summary is returned
   - not-found path still works

5. `LlmScenarioSuggesterTests`
   - feedback fingerprint participates in cache key
   - empty feedback still allows cache/hit logic
   - `N8nBoundaryEndpointPayload` receives endpoint-scoped `FeedbackContext`

## Verification Commands

Run targeted commands and report exactly what happened:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestGeneration'
```

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
```

```powershell
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If Docker-related files actually changed, also run the required compose/build checks from `AGENTS.md`.

## Reporting Requirements

In the final response for a real implementation run, explicitly state:

- whether migration verification was run
- the exact migration verification command used
- whether Docker registration was checked
- which Docker/compose commands were run, if any
- whether Docker daemon was available when Docker commands were required
- any verification step that was skipped and why

If no Docker-related files changed, say that Docker registration was checked by inspection and no compose/build command was required.

## Review Checklist

- feedback state lives in `TestGeneration`, not `LlmAssistant`
- one feedback row per suggestion/user
- FE-15 list/detail still work
- feedback-aware cache fingerprinting is covered by tests
- payload enrichment path is covered by tests
- migration exists and `--verify-migrations` passes
- no new Docker/runtime wiring was introduced unless explicitly needed and verified

## Done Criteria

- targeted FE-16 tests exist
- targeted FE-16 tests were executed
- WebAPI build ran
- migrator build ran
- `--verify-migrations` ran and passed
- any remaining blocker is reported with exact evidence

Stop after tests pass or after you identify the exact remaining blocker with evidence.
