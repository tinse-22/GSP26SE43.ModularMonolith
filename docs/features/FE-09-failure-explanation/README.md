# FE-09 Failure Explanation Package

Recommended FE-09 v1 for this codebase:

- synchronous on-demand explanation API
- cache-backed repeated requests
- reuse `LlmInteraction` and `LlmSuggestionCache`
- no new tables and no new EF migrations

Why this package exists:

- FE-07 and FE-08 now provide deterministic failed-run inputs.
- `ClassifiedAds.Modules.LlmAssistant` already has audit and cache primitives.
- The repo does not yet have `LlmAssistant` wired into `Background`, so async queue orchestration is better treated as a later extension.

Files:

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE09.md`
- `PROMPT-PHASE-1-FAILURE-READ-GATEWAY.md`
- `PROMPT-PHASE-2-LLM-RUNTIME-AND-CACHE.md`
- `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
- `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`

Related future-scope docs already in repo:

- `docs/features/FE-09-acceptance-criteria/FE-09-01/requirement.json`
- `docs/features/FE-09-acceptance-criteria/FE-09-01/contracts.json`
- `docs/features/FE-09-acceptance-criteria/FE-09-01/workflow.json`

Decision note:

- This package intentionally targets FE-09 v1 as synchronous request-response.
- The async RabbitMQ/worker workflow remains a future extension after the API-first version is stable.
