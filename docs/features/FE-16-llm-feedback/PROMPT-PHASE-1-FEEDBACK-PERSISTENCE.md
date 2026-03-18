# PHASE 1 PROMPT - Feedback Persistence For FE-16

Implement only the persistence layer for FE-16. Do not start feedback-context runtime or controller work yet.

## Task Classification

Expected classification for this phase:

- `Application code only`
- `Touches EF model / DbContext / migration / seed / connection settings`

This phase is not complete unless migration freshness is verified per `AGENTS.md`.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Migrator`
- related unit tests only if needed for the persistence layer

## Goal

Add a durable feedback table for per-user feedback on FE-15 `LlmSuggestion` rows.

## Preferred Solution For This Phase

- Add `LlmSuggestionFeedback` in `TestGeneration`; do not create a new module.
- Use a dedicated enum such as `LlmSuggestionFeedbackSignal` with values:
  - `Helpful`
  - `NotHelpful`
- Keep `TestSuiteId` and `EndpointId` on the feedback row as denormalized query columns copied from the owning suggestion.
- Follow the repo's current concurrency style:
  - `RowVersion` is a byte array concurrency token
  - it is application-managed, not database-generated
- Keep the schema minimal and additive. This phase should end with entity + configuration + DbContext + repository registration + migration.

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

Recommended modeling notes:

- keep the enum in the entity file unless the repo already has a better local pattern
- do not add extra durable state that belongs to future prompt aggregation only
- do not add reverse-navigation collections unless they are clearly needed

## Configuration Rules

Required EF behavior:

- table name should follow existing pluralized style, for example `LlmSuggestionFeedbacks`
- `Id` should default to `gen_random_uuid()`
- `FeedbackSignal` should be stored as string
- `Notes` should use `text`
- `RowVersion` should use the same style as `LlmSuggestion`:
  - `bytea`
  - `IsConcurrencyToken()`
  - `ValueGeneratedNever()`
- FK to `LlmSuggestion`
- cascade delete is acceptable because feedback lifecycle follows suggestion lifecycle

Required indexes:

- unique index on `(SuggestionId, UserId)`
- non-unique index on `(TestSuiteId, EndpointId)`
- non-unique index on `FeedbackSignal`

## Persistence Rules

- one row per `(SuggestionId, UserId)`
- feedback storage lives in `testgen`, not `llmassistant`
- notes should be stored as `text`
- the schema should support fast aggregation by suite and endpoint later
- this phase must not add controller endpoints or query-model changes

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

Then run the mandatory gate:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

If you use a different migration name, report it explicitly.

## Rules

- Do NOT add controller endpoints in this phase.
- Do NOT add feedback context aggregation in this phase.
- Do NOT change `LlmAssistant` persistence.
- Do NOT change `LlmScenarioSuggester` cache key yet.
- Do NOT modify Docker/compose unless a real new dependency appears.

## Minimal Tests

If you add tests in this phase, keep them narrow:

- configuration creates the unique key as expected
- enum-to-string mapping is stable
- the feedback entity is included in the EF model cleanly

## Done Criteria

- `LlmSuggestionFeedback` entity exists
- EF configuration exists and matches repo conventions
- `TestGenerationDbContext` includes the new `DbSet`
- repository registration is updated if required by this module pattern
- a migration was created in `ClassifiedAds.Migrator`
- migrator build ran
- `--verify-migrations` ran and passed

Stop after persistence + migration are in place.
