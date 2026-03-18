# PHASE 1 PROMPT - Feedback Persistence For FE-16

Implement only the persistence layer for FE-16. Do not start prompt-refinement or controller work yet.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Migrator`
- related unit tests only if needed for the persistence layer

## Goal

Add a durable feedback table for per-user feedback on FE-15 `LlmSuggestion` rows.

## Files To Add

- `Entities/LlmSuggestionFeedback.cs`
- `DbConfigurations/LlmSuggestionFeedbackConfiguration.cs`

## Files To Modify

- `Persistence/TestGenerationDbContext.cs`
- `ServiceCollectionExtensions.cs`
- new migration under `ClassifiedAds.Migrator/Migrations/TestGeneration`

## Required Entity Shape

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

Recommended enum:

- `Helpful`
- `NotHelpful`

## Persistence Rules

- one row per `(SuggestionId, UserId)`
- notes should be stored as text
- `RowVersion` must be a concurrency token
- foreign key to `LlmSuggestion`
- indexes at least for:
  - `(SuggestionId, UserId)` unique
  - `(TestSuiteId, EndpointId)`
  - `FeedbackSignal`

## Migration Work

Create a migration in `ClassifiedAds.Migrator` only.

Use the repo-standard command shape from `AGENTS.md`:

```powershell
dotnet ef migrations add AddLlmSuggestionFeedback `
  --context TestGenerationDbContext `
  --project ClassifiedAds.Migrator `
  --startup-project ClassifiedAds.Migrator `
  --output-dir Migrations/TestGeneration
```

Then:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If you use a different migration name, report it explicitly.

## Rules

- Do NOT add controller endpoints in this phase.
- Do NOT change `LlmAssistant` persistence.
- Do NOT change `LlmScenarioSuggester` cache key yet.
- Do NOT modify Docker/compose unless a real new dependency appears.

## Minimal Tests

If you add tests in this phase, keep them narrow:

- configuration creates unique key as expected
- enum/string mapping is stable
- feedback entity can be added to the context snapshot cleanly

Stop after persistence + migration are in place.
