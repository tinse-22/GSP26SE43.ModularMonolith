# FE-16 LLM Feedback Package

Recommended FE-16 v1 for this codebase:

- build on the existing FE-15 `LlmSuggestion` workflow already implemented in `ClassifiedAds.Modules.TestGeneration`
- store feedback durably in `ClassifiedAds.Modules.TestGeneration`, not in `LlmSuggestionCache`
- allow per-user helpful/not-helpful feedback with optional notes
- surface current-user feedback and aggregate feedback summary through the existing FE-15 list/detail APIs
- feed sanitized feedback context back into FE-06 / FE-15 suggestion generation so later prompts can learn from recent review signals

Why this package exists:

- FE-15 now has durable `LlmSuggestion` rows, so FE-16 should extend that workflow instead of inventing a second suggestion store.
- FE-16 needs persistence, API write semantics, and prompt-refinement wiring, but it should still preserve module boundaries:
  - `TestGeneration` owns suite + suggestion lifecycle
  - `LlmAssistant` stays focused on cache/audit support
- If feedback changes prompt inputs, cache invalidation must also change; otherwise stale cached suggestions will ignore the latest user feedback.

Files:

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE16.md`
- `PROMPT-PHASE-1-FEEDBACK-PERSISTENCE.md`
- `PROMPT-PHASE-2-FEEDBACK-CONTEXT-AND-PROMPT-REFINEMENT.md`
- `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
- `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`

Decision note:

- FE-16 is split into 4 phases because it touches 4 different risk areas:
  - EF persistence + migration
  - prompt-refinement + cache-key correctness
  - API/model surface
  - tests + verification
- This split is intentionally smaller than FE-15 full implementation work, but more explicit than a single monolithic prompt, so an AI agent can finish each phase cleanly without drifting across the codebase.
