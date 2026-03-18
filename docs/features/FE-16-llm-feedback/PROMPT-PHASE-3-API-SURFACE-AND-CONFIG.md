# PHASE 3 PROMPT - API Surface And Model Wiring For FE-16

Implement only the HTTP/CQRS surface for FE-16. Reuse the persistence and feedback-context runtime from phases 1 and 2.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.WebAPI`
- light unit tests directly related to handlers/controllers if needed

## Goal

Expose feedback write semantics through the existing FE-15 suggestion controller and enrich suggestion read models with feedback metadata.

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
- validate suite ownership using current FE-15 ownership pattern
- load suggestion by `suggestionId + testSuiteId`
- reject `Superseded`
- create or update feedback for the current user
- return one feedback model

## Read-Model Rules

Extend `LlmSuggestionModel` to include:

- `CurrentUserFeedback`
- `FeedbackSummary`

Then update:

- `GetLlmSuggestionsQueryHandler`
- `GetLlmSuggestionDetailQueryHandler`

So they batch-load feedback rows instead of querying in loops.

## Notes

- Do NOT add a second controller just for feedback.
- Do NOT add host/module registration changes unless truly required.
- Do NOT redesign FE-15 routes.
- Do NOT add Docker/compose changes unless a real project reference/runtime dependency changed.

## Minimal Tests

Add only the tests needed to validate handler/controller behavior if not already covered:

- feedback command creates a new row
- feedback command updates an existing row for the same user
- list query returns `CurrentUserFeedback` and counts
- detail query returns `FeedbackSummary`
- superseded suggestion is rejected

Stop after API/model wiring is in place. Leave broad verification to phase 4.
