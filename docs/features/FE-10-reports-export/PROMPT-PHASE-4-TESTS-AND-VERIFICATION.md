# PHASE 4 PROMPT - Tests And Verification For FE-10

Implement and run the missing tests for FE-10. Do not redesign the runtime architecture in this phase unless a test reveals a real defect.

## Scope

Projects allowed:

- `ClassifiedAds.UnitTests`
- minimal production-code fixes only when required by failing tests

## Goal

Finish FE-10 with targeted unit coverage and verification commands that prove the feature is wired correctly.

## Required Test Areas

1. `TestRunReportReadGatewayServiceTests`
   - non-finished run -> `REPORT_RUN_NOT_READY`
   - expired results -> `RUN_RESULTS_EXPIRED`
   - successful mapping returns definitions, results, and recent history

2. `CoverageCalculatorTests`
   - suite-scoped totals are correct
   - by-method and uncovered paths are calculated correctly

3. `ReportDataSanitizerTests`
   - Authorization/Cookie/token/password/apiKey values are masked
   - body preview truncation works

4. `JsonReportRendererTests`
   - JSON output contains summary and coverage sections

5. `CsvReportRendererTests`
   - CSV export rows are deterministic

6. `HtmlReportRendererTests`
   - HTML contains summary, history, and detailed sections
   - HTML does not contain raw secrets

7. `PdfReportRendererTests`
   - PDF renderer invokes the existing converter with HTML input

8. `TestReportGeneratorTests`
   - renderer selection
   - upload success path
   - coverage upsert path
   - upload failure stops metadata persistence

9. `GenerateTestReportCommandHandlerTests`
   - owner validation
   - run-ready validation
   - generator invocation

10. `GetTestRunReportsQueryHandlerTests`
    - owner validation
    - run scoping

11. `GetTestRunReportQueryHandlerTests`
    - report not found path

12. `DownloadTestRunReportQueryHandlerTests`
    - storage gateway invocation
    - report file missing path

## Verification Commands

Run targeted commands and report exactly what happened:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestReporting'
```

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --no-restore --filter 'FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution'
```

```powershell
dotnet build 'ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj' --no-restore
```

If the test namespaces differ after implementation, use the closest filter and state the exact command you actually ran.

## Review Checklist

- no migrations were added
- no Background worker or queue-based export was added
- no direct TestExecution repository/cache access from TestReporting
- report download stays behind TestReporting ownership checks
- secrets are masked before render/export
- coverage stays deterministic and suite-scoped

Stop after tests pass or after you identify the exact remaining blocker with evidence.
