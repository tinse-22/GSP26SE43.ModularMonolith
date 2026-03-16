# PHASE 2 PROMPT - Reporting Runtime And Renderers For FE-10

Implement only the reusable TestReporting runtime services for FE-10. Do not add controller/command/query API surface yet.

## Scope

Projects allowed:

- `ClassifiedAds.Modules.TestReporting`
- related unit tests in `ClassifiedAds.UnitTests/TestReporting`

## Goal

Build the FE-10 runtime pipeline inside `TestReporting`: sanitize deterministic run context, compute coverage, render report files, upload to storage, and persist metadata.

## Files To Add

- `Models/TestReportModel.cs`
- `Models/CoverageMetricModel.cs`
- `Models/TestRunReportDocumentModel.cs`
- `Models/RenderedReportFile.cs`
- `ConfigurationOptions/ReportGenerationOptions.cs`
- `Services/ITestReportGenerator.cs`
- `Services/TestReportGenerator.cs`
- `Services/ICoverageCalculator.cs`
- `Services/CoverageCalculator.cs`
- `Services/IReportDataSanitizer.cs`
- `Services/ReportDataSanitizer.cs`
- `Services/IReportRenderer.cs`
- `Services/PdfReportRenderer.cs`
- `Services/CsvReportRenderer.cs`
- `Services/JsonReportRenderer.cs`
- `Services/HtmlReportRenderer.cs`

## Files To Modify

- `ConfigurationOptions/TestReportingModuleOptions.cs`
  - add nested `ReportGeneration` options
- `ServiceCollectionExtensions.cs`
  - register new FE-10 services and renderer strategies

## Runtime Pipeline

Implement this sequence in `TestReportGenerator`:

1. sanitize incoming context
2. load endpoint metadata when available
3. calculate deterministic coverage
4. build structured report document model
5. select renderer by format
6. render file
7. upload file through `IStorageFileGatewayService`
8. upsert `CoverageMetric`
9. create `TestReport`
10. return `TestReportModel`

## Coverage Rules

- `TotalEndpoints` = distinct scoped endpoint ids from execution context
- `TestedEndpoints` = distinct endpoint ids in actual results where `Status != "Skipped"`
- `CoveragePercent` = percentage over scoped endpoints only
- `ByMethod` and `ByTag` must come from `IApiEndpointMetadataService` data for the scoped endpoints
- `UncoveredPaths` must list paths whose endpoint id was in scope but was not executed

## Sanitization Rules

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

- truncate body previews to the configured limit
- do not export raw secrets in headers or extracted variables

## Renderer Rules

- `JSON` renderer: serialize structured document model
- `CSV` renderer: flatten report rows deterministically
- `HTML` renderer: use existing `IRazorLightEngine` and avoid filesystem template dependency
- `PDF` renderer: reuse HTML output and existing `IConverter`

File category rules:

- `PDF`, `HTML` -> `FileCategory.Report`
- `CSV`, `JSON` -> `FileCategory.Export`

## Persistence Rules

- reuse `CoverageMetric`
- reuse `TestReport`
- upsert coverage by `TestRunId`
- create one new `TestReport` row per successful generation
- if upload fails, do not persist report metadata

## Tests

Add unit tests for:

- deterministic coverage calculation
- sanitizer masks secret-bearing headers and variables
- JSON renderer produces structured output
- CSV renderer produces stable flattened rows
- HTML renderer includes expected sections and excludes raw secrets
- PDF renderer delegates to converter
- generator selects correct renderer, uploads file, upserts coverage, and returns metadata

Stop after this phase. Do not add controller/command/query work yet.
