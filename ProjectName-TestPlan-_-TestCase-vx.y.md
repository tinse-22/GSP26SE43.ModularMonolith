# API Testing Report Standard For `ProjectName-TestPlan-_-TestCase-vx.y`

## Muc dich

File nay la chuan noi dung cho API testing report cua testcase khi nguon testcase duoc tao/bo sung boi LLM va duoc thuc thi qua codebase hien tai. Chuan nay dung de doi chieu voi file mau `ProjectName-TestPlan-_-TestCase-vx.y.xls`, nhung implementation runtime trong codebase nen xuat `Excel` thanh `.xlsx`, khong phai `.xls`, vi `ExcelReportRenderer` dang dung ClosedXML va content type OpenXML:

```text
application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

Ten file runtime hien tai duoc build theo pattern:

```text
@{ProjectName}-TestPlan-_-TestCase-v1.0-{reportType}-{yyyyMMddTHHmmssZ}.xlsx
```

## Danh gia API report hien tai

Module dung de implement la `ClassifiedAds.Modules.TestReporting`. API report hien tai da dung huong cho FE-10 vi no khong doc truc tiep database cua `TestExecution`, ma lay du lieu chay test qua contract gateway:

- `ITestRunReportReadGatewayService.GetReportContextAsync(...)`
- `TestRunReportContextDto`
- `ReportTestCaseDefinitionDto`
- `ReportTestCaseResultDto`
- `TestRunExecutionAttemptDto`

Luồng dung trong codebase:

1. FE hoac client tao test run tu test suite.
2. `TestExecution` chay tung testcase, gom ket qua deterministic.
3. `TestReporting` lay context cua run qua gateway.
4. `ReportDataSanitizer` mask token/secret/password/cookie va cat ngan response preview.
5. `CoverageCalculator` tinh coverage theo endpoint metadata.
6. Renderer xuat file `PDF`, `CSV`, `JSON`, `HTML`, hoac `Excel`.
7. File duoc upload qua `IStorageFileGatewayService`.
8. Metadata report duoc luu vao bang `testreporting."TestReports"` va coverage vao `testreporting."CoverageMetrics"`.

Danh gia: cach nay dung voi modular monolith vi ownership ro rang. `TestExecution` so huu ket qua run, `TestReporting` so huu metadata/export, `Storage` so huu binary file. Khong nen de report module query truc tiep DbContext cua module khac.

## API contract chuan

Base route:

```http
/api/test-suites/{suiteId}/test-runs/{runId}/reports
```

Tat ca endpoint yeu cau auth va policy:

```text
Permission:GetTestRuns
```

### Generate report

```http
POST /api/test-suites/{suiteId}/test-runs/{runId}/reports
Content-Type: application/json
```

Request body:

```json
{
  "reportType": "Detailed",
  "format": "Excel",
  "recentHistoryLimit": 5
}
```

Gia tri hop le:

```text
reportType: Summary | Detailed | Coverage
format: PDF | CSV | JSON | HTML | Excel
recentHistoryLimit: integer, >= 1, <= ReportGeneration.MaxHistoryLimit
```

Response thanh cong:

```http
201 Created
Location: /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}
```

Response body:

```json
{
  "id": "report-guid",
  "testSuiteId": "suite-guid",
  "testRunId": "run-guid",
  "reportType": "Detailed",
  "format": "Excel",
  "downloadUrl": "/api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download",
  "generatedAt": "2026-04-26T00:00:00Z",
  "expiresAt": null,
  "coverage": {
    "testRunId": "run-guid",
    "totalEndpoints": 25,
    "testedEndpoints": 20,
    "coveragePercent": 80.0,
    "byMethod": {
      "GET": 90.0,
      "POST": 75.0
    },
    "byTag": {
      "Auth": 100.0,
      "Projects": 66.67
    },
    "uncoveredPaths": [
      "DELETE /api/projects/{id}"
    ],
    "calculatedAt": "2026-04-26T00:00:00Z"
  }
}
```

### List reports cua run

```http
GET /api/test-suites/{suiteId}/test-runs/{runId}/reports
```

Tra ve danh sach `TestReportModel`, sap xep moi nhat truoc.

### Get one report metadata

```http
GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}
```

Tra ve metadata cua report va coverage gan voi `runId`.

### Download report file

```http
GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download
```

Tra ve binary file tu Storage. Voi Excel runtime chuan:

```text
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
File extension: .xlsx
```

## Chuan LLM output truoc khi co report

LLM chi nen la nguon de de xuat testcase. Report khong duoc tin truc tiep vao van ban LLM. Report phai dua tren testcase da duoc persist va ket qua execution that.

LLM response duoc xem la "chuan chi" khi thoa cac dieu kien:

1. Co output parse duoc thanh JSON/schema noi bo, khong tra ve markdown tu do.
2. Moi testcase co `name`, `description`, `testType`, `orderIndex`.
3. Moi testcase co request ro rang: `httpMethod`, `url`, `headers`, `pathParams`, `queryParams`, `bodyType`, `body`, `timeout`.
4. Moi testcase co expectation ro rang: `expectedStatus`, `responseSchema`, `headerChecks`, `bodyContains`, `bodyNotContains`, `jsonPathChecks`, `maxResponseTime`.
5. Dependency giua testcase phai dung ID/reference hop le, khong phu thuoc vao thu tu text mo ho.
6. Du lieu nhay cam khong duoc generate vao plain text: token, password, cookie, api key.
7. Variable extraction va variable usage phai co ten on dinh, vi report can hien thi ket qua dependency/retry/skip.
8. Testcase phai gan duoc voi endpoint metadata neu co `ApiSpecId`, de coverage tinh dung.
9. Negative/boundary testcase phai noi ro ly do fail mong doi, khong chi ghi "should fail".
10. Ket qua pass/fail trong report phai den tu `TestExecution`, khong den tu LLM prediction.

## Chuan noi dung Excel report

Excel report chuan can co 4 sheet nhu implementation hien tai.

### 1. `Summary`

Muc dich: cho biet run nao duoc report, moi truong nao, tong quan ket qua va coverage.

Cot/noi dung bat buoc:

| Field | Y nghia | Nguon |
|---|---|---|
| Suite Name | Ten test suite | `TestRunReportContextDto.SuiteName` |
| Run Number | So thu tu run | `Run.RunNumber` |
| Final Status | Trang thai run | `Run.Status` |
| Environment | Moi truong da resolve | `Run.ResolvedEnvironmentName` |
| Generated At | Thoi diem tao report UTC | `TestRunReportDocumentModel.GeneratedAt` |
| Total Tests | Tong testcase | `Run.TotalTests` |
| Passed | So testcase pass | `Run.PassedCount` |
| Failed | So testcase fail | `Run.FailedCount` |
| Skipped | So testcase skipped | `Run.SkippedCount` |
| Total Duration | Tong thoi gian run | `Run.DurationMs` |
| Coverage Percent | Ty le endpoint da test | `Coverage.CoveragePercent` |
| Tested Endpoints | So endpoint co testcase/run result | `Coverage.TestedEndpoints` |
| Total Endpoints | Tong endpoint trong API spec | `Coverage.TotalEndpoints` |

### 2. `Test Cases`

Muc dich: bang chinh de review tung testcase.

Cot bat buoc theo renderer hien tai:

| Column | Noi dung |
|---|---|
| Order | Thu tu testcase |
| Test Case Name | Ten testcase |
| Status | `Passed`, `Failed`, `Skipped` |
| HTTP | HTTP status actual neu co |
| Duration (ms) | Thoi gian testcase |
| Retries | So lan retry, tinh bang `TotalAttempts - 1` |
| Resolved URL | URL sau khi resolve environment/variables |
| Failure Analysis | Cac failure reason hoac ly do skipped vi dependency |
| Response Preview | Body response da sanitize va truncate |

Quy tac chat luong:

- `Status` phai la ket qua execution thuc te.
- `Failure Analysis` phai co code/message ro rang, vi du `STATUS_CODE_MISMATCH`, `SCHEMA_MISMATCH`, `JSON_PATH_MISMATCH`, `RESPONSE_TIME_EXCEEDED`.
- Neu testcase skipped do dependency fail, phai hien thi danh sach dependency ID lien quan.
- `Response Preview` khong duoc chua token, cookie, password, secret, api key.

### 3. `Execution Timeline`

Muc dich: phan tich retry, attempt, skipped cause va loi chi tiet theo thoi gian.

Cot bat buoc:

| Column | Noi dung |
|---|---|
| Test Case Name | Ten testcase hoac ID neu khong map duoc |
| Attempt | So lan attempt |
| Status | Trang thai attempt |
| Duration (ms) | Thoi gian attempt |
| Retry Reason | Ly do retry |
| Skipped Cause | Ly do skipped |
| Start Time | Thoi diem bat dau |
| End Time | Thoi diem ket thuc |
| Detailed Errors | Code, message, expected, actual |

Luu y implementation: sheet nay chi co du lieu khi `TestRunReportContextDto.Attempts` duoc truyen day du qua gateway va khong bi sanitizer lam mat. Neu sheet bi thieu trong report thuc te, can kiem tra lai pipeline `ReportDataSanitizer` va gateway.

### 4. `API Coverage`

Muc dich: do muc bao phu API theo method/tag va liet ke endpoint chua co coverage.

Noi dung bat buoc:

- `Endpoint Coverage By HTTP Method`: method va coverage percent.
- `Endpoint Coverage By Tag`: tag va coverage percent.
- `Uncovered API Endpoints`: danh sach endpoint chua duoc test.

Quy tac:

- Coverage phai tinh tu endpoint metadata cua `ApiDocumentation` va ket qua testcase cua run.
- Khong tinh coverage bang cach dem dong trong Excel.
- Endpoint path nen hien thi kem HTTP method khi co du lieu.

## Mapping voi codebase

| Trach nhiem | File/class chuan |
|---|---|
| API controller | `ClassifiedAds.Modules.TestReporting/Controllers/TestReportsController.cs` |
| Generate command | `ClassifiedAds.Modules.TestReporting/Commands/GenerateTestReportCommand.cs` |
| Report metadata entity | `ClassifiedAds.Modules.TestReporting/Entities/TestReport.cs` |
| Response DTO | `ClassifiedAds.Modules.TestReporting/Models/TestReportModel.cs` |
| Document model cho renderer | `ClassifiedAds.Modules.TestReporting/Models/TestRunReportDocumentModel.cs` |
| Runtime generator | `ClassifiedAds.Modules.TestReporting/Services/TestReportGenerator.cs` |
| Excel renderer | `ClassifiedAds.Modules.TestReporting/Services/ExcelReportRenderer.cs` |
| Data sanitizer | `ClassifiedAds.Modules.TestReporting/Services/ReportDataSanitizer.cs` |
| Cross-module report context | `ClassifiedAds.Contracts/TestExecution/DTOs/TestRunReportContextDto.cs` |

## Cach implement chuan khi can sua/mo rong

### Nguyen tac module

- Khong query truc tiep `TestExecutionDbContext` trong `TestReporting`.
- Khong de LLM ghi report final. LLM chi tao/de xuat testcase; report final phai lay tu execution result.
- Khong tao them bang moi neu chi them field render co the derive tu `TestRunReportContextDto`.
- Neu thay doi entity/configuration/DbContext cua `TestReporting`, bat buoc tao migration trong `ClassifiedAds.Migrator`.
- Neu them project reference/module/runtime dependency, bat buoc check Dockerfile va `docker-compose.yml`.

### Khi them format report moi

1. Them enum vao `ReportFormat`.
2. Tao renderer moi implement `IReportRenderer`.
3. Register renderer trong `ServiceCollectionExtensions`.
4. Cap nhat validation/API docs.
5. Them test cho happy path va unsupported format.
6. Neu format tao binary file, content type va extension phai dung.

### Khi them cot vao Excel

1. Them field vao contract DTO neu field den tu `TestExecution`.
2. Map field trong `TestReportGenerator.BuildCases(...)` hoac document model phu hop.
3. Sanitize field neu co kha nang chua secret/body/header.
4. Render cot trong `ExcelReportRenderer`.
5. Them test snapshot/behavior cho renderer neu co san test infra.
6. Khong them cot chi bang cach parse string LLM response.

### Khi them thong tin LLM vao report

Thong tin LLM duoc phep dua vao report:

- prompt version
- model/provider
- generation timestamp
- testcase source type
- confidence/rationale da sanitize
- feedback status neu da duoc user review

Thong tin LLM khong nen dua vao report final neu chua validated:

- ket luan pass/fail du doan
- secret/token do LLM sinh ra
- raw chain-of-thought
- response body full neu co data nhay cam

## Acceptance criteria cho API testing report

Report dat chuan khi:

1. `POST reports` tra `201 Created` va `TestReportModel` co `downloadUrl`.
2. `GET reports` tra report moi nhat truoc.
3. `GET reports/{reportId}` chi tra report dung `runId`.
4. `GET reports/{reportId}/download` tra dung binary va content type.
5. Excel co day du sheet `Summary`, `Test Cases`, `Execution Timeline` neu co attempts, va `API Coverage`.
6. Moi testcase trong run co dong tuong ung trong `Test Cases`.
7. Failed/skipped testcase co failure/skipped analysis.
8. Retry attempts co the trace duoc trong `Execution Timeline`.
9. Coverage co method/tag/uncovered paths.
10. Report khong lo token, cookie, password, secret, api key.
11. File name dung pattern va nen dung `.xlsx` cho Excel runtime.
12. Metadata report duoc persist trong `testreporting."TestReports"`.
13. Coverage duoc persist/upsert trong `testreporting."CoverageMetrics"`.

## Rủi ro/can kiem tra trong code hien tai

Co hai diem nen kiem tra khi chuan hoa report Excel:

1. `ReportDataSanitizer.Sanitize(...)` hien tai nen giu lai `ProjectName` va `Attempts`. Neu khong, file name co the fallback ve `ModularMonolith` va sheet `Execution Timeline` co the khong co du lieu du attempts.
2. Comment trong `TestReport` noi `ReportFormat: PDF, CSV, JSON, HTML` nhung enum da co `Excel`. Nen cap nhat comment de tranh doc sai contract.

Hai diem nay la khuyen nghi code review, khong phai thay doi trong file tai lieu nay.

## Manual API test checklist

Dung checklist nay de verify API report end-to-end:

```text
1. Dang nhap user co Permission:GetTestRuns.
2. Tao hoac chon test suite co testcase da duoc LLM generate va user chap nhan.
3. Chay test run den trang thai terminal: Completed/Failed.
4. Goi POST /api/test-suites/{suiteId}/test-runs/{runId}/reports voi:
   { "reportType": "Detailed", "format": "Excel", "recentHistoryLimit": 5 }
5. Xac nhan response 201 va co downloadUrl.
6. Goi GET /api/test-suites/{suiteId}/test-runs/{runId}/reports.
7. Goi GET downloadUrl.
8. Mo file .xlsx va check 4 sheet.
9. Doi chieu Passed/Failed/Skipped voi test run summary.
10. Tim token/cookie/password trong file; neu co thi fail security check.
```

## Ket luan

Chuan API testing report cua codebase nay nen dua tren execution result deterministic, khong dua tren LLM text. LLM chi tao testcase dau vao; report final phai lay tu `TestRunReportContextDto`, sanitize, tinh coverage, render file, upload storage va persist metadata. Voi Excel, chuan runtime nen la `.xlsx`; file `.xls` hien tai chi nen xem la file mau ten/noi dung, khong nen xem la extension contract cho API.
