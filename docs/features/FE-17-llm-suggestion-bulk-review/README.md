# FE-17 LLM Suggestion Bulk Review Package

This package defines the preferred FE-17 v1 solution for this repo.

## Preferred Solution

- Build on top of the existing FE-15 durable `LlmSuggestion` workflow in `ClassifiedAds.Modules.TestGeneration`.
- Stay additive to FE-16 feedback. FE-17 must not redesign or bypass `LlmSuggestionFeedback`.
- Expose one bulk-review endpoint on the existing `LlmSuggestionsController` surface:
  - `POST /api/test-suites/{suiteId:guid}/llm-suggestions/bulk-review`
- Support only two batch actions in v1:
  - `Approve`
  - `Reject`
- Reuse one shared review service for both single-item FE-15 review and FE-17 bulk review so materialization, audit, subscription checks, and suite-version writes stay consistent.
- Filter the bulk target set using the same durable suggestion data:
  - `FilterBySuggestionType`
  - `FilterByTestType`
  - `FilterByEndpointId`
- Process only `Pending` suggestions and keep ordering deterministic by `DisplayOrder`.
- Return a compact batch result model with counts and processed ids instead of replaying one response per suggestion.
- Do not add a new module, host reference, runtime dependency, Docker change, compose change, or EF migration unless code changes truly require it.

## Why This Shape Fits This Repo

- FE-15 already owns durable `LlmSuggestion` rows, single-review semantics, and transactional materialization into `TestCase` records.
- FE-16 already extended the same suggestion lifecycle with feedback metadata and feedback-aware generation context.
- `ClassifiedAds.Modules.TestGeneration` is therefore still the correct home for FE-17.
- A shared review service is the cleanest fit because it avoids duplicating approval/rejection logic across FE-15 and FE-17.
- FE-17 is fundamentally an application-flow feature, not a storage redesign. The preferred solution keeps the schema stable and focuses on CQRS/controller/service wiring.

## Files In This Package

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE17.md`
- `PROMPT-PHASE-1-SHARED-REVIEW-SERVICE.md`
- `PROMPT-PHASE-2-BULK-API-SURFACE.md`
- `PROMPT-PHASE-3-TESTS-AND-VERIFICATION.md`
- `PROMPT-IMPLEMENT-FE17.json`

## Phase Split

FE-17 is intentionally split into 3 phases because it touches 3 different risk areas:

- shared review/materialization reuse between FE-15 and FE-17
- HTTP/CQRS bulk endpoint wiring and filter semantics
- tests and verification gates

This keeps each prompt focused, makes `AGENTS.md` gates easier to satisfy, and reduces the chance that an implementation drifts into unnecessary EF or Docker changes.
