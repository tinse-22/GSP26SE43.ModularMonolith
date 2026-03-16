# FE-10 Reports And Export Package

Recommended FE-10 v1 for this codebase:

- synchronous on-demand report generation API
- reuse existing `TestReport` and `CoverageMetric`
- read run data via contract gateway from `TestExecution`
- compute coverage from suite-scoped endpoints plus executed case results
- render `PDF`, `CSV`, `JSON`, and `HTML` using existing infrastructure utilities
- upload generated files via `IStorageFileGatewayService`
- no new tables and no new EF migrations

Why this package exists:

- FE-07 and FE-08 now provide deterministic run summary plus cache-backed detailed results.
- `ClassifiedAds.Modules.TestReporting` already exists, is wired into `WebAPI` and `Migrator`, and already owns the persistence schema for report metadata.
- `ClassifiedAds.Contracts.Storage` already exposes upload/download operations that can be reused instead of inventing a new export pipeline.
- `ClassifiedAds.Infrastructure` already ships `CsvHelper`, `RazorLight`, and `DinkToPdf`, and the hosts already register the HTML/PDF helpers.

Files:

- `requirement.json`
- `workflow.json`
- `contracts.json`
- `implementation-map.json`
- `PROMPT-IMPLEMENT-FE10.md`
- `PROMPT-PHASE-1-REPORT-READ-GATEWAY.md`
- `PROMPT-PHASE-2-REPORTING-RUNTIME-AND-RENDERERS.md`
- `PROMPT-PHASE-3-API-SURFACE-AND-CONFIG.md`
- `PROMPT-PHASE-4-TESTS-AND-VERIFICATION.md`

Decision note:

- This package intentionally targets FE-10 v1 as synchronous request-response.
- It reuses the existing `testreporting` schema and does not introduce a background worker, email delivery flow, or async export queue.
- File generation is owned by `TestReporting`; binary storage is owned by `Storage`; run data ownership stays in `TestExecution`.
