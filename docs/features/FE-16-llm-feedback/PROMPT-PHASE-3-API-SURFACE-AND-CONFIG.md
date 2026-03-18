# PHASE 3 PROMPT - API Surface And Model Wiring For FE-16

Implement only the HTTP/CQRS surface for FE-16. Reuse the persistence and feedback-context runtime from phases 1 and 2.

## Task Classification

Expected classification for this phase:

- `Application code only`

No new migration should be required in this phase.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.WebAPI`
- light unit tests directly related to handlers/controllers if needed

## Goal

Expose feedback write semantics through the existing FE-15 suggestion controller and enrich suggestion read models with feedback metadata.

## Preferred Solution For This Phase

- Reuse `LlmSuggestionsController`.
- Add one upsert command file that follows the repo pattern of keeping command + handler together.
- Do not require a `RowVersion` in the feedback request for FE-16 v1; this is an idempotent current-user upsert surface.
- Batch-load feedback rows for list/detail queries and map in memory.

## Files To Add

- `Commands/UpsertLlmSuggestionFeedbackCommand.cs`
- `Models/LlmSuggestionFeedbackModel.cs`
- `Models/LlmSuggestionFeedbackSummaryModel.cs`
- `Models/Requests/UpsertLlmSuggestionFeedbackRequest.cs`

## Files To Modify

- `Controllers/LlmSuggestionsController.cs`
- `Models/LlmSuggestionModel.cs`
- `Queries/GetLlmSuggestionsQuery.cs`
- `Queries/GetLlmSuggestionDetailQuery.cs`

## Required Endpoint

Add:

1. `PUT /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}/feedback`

Rules:

- require `[Authorize(Permissions.UpdateTestCase)]`
- resolve `CurrentUserId` in the controller and dispatch `UpsertLlmSuggestionFeedbackCommand`
- validate suite ownership using the current FE-15 ownership pattern
- reject archived suites
- load suggestion by `suggestionId + testSuiteId`
- reject `Superseded`
- normalize `Notes` the same way phase 2 normalizes prompt-input notes
- create or update feedback for the current user
- refresh `UpdatedDateTime` and `RowVersion` on update
- return one feedback model

## Suggested Model Shape

`LlmSuggestionFeedbackModel` should be compact and API-friendly. Recommended fields:

- `Id : Guid`
- `SuggestionId : Guid`
- `TestSuiteId : Guid`
- `EndpointId : Guid?`
- `UserId : Guid`
- `Signal : string`
- `Notes : string?`
- `CreatedDateTime : DateTimeOffset`
- `UpdatedDateTime : DateTimeOffset?`
- `RowVersion : string`

`LlmSuggestionFeedbackSummaryModel` should at least include:

- `HelpfulCount : int`
- `NotHelpfulCount : int`
- `LastFeedbackAt : DateTimeOffset?`

## Read-Model Rules

Extend `LlmSuggestionModel` to include:

- `CurrentUserFeedback`
- `FeedbackSummary`

Then update:

- `GetLlmSuggestionsQueryHandler`
- `GetLlmSuggestionDetailQueryHandler`

Required query behavior:

- validate suite existence and suite ownership first
- load suggestions for the suite
- batch-load feedback rows for the relevant suggestion ids
- compute current-user feedback and aggregate summary without per-suggestion queries
- keep existing FE-15 filters intact

## Notes

- Do NOT add a second controller just for feedback.
- Do NOT redesign FE-15 routes.
- Do NOT add host/module registration changes unless truly required.
- Do NOT add Docker/compose changes unless a real project reference/runtime dependency changed.
- Do NOT move feedback read/write logic into `LlmAssistant`.

## Minimal Tests

Add only the tests needed to validate handler/controller behavior if not already covered:

- feedback command creates a new row
- feedback command updates an existing row for the same user
- archived suite is rejected
- list query returns `CurrentUserFeedback` and counts
- detail query returns `FeedbackSummary`
- superseded suggestion is rejected

## Done Criteria

- PUT feedback endpoint exists on the existing controller surface
- current-user feedback can be created and updated idempotently
- `LlmSuggestionModel` includes `CurrentUserFeedback` and `FeedbackSummary`
- list/detail query handlers batch-load feedback rows instead of querying in loops
- minimal API-surface tests for this phase pass

Stop after API/model wiring is in place. Leave broad verification to phase 4.
