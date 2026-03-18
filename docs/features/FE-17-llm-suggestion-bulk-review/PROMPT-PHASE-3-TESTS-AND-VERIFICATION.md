# PHASE 3 PROMPT - Tests And Verification For FE-17

Implement and run the missing tests for FE-17. Do not redesign the architecture in this phase unless a test reveals a real defect.

## Task Classification

Expected classification for this phase:

- `Application code only`

Preferred FE-17 implementation should not require migration work, but you must still state clearly whether migration verification was needed and why.

## Scope

Projects allowed:

- `ClassifiedAds.UnitTests`
- minimal production-code fixes only when required by failing tests

## Goal

Finish FE-17 with targeted test coverage and verification commands that prove bulk approve/reject is wired correctly and does not introduce accidental FE-15 or FE-16 regressions.

## Required Test Areas

1. `BulkReviewLlmSuggestionsCommandHandlerTests`
   - suite owner validation
   - suite not found
   - invalid action
   - invalid suggestion-type filter
   - invalid test-type filter
   - reject requires review notes
   - only matching pending suggestions are processed
   - zero-match path returns zero counts
   - bulk approve materializes test cases
   - bulk reject does not materialize test cases
   - subscription usage increments once per batch approve

2. `ReviewLlmSuggestionCommandHandlerTests`
   - run only if shared FE-15 review logic changed
   - confirm single-review semantics still work after reuse changes

## Verification Commands

Run targeted commands and report exactly what happened:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~BulkReviewLlmSuggestionsCommandHandlerTests'
```

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

If shared single-review logic changed:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ReviewLlmSuggestionCommandHandlerTests'
```

If EF-related files changed unexpectedly:

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
- the exact migration verification command used, if any
- whether Docker registration was checked
- which Docker/compose commands were run, if any
- whether Docker daemon was available when Docker commands were required
- any verification step that was skipped and why

If no EF-related files changed, say that no migration gate was required by scope and that you verified the conclusion by inspection.

If no Docker-related files changed, say that Docker registration was checked by inspection and no compose/build command was required.

## Review Checklist

- FE-17 reuses durable `LlmSuggestion` rows
- no new migration was introduced unless a real blocker forced it
- FE-15 single review still works
- FE-16 feedback surface still works
- bulk approve uses shared materialization logic
- bulk reject does not create test cases
- WebAPI build passes

## Done Criteria

- targeted FE-17 tests exist
- targeted FE-17 tests were executed
- WebAPI build ran
- migration conclusion was explicitly addressed
- Docker/compose conclusion was explicitly addressed
- any remaining blocker is reported with exact evidence

Stop after tests pass or after you identify the exact remaining blocker with evidence.
