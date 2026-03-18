# PHASE 1 PROMPT - Shared Review Service Reuse For FE-17

Implement only the shared review-service layer for FE-17. Do not add the bulk controller endpoint yet.

## Task Classification

Expected classification for this phase:

- `Application code only`

No new migration should be needed in this phase. If you end up changing EF model shape, you must go back through the migration gate from `AGENTS.md`.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestGeneration`
- related unit tests only if needed for the shared review layer

## Goal

Create or harden one shared review service that both FE-15 single review and FE-17 bulk review can use.

## Preferred Solution For This Phase

- Add or reconcile `ILlmSuggestionReviewService`.
- Add or reconcile `LlmSuggestionReviewService`.
- Keep FE-15 single review routed through the same service.
- Support:
  - `RejectAsync`
  - `RejectManyAsync`
  - `ApproveManyAsync`
- Keep transaction, audit, suite-version, and subscription logic centralized in this service.

## Files To Add

- `Services/ILlmSuggestionReviewService.cs`
- `Services/LlmSuggestionReviewService.cs`

## Files To Modify

- `Commands/ReviewLlmSuggestionCommand.cs`
- `ServiceCollectionExtensions.cs`

## Required Service Rules

- `RejectManyAsync` should update review metadata only.
- `ApproveManyAsync` should:
  - check `MaxTestCasesPerSuite` for the batch size
  - require approved API order via the existing gate service
  - materialize `TestCase` graph using the existing materializer
  - create `TestCaseChangeLog` rows
  - create one `TestSuiteVersion` row for the batch
  - update suggestion review fields and `AppliedTestCaseId`
  - increment subscription usage once after success

## Notes

- Do NOT add the FE-17 controller endpoint in this phase.
- Do NOT add request/result models in this phase.
- Do NOT add a new batch table.
- Do NOT change FE-16 feedback persistence or query wiring.
- Do NOT touch Docker/compose unless a real new dependency appears.

## Minimal Tests

If you add tests in this phase, keep them narrow:

- `ApproveManyAsync` materializes multiple suggestions transactionally
- `RejectManyAsync` updates review metadata without materialization
- batch approve increments subscription usage once
- FE-15 single-review path can still reuse the same service cleanly

## Done Criteria

- shared review service exists
- FE-15 single review can reuse it
- batch approve/reject primitives exist
- no migration was added unless a real blocker forced EF changes

Stop after the shared service layer is in place.
