# Feature (FE) Completion Tracker

> Last Updated: 2026-03-15
> Audit Basis: code audit across modules + targeted test verification
> Latest Verification: `dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution'` -> Passed 116/116
> Note: FE-05 is tracked as one feature here and includes FE-05A (order workflow) + FE-05B (happy-path generation).

---

## Snapshot

| Status | Count | % |
|---|---:|---:|
| Completed | 12 | 71% |
| In Progress | 0 | 0% |
| Skeleton | 2 | 12% |
| Not Started | 3 | 18% |
| Total | 17 | 100% |

Overall implementation-weighted progress: ~86%

Why weighted progress is higher than completion count:
- Most core and high-weight features are already done, including generation, execution, and rule-based validation.
- The biggest remaining gaps are post-execution experience features: failure explanation, reporting/export, and LLM review workflow.

---

## Current End-to-End Product Flow

| Step | Requirement | Status | Notes |
|---|---|---|---|
| 1 | Upload and manage API sources | Completed | FE-02, FE-11, FE-13 |
| 2 | Parse and normalize specs | Completed | FE-03 |
| 3 | Configure suite scope and execution environment | Completed | FE-04 |
| 4 | Propose and confirm API order | Completed | FE-05A |
| 5 | Generate happy-path test cases | Completed | FE-05B |
| 6 | Generate boundary and negative cases | Completed | FE-06 |
| 7 | Execute dependency-aware test runs | Completed | FE-07 |
| 8 | Apply deterministic pass/fail validation | Completed | FE-08 |
| 9 | Explain failures with LLM | Remaining | FE-09 |
| 10 | Generate reports and export files | Remaining | FE-10 |
| 11 | Review, feedback, and bulk actions for LLM suggestions | Remaining | FE-15, FE-16, FE-17 |

---

## Recommended Next Requirements

| Priority | FE | Recommendation | Why now |
|---|---|---|---|
| 1 | FE-09 | Build LLM-assisted failure explanation | FE-07 and FE-08 are now in place, so failure explanation has stable deterministic input. `LlmAssistant` already has interaction logging and suggestion caching primitives. |
| 2 | FE-10 | Build reporting and export | This naturally follows completed execution/validation flows and can reuse persisted run summaries plus cached run details. |
| 3 | FE-15 -> FE-16 -> FE-17 | Build the LLM review loop last | These features improve the feedback loop, but they do not block the core MVP workflow from spec -> tests -> execution -> validation. |

Recommended default path for the next sprint:
1. FE-09
2. FE-10
3. FE-15
4. FE-16
5. FE-17

Lower-risk alternative:
- If the next sprint should avoid LLM runtime work, do FE-10 before FE-09.

---

## Feature Status

| FE | Feature | Module | Status | Evidence |
|---|---|---|---|---|
| FE-01 | Authentication and role-based access control | Identity | Completed | Auth, users, roles, JWT + refresh rotation, rate limiting, RBAC, external IdP support, seeded permissions |
| FE-02 | Upload/store/manage API sources | ApiDocumentation | Completed | Project/specification/endpoint management, upload flow, lifecycle, audit logging |
| FE-03 | Parse and normalize API inputs | ApiDocumentation + Storage | Completed | OpenAPI + Postman parsers, async parse flow, idempotent command pipeline, 39 unit tests |
| FE-04 | Test scope and execution configuration | TestGeneration + TestExecution | Completed | Test suite scope management plus execution environment CRUD and auth config masking |
| FE-05 | Happy-path generation workflow | TestGeneration | Completed | FE-05A order proposal/approve/reorder + FE-05B happy-path generation with prompt pipeline and persistence |
| FE-06 | Boundary and negative generation | TestGeneration + LlmAssistant + ApiDocumentation | Completed | Path mutations, body mutation engine, LLM scenario suggestions, orchestration, 41 unit tests |
| FE-07 | Dependency-aware execution with variable extraction | TestExecution | Completed | `StartTestRunCommand`, `TestRunsController` (4 endpoints), `TestExecutionOrchestrator`, execution context gateway, dependency skip logic, environment runtime resolver, variable resolve/extract, HTTP executor |
| FE-08 | Deterministic rule-based validation | TestExecution | Completed | `RuleBasedValidator` for status/schema/header/body/jsonpath/latency checks, endpoint schema fallback, `TestResultCollector`, cached run details, retention handling, sensitive variable masking |
| FE-09 | LLM-assisted failure explanations | LlmAssistant | Skeleton | `LlmAssistantGatewayService` can log interactions and cache suggestions, but there is no failure-explanation workflow, prompt runtime, command/query/controller, or explanation API yet |
| FE-10 | Test execution reports and export | TestReporting | Skeleton | `TestReport` and `CoverageMetric` entities plus persistence setup only; no report builder, controller, export service, or file generation yet |
| FE-11 | Manual Entry mode | ApiDocumentation | Completed | Manual specification and inline endpoint definition flow is implemented |
| FE-12 | Path-parameter templating | ApiDocumentation | Completed | Path parameter extraction, validation, URL resolution, and mutation generation are implemented |
| FE-13 | cURL import | ApiDocumentation | Completed | cURL parsing into method, URL, headers, body, and params is implemented |
| FE-14 | Subscription and billing management | Subscription | Completed | Plan lifecycle, usage limits, payment flow, webhook handling, workers, and billing persistence are implemented |
| FE-15 | LLM suggestion review interface | TestGeneration + LlmAssistant | Not Started | No review UI/API workflow yet |
| FE-16 | User feedback on LLM suggestions | TestGeneration + LlmAssistant | Not Started | No feedback capture or storage flow yet |
| FE-17 | Bulk approval/rejection with filtering | TestGeneration + LlmAssistant | Not Started | No bulk review actions yet |

---

## Module Status

| Module | FE Coverage | Status | Notes |
|---|---|---|---|
| Identity | FE-01 | Full | Production-ready auth and authorization stack |
| ApiDocumentation | FE-02, FE-03, FE-11, FE-12, FE-13 | Full | Input management, parsing, manual entry, path templating, cURL import |
| TestGeneration | FE-04, FE-05, FE-06 | Full | Scope config, ordering, happy-path generation, boundary/negative generation |
| TestExecution | FE-04, FE-07, FE-08 | Full | 2 controllers, 3 commands, 5 queries, runtime resolution, execution pipeline, validator, result collector, targeted tests passing |
| Subscription | FE-14 | Full | Plans, limits, payment integration, workers |
| LlmAssistant | FE-06 support, FE-09, FE-15, FE-16, FE-17 | Partial | Interaction audit and suggestion cache exist; feature workflows are still missing |
| TestReporting | FE-10 | Skeleton | Persistence model only |

---

## Verification Notes

- Targeted verification for `TestExecution` passed on 2026-03-15: 116/116 tests.
- During verification, `ClassifiedAds.UnitTests/TestExecution/StartTestRunCommandHandlerTests.cs` was aligned with the current `ITestExecutionOrchestrator.ExecuteAsync(...)` signature so the test project reflects the actual service contract.
- The solution still emits existing nullable/analyzer warnings outside the scope of this tracker refresh, but the targeted `TestExecution` test slice is green.

---

## Change Log

| Date | Scope | Change |
|---|---|---|
| 2026-03-15 | FE-07, FE-08 | Promoted both features to `Completed` after full code audit. Confirmed `StartTestRunCommand`, test run queries, `TestRunsController`, `TestExecutionOrchestrator`, `HttpTestExecutor`, `VariableResolver`, `VariableExtractor`, `RuleBasedValidator`, and `TestResultCollector`. Targeted `TestExecution` tests now pass 116/116. Overall weighted progress moved from ~70% to ~86%. |
| 2026-02-28 | FE-06 | Completed boundary and negative test generation with path mutations, body mutation engine, LLM scenario suggestions, orchestration, and 41 unit tests. |
| 2026-02-28 | FE-03 | Confirmed parser flow completion for OpenAPI/Postman parsing and async spec processing. |
| 2026-02-25 | FE-05B | Completed happy-path test case generation workflow. |
| 2026-02-24 | FE-05A | Completed API ordering workflow with propose/reorder/approve flow. |
| 2026-02-19 | FE-04 | Completed test scope and execution environment configuration. |
| 2026-02-18 | FE-14 | Completed subscription and billing flow. |
| 2026-02-13 | FE-02, FE-11, FE-13 | Completed API input management, manual entry, and cURL import. |
| 2026-02-07 | FE-01 | Completed identity module implementation. |
