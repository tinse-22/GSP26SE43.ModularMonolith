# PHASE 2 PROMPT - Bulk API Surface For FE-17

Implement only the FE-17 HTTP/CQRS surface. Reuse the shared review service from phase 1.

## Task Classification

Expected classification for this phase:

- `Application code only`

No new migration should be required in this phase.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- light unit tests directly related to the bulk command/controller if needed

## Goal

Expose one bulk-review endpoint on the existing FE-15 controller surface and wire it to the shared review service with stable filter semantics.

## Preferred Solution For This Phase

- Reuse `LlmSuggestionsController`.
- Add one bulk command file that follows the repo pattern of keeping command + handler together.
- Add one request model and one result model.
- Keep batch selection inside the bulk command handler.
- Return zero counts when no suggestions match.

## Files To Add

- `Commands/BulkReviewLlmSuggestionsCommand.cs`
- `Models/Requests/BulkReviewLlmSuggestionsRequest.cs`
- `Models/BulkReviewLlmSuggestionsResultModel.cs`

## Files To Modify

- `Controllers/LlmSuggestionsController.cs`

## Required Endpoint

Add or reconcile:

1. `POST /api/test-suites/{suiteId:guid}/llm-suggestions/bulk-review`

Rules:

- require `[Authorize(Permissions.UpdateTestCase)]`
- resolve `CurrentUserId` in the controller and dispatch `BulkReviewLlmSuggestionsCommand`
- validate suite ownership using the current FE-15 ownership pattern
- support only `Approve` and `Reject`
- require `ReviewNotes` when action is `Reject`
- filter by:
  - `ReviewStatus=Pending`
  - optional suggestion type
  - optional test type
  - optional endpoint id
- order matched suggestions by `DisplayOrder`
- call the shared review service once for the batch
- return `200 OK` with `BulkReviewLlmSuggestionsResultModel`

## Suggested Result Model

Recommended fields:

- `TestSuiteId : Guid`
- `Action : string`
- `MatchedCount : int`
- `ProcessedCount : int`
- `MaterializedCount : int`
- `ReviewedAt : DateTimeOffset`
- `SuggestionIds : List<Guid>`
- `AppliedTestCaseIds : List<Guid>`

## Notes

- Do NOT add a second controller just for bulk review.
- Do NOT redesign FE-15 routes.
- Do NOT move bulk review state into `LlmAssistant`.
- Do NOT add Docker/compose changes unless a real project reference/runtime dependency changed.
- Do NOT add migration work in this phase unless the code unexpectedly touched EF model files.

## Minimal Tests

Add only the tests needed to validate handler/controller behavior if not already covered:

- bulk approve processes matching pending suggestions
- bulk reject processes matching pending suggestions
- invalid action is rejected
- invalid filters are rejected
- reject requires review notes
- zero-match path returns zero counts

## Done Criteria

- bulk-review endpoint exists on the existing controller surface
- request/result models exist
- batch selection and filtering work as specified
- the command handler calls the shared review service once per batch
- minimal API-surface tests for this phase pass

Stop after bulk endpoint wiring is in place. Leave broad verification to phase 3.
