# TASK: Implement FE-10 - On-Demand Test Reporting And Export

## CONTEXT

You are implementing FE-10 in a .NET 10 Modular Monolith codebase (ASP.NET Core Web API, EF Core, PostgreSQL, CQRS via Dispatcher). Read `AGENTS.md` at repository root before touching code.

Primary module: `ClassifiedAds.Modules.TestReporting`
Supporting modules: `ClassifiedAds.Modules.TestExecution`, `ClassifiedAds.Modules.ApiDocumentation`, `ClassifiedAds.Modules.Storage`
Contract project: `ClassifiedAds.Contracts`

Read these spec files first:

- `docs/features/FE-10-reports-export/requirement.json`
- `docs/features/FE-10-reports-export/workflow.json`
- `docs/features/FE-10-reports-export/contracts.json`
- `docs/features/FE-10-reports-export/implementation-map.json`
- `docs/features/FE-10-reports-export/README.md`

## HARD CONSTRAINTS

- MUST keep FE-10 v1 synchronous and on-demand.
- MUST keep `ClassifiedAds.Modules.TestReporting` as the primary runtime module.
- MUST NOT add new entities, tables, or EF migrations.
- MUST reuse existing `TestReport` and `CoverageMetric`.
- MUST NOT let `ClassifiedAds.Modules.TestReporting` access `TestExecutionDbContext`, `TestRun` repository, or `IDistributedCache` directly.
- MUST add a cross-module read gateway in `ClassifiedAds.Contracts/TestExecution`, implemented inside `ClassifiedAds.Modules.TestExecution`.
- MUST upload/download generated files only via `IStorageFileGatewayService`.
- MUST sanitize sensitive request/response data before rendering and exporting files.
- MUST reuse existing permission string `Permission:GetTestRuns` for FE-10 v1.
- MUST default to ASCII in new files.

## REQUIRED API SURFACE

Implement these endpoints in `ClassifiedAds.Modules.TestReporting`:

1. `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`
   - Permission: `Permission:GetTestRuns`
   - Request body: `GenerateTestReportRequest`
   - Response: `201 Created` + `TestReportModel`

2. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + `List<TestReportModel>`

3. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + `TestReportModel`

4. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`
   - Permission: `Permission:GetTestRuns`
   - Response: `200 OK` + file result

## ARCHITECTURE TO IMPLEMENT

### 1. New cross-module report read gateway

Create new contract DTOs/interfaces under `ClassifiedAds.Contracts/TestExecution/`:

- `DTOs/TestRunReportContextDto.cs`
- `Services/ITestRunReportReadGatewayService.cs`

Implement the gateway in `ClassifiedAds.Modules.TestExecution/Services/TestRunReportReadGatewayService.cs`.

The gateway must:

- load suite access context via existing `ITestExecutionReadGatewayService.GetSuiteAccessContextAsync(testSuiteId)`
- load `TestRun` by `suiteId + runId`
- reject missing run with `NotFoundException`
- reject `Pending` or `Running` run with `ConflictException("REPORT_RUN_NOT_READY", ...)`
- read detailed run payload from TestExecution cache using existing FE-07/08 storage pattern
- bubble `RUN_RESULTS_EXPIRED` if cache payload is gone or expired
- load original ordered test definitions by calling existing `ITestExecutionReadGatewayService.GetExecutionContextAsync(testSuiteId, null)` once
- load bounded recent run history from the same suite
- return DTO only

Do NOT expose entities or cache implementation types.

### 2. TestReporting runtime services

Add these services:

- `ITestReportGenerator` / `TestReportGenerator`
- `ICoverageCalculator` / `CoverageCalculator`
- `IReportDataSanitizer` / `ReportDataSanitizer`
- `IReportRenderer` / `PdfReportRenderer`
- `IReportRenderer` / `CsvReportRenderer`
- `IReportRenderer` / `JsonReportRenderer`
- `IReportRenderer` / `HtmlReportRenderer`

Rationale:

- keep run-context access separate from report generation
- keep coverage calculation separate from rendering
- keep secret masking separate from renderers
- keep format-specific rendering behind a strategy interface

### 3. Reuse `TestReport` and `CoverageMetric`

- reuse `CoverageMetric` as a per-run coverage snapshot
- reuse `TestReport` as metadata inventory for generated files
- do not add a new export table

Persistence rules:

- upsert one `CoverageMetric` row per `TestRunId`
- create one new `TestReport` row per successful generation
- use `FileCategory.Report` for `PDF` and `HTML`
- use `FileCategory.Export` for `CSV` and `JSON`

### 4. Rendering and file generation

- `Summary` report must include run summary, coverage summary, failure distribution, and recent run history
- `Detailed` report must include summary plus per-case results and failure details
- `Coverage` report must focus on endpoint coverage, method/tag breakdown, and uncovered paths
- `JSON` renderer should serialize a structured document model
- `CSV` renderer should flatten relevant rows deterministically
- `HTML` renderer should reuse existing `IRazorLightEngine` and avoid filesystem-template dependency
- `PDF` renderer should render HTML first, then convert using existing `IConverter`

### 5. API + CQRS surface

Add:

- `Controllers/TestReportsController.cs`
- `Commands/GenerateTestReportCommand.cs`
- `Queries/GetTestRunReportsQuery.cs`
- `Queries/GetTestRunReportQuery.cs`
- `Queries/DownloadTestRunReportQuery.cs`
- `Models/Requests/GenerateTestReportRequest.cs`
- `Models/TestReportModel.cs`
- `Models/CoverageMetricModel.cs`

Add config under `Modules:TestReporting` for generation defaults.

## IMPLEMENTATION ORDER

1. Contracts/TestExecution report-read DTOs + interface
2. TestExecution report-read gateway + DI
3. TestReporting models + sanitizer + coverage + renderers + generator
4. TestReporting command/query/controller + config
5. Unit tests
6. Targeted verification

## DETAILED REQUIREMENTS

### A. Report read gateway behavior

Method signature:

```csharp
Task<TestRunReportContextDto> GetReportContextAsync(
    Guid testSuiteId,
    Guid runId,
    int recentHistoryLimit = 5,
    CancellationToken ct = default);
```

Behavior rules:

1. Load suite access context first.
2. Load run by `runId` and `testSuiteId`.
3. Validate run exists.
4. Validate `run.Status` is `Completed` or `Failed`.
5. Validate `RedisKey` exists and `ResultsExpireAt` is not expired.
6. Read and deserialize cached `TestRunResultModel` once only.
7. Call existing `GetExecutionContextAsync(testSuiteId, null)` once only.
8. Load recent run history for the same suite ordered by `RunNumber DESC`, excluding hot loops, bounded by `recentHistoryLimit`.
9. Return DTO with:
   - suite/project/spec/user ownership context
   - run info and resolved environment name
   - recent run history
   - ordered endpoint ids
   - original test case definitions
   - actual run results including failure reasons and validation flags

### B. Coverage rules

Coverage must be calculated from the suite-scoped endpoint set, not from arbitrary global state.

Rules:

- `TotalEndpoints` = distinct `OrderedEndpointIds`
- `TestedEndpoints` = distinct `EndpointId` values in actual results where `Status != "Skipped"`
- `CoveragePercent` = `0` when `TotalEndpoints == 0`, otherwise percentage rounded reasonably
- `ByMethod` and `ByTag` must be based only on metadata for the scoped endpoints
- `UncoveredPaths` must list scoped endpoints that were not executed successfully or unsuccessfully in the current run
- persist `ByMethod`, `ByTag`, and `UncoveredPaths` as JSON strings in `CoverageMetric`

### C. Sanitization rules

Mask values when key contains:

- `authorization`
- `cookie`
- `set-cookie`
- `token`
- `secret`
- `password`
- `apikey`
- `api-key`

Also:

- truncate response body previews to a configurable limit
- do not attempt to export full raw response bodies beyond FE-08 preview data
- sanitize both rendered content and any serialized structured report payload used by renderers

### D. Renderer and file rules

Renderer selection rules:

- `ReportFormat.PDF` -> `PdfReportRenderer`
- `ReportFormat.CSV` -> `CsvReportRenderer`
- `ReportFormat.JSON` -> `JsonReportRenderer`
- `ReportFormat.HTML` -> `HtmlReportRenderer`

Other rules:

- if no renderer supports the requested format, throw controlled validation error
- generated file name should include run number, report type, format, and UTC timestamp
- returned content type must match the chosen format
- upload file via `IStorageFileGatewayService.UploadAsync`

### E. Persistence and transaction rules

Generator pipeline:

1. sanitize context
2. load endpoint metadata when `ApiSpecId` exists and ordered endpoint ids are available
3. calculate coverage
4. build structured document model
5. render file
6. upload file
7. upsert `CoverageMetric`
8. create `TestReport`
9. return `TestReportModel`

Rules:

- if file upload fails, do not persist `TestReport`
- if DB save fails after upload, bubble the error; do not introduce destructive cleanup without an existing contract for it
- `GetTestRunReports` / `GetTestRunReport` / `DownloadTestRunReport` should validate owner using existing suite access context and then use `TestReport` metadata only

### F. API semantics

`POST`:

- owner must match current user via `CreatedById`
- if run results expired -> bubble `RUN_RESULTS_EXPIRED`
- if run not completed/failed -> bubble `REPORT_RUN_NOT_READY`
- return `201 Created`

`GET list`:

- owner must match current user via suite access context
- return metadata only

`GET single`:

- owner must match current user via suite access context
- return metadata only

`GET download`:

- owner must match current user via suite access context
- load metadata row then storage binary
- return file result with stored file name and content type

## TESTS

Add at least these unit test groups:

- `TestRunReportReadGatewayServiceTests`
- `CoverageCalculatorTests`
- `ReportDataSanitizerTests`
- `JsonReportRendererTests`
- `CsvReportRendererTests`
- `HtmlReportRendererTests`
- `PdfReportRendererTests`
- `TestReportGeneratorTests`
- `GenerateTestReportCommandHandlerTests`
- `GetTestRunReportsQueryHandlerTests`
- `GetTestRunReportQueryHandlerTests`
- `DownloadTestRunReportQueryHandlerTests`

## VERIFICATION

At minimum run:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestReporting'
```

And:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution'
```

And:

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

If the repo naming differs for the new tests, use the nearest targeted filter and report exactly what you ran.

## DONE CRITERIA

- report APIs exist and are wired
- report-read gateway exists in Contracts/TestExecution and TestExecution module
- TestReporting reuses `TestReport` and `CoverageMetric`
- generated files are uploaded only via Storage gateway
- no migrations were added
- secrets are masked before render/export
- targeted tests were added and executed
