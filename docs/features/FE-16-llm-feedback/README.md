# FE-16 LLM Feedback Package

This package defines the preferred FE-16 v1 solution for this repo.

## Preferred Solution

- Build on the existing FE-15 `LlmSuggestion` workflow already implemented in `ClassifiedAds.Modules.TestGeneration`.
- Keep durable feedback state in `ClassifiedAds.Modules.TestGeneration`, not in `LlmSuggestionCache` and not in `ClassifiedAds.Modules.LlmAssistant`.
- Add one durable `LlmSuggestionFeedback` row per `(SuggestionId, UserId)`.
- Copy `TestSuiteId` and `EndpointId` onto the feedback row so feedback aggregation stays cheap and does not require large joins.
- Store the feedback signal as a string-backed enum such as `LlmSuggestionFeedbackSignal`.
- Keep `Notes` optional, normalized, and stored as `text`.
- Follow the repo's existing optimistic-concurrency pattern: `RowVersion` is application-managed and refreshed with `Guid.NewGuid().ToByteArray()`.
- Reuse the existing FE-15 controller and query surface:
  - `PUT /api/test-suites/{suiteId:guid}/llm-suggestions/{suggestionId}/feedback`
  - extend FE-15 list/detail responses with `CurrentUserFeedback` and `FeedbackSummary`
- Feed feedback back into future generation by:
  - building endpoint-scoped `FeedbackContext`
  - computing a stable `FeedbackFingerprint`
  - adding that fingerprint to `LlmScenarioSuggester` cache-key material
  - enriching `N8nBoundaryEndpointPayload` with `FeedbackContext`
- Keep `ObservationConfirmationPromptBuilder` focused on spec and business-rule prompting unless the payload-enrichment path proves impossible.
- Do not add a new module, host reference, runtime dependency, Docker change, or compose change unless code changes truly require it.

## Why This Shape Fits This Repo

- FE-15 already persists durable `LlmSuggestion` rows in `testgen`, so FE-16 should extend that lifecycle instead of inventing a second store.
- `TestGeneration` already owns suite ownership, suggestion lifecycle, and FE-15 list/detail/controller patterns, so it is the right place for FE-16 persistence and API semantics.
- `LlmAssistant` should stay limited to cache and audit support. Moving durable feedback there would blur module ownership.
- Feedback only helps future suggestion generation if cache invalidation changes with it. A feedback-aware fingerprint is therefore part of the solution, not an optional extra.
- The payload-enrichment path is the cleanest fit with the current code because it keeps prompt-builder responsibilities narrow and avoids mixing human review feedback into the spec-only observation/confirmation builder.

## Files In This Package

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE16.md`
- `PROMPT-PHASE-1-FEEDBACK-PERSISTENCE.md`
- `PROMPT-PHASE-2-FEEDBACK-CONTEXT-AND-PROMPT-REFINEMENT.md`
- `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
- `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`

## Phase Split

FE-16 is intentionally split into 4 phases because it touches 4 different risk areas:

- EF persistence and migration freshness
- feedback aggregation, prompt enrichment, and cache-key correctness
- HTTP/CQRS/read-model wiring
- tests and verification gates

This keeps each prompt focused, makes AGENTS.md gates easier to satisfy, and reduces the chance that an implementation drifts across unrelated parts of the codebase.
