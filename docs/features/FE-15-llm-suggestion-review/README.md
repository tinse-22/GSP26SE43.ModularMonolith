# FE-15 LLM Suggestion Review Package

Recommended FE-15 v1 for this codebase:

- durable suggestion review records inside `ClassifiedAds.Modules.TestGeneration`
- reuse existing FE-06 LLM scenario pipeline instead of inventing a second generator
- keep `ClassifiedAds.Modules.LlmAssistant` as audit + raw suggestion cache support only
- preview, approve, reject, and modify individual suggestions before materializing real `TestCase` rows
- additive workflow that does not break the existing FE-06 direct-generation endpoint

Why this package exists:

- FE-06 already generates useful `LlmSuggestedScenario` data, but it currently materializes those suggestions directly into `TestCase` rows.
- FE-15 requires a human review gate before an LLM suggestion becomes part of the suite.
- `ClassifiedAds.Modules.TestGeneration` already owns suite/test-case lifecycle, review patterns, change logs, and suite versioning, so review state belongs there.
- `ClassifiedAds.Modules.LlmAssistant` already provides interaction audit logging and raw suggestion caching, so FE-15 should reuse that support instead of duplicating it.

Files:

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE15.md`

Decision note:

- FE-15 v1 is intentionally additive to FE-06.
- Existing `generate-boundary-negative` behavior should remain available for current clients.
- The new FE-15 review flow is the recommended path when teams want human approval before LLM-created cases are added to the suite.
