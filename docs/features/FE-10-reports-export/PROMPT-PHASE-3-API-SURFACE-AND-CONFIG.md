# PHASE 3 PROMPT - API Surface And Config For FE-10

Implement only the CQRS/API/config surface for FE-10. Reuse the phase-1 report gateway and phase-2 runtime services.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestReporting`
- `ClassifiedAds.WebAPI`
- light unit tests directly related to handlers/controllers if needed

## Goal

Expose FE-10 through report APIs and wire runtime/config into the existing WebAPI host.

## Files To Add

- `Controllers/TestReportsController.cs`
- `Commands/GenerateTestReportCommand.cs`
- `Queries/GetTestRunReportsQuery.cs`
- `Queries/GetTestRunReportQuery.cs`
- `Queries/DownloadTestRunReportQuery.cs`
- `Models/Requests/GenerateTestReportRequest.cs`

## Required Endpoints

1. `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`
2. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports`
3. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}`
4. `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`

All endpoints must:

- require `[Authorize("Permission:GetTestRuns")]`
- use `ICurrentUser`
- validate ownership by comparing `currentUserId` with `CreatedById` from suite or report context

## POST Handler Rules

The command handler must:

1. load report context via `ITestRunReportReadGatewayService`
2. validate owner
3. normalize and validate request `ReportType`, `Format`, and `RecentHistoryLimit`
4. call `ITestReportGenerator.GenerateAsync(...)`
5. return `201 Created` metadata response

## GET Query Rules

`GetTestRunReportsQueryHandler` must:

1. validate suite ownership via `ITestExecutionReadGatewayService.GetSuiteAccessContextAsync(...)`
2. return report metadata list for the run only

`GetTestRunReportQueryHandler` must:

1. validate suite ownership
2. return one metadata row for `reportId + runId`

`DownloadTestRunReportQueryHandler` must:

1. validate suite ownership
2. load the metadata row
3. call `IStorageFileGatewayService.DownloadAsync(fileId)`
4. return file payload model for the controller

## Config Rules

Add `Modules:TestReporting:ReportGeneration` config in `ClassifiedAds.WebAPI/appsettings.json`.

Suggested structure:

```json
"TestReporting": {
  "ReportGeneration": {
    "DefaultHistoryLimit": 5,
    "MaxHistoryLimit": 20,
    "MaxResponseBodyPreviewChars": 4000,
    "ReportRetentionHours": 168
  }
}
```

If the repo already stores local overrides in `appsettings.Development.json`, add the matching subtree there too.

## Rules

- Do NOT modify `ClassifiedAds.Background` in this phase.
- Do NOT add new permissions or new identity seed work; reuse `Permission:GetTestRuns`.
- Do NOT download files directly from Storage controller routes in FE-10 APIs.
- Do NOT generate report inside GET endpoints.

## Minimal Tests

Add only the tests needed to validate handler behavior if not already covered:

- POST handler validates owner and delegates to generator
- GET list handler scopes to the requested run
- GET single handler returns not found on missing report
- download handler calls storage gateway with the resolved file id

Stop after this phase. Leave broad verification to phase 4.
