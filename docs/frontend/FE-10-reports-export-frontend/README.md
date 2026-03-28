# FE-10 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-26

Thu muc nay duoc viet rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestReporting`
- `ClassifiedAds.Modules.TestExecution`
- `ClassifiedAds.Modules.Storage`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-10
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-10-reports-export`
- uu tien controller, command/query handler, gateway, renderer, storage download, va exception handling dang chay trong codebase hien tai

## 1. Pham vi FE-10

Runtime frontend-facing hien tai cua FE-10 tap trung vao `TestReportsController`:

- `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`

Thu muc nay chi cover API surface cho report/export synchronous on-demand. Khong cover:

- FE-07/08 test run start/list/detail/results APIs
- UI preview design cho file export
- background queue, polling progress, email delivery, webhook callback, hay async export pipeline
- delete report, revoke file, hay cleanup flow

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Ca 4 endpoint deu can `Permission:GetTestRuns`.
- Ngoai permission, ca generate/list/get/download deu co owner check trong handler/gateway: `suite.CreatedById == CurrentUserId`.
- Owner check dung owner cua test suite, khong dung `run.TriggeredById`.

## 3. Files trong thu muc nay

- `reports-api.json`: contract frontend-facing cho FE-10 tren `TestReportsController`

## 4. Nhung diem FE de noi sai

1. `POST /reports` la synchronous request-response. Backend chi tra ve sau khi da doc run context, sanitize, tinh coverage, render file, upload file, va persist metadata.
2. Moi lan `POST /reports` thanh cong deu tao `TestReport` row moi va upload file moi. Khong co dedupe, khong co idempotent upsert, va khong co "regenerate in place".
3. Generate chi hop le khi test run da `Completed` hoac `Failed`. `Pending`/`Running` tra `409 REPORT_RUN_NOT_READY`.
4. Generate phu thuoc vao detailed run results dang nam trong distributed cache cua FE-07/08. Neu cache het han hoac mat payload thi tra `409 RUN_RESULTS_EXPIRED`.
5. `recentHistoryLimit` la optional. Runtime hien tai mac dinh `5`, cho phep `1..20`, va gia tri > `20` hoac < `1` tra `400`.
6. `reportType` nhan `Summary | Detailed | Coverage`, parse case-insensitive va co trim khoang trang.
7. `format` nhan `PDF | CSV | JSON | HTML`, parse case-insensitive va co trim khoang trang.
8. Response `201` cua `POST` tra body `TestReportModel`; controller dung `CreatedAtAction`, vi vay FE co the ky vong `Location` header tro ve route `GET /reports/{reportId}`.
9. `downloadUrl` trong `TestReportModel` la relative URL, khong phai absolute URL.
10. `GET /reports` khong co pagination, khong co filter, va sort co dinh `GeneratedAt desc`, tie-break `Id desc`.
11. `GET /reports` tra `200 []` neu run hien tai chua co report nao. Backend khong special-case "run khong ton tai" o list route sau khi owner check suite pass.
12. `coverage` duoc attach vao ca list response va detail response neu run da co `CoverageMetric`. No khong chi danh rieng cho `reportType = Coverage`.
13. Coverage o FE-10 la endpoint execution coverage, khong phai pass rate. Test case `Failed` nhung khong `Skipped` van duoc tinh la endpoint da duoc test.
14. Coverage scope dua tren `OrderedEndpointIds` cua execution context, khong phai full API spec. Neu metadata endpoint thieu, backend fallback sang request/resolved URL va co the cho ra `UNKNOWN` / `untagged`.
15. `GET /download` tra binary truc tiep tu Storage, khong co JSON envelope. `Content-Type` va ten file phu thuoc vao format da generate.
16. Khi format la `JSON`, file download khong co shape `TestReportModel`; no la full `TestRunReportDocumentModel` da serialize.
17. `Detailed` la report type duy nhat render full danh sach case-level detail vao file. `Summary` va `Coverage` khong co muc chi tiet nhu vay.
18. Header nhay cam, cookie, token, secret, password, api key, extracted variables, va response body preview deu bi sanitize truoc khi render. FE khong nen ky vong file export chua gia tri raw secret.
19. `responseBodyPreview` trong report file tiep tuc bi truncate theo config host. Runtime hien tai dang dung `4000` ky tu sau sanitize.
20. `expiresAt` duoc tinh theo config retention. Runtime hien tai dang dat `168` gio. Hien chua co endpoint FE-10 de xoa report da het han.
21. `GET /reports/{reportId}` va `GET /download` tra `404` neu khong thay metadata report. Download co them `404 REPORT_FILE_NOT_FOUND` neu metadata ton tai nhung file trong Storage khong con.
22. Hien chua co polling/progress/cancel. UX nen treat generate report nhu mot mutation synchronous co the mat nhieu giay, dac biet voi `PDF`.

## 5. Filter, param, sort hien tai

- `POST /api/test-suites/{suiteId}/test-runs/{runId}/reports`
  - khong co query param
  - body: `reportType`, `format`, `recentHistoryLimit`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports`
  - khong co query param
  - khong co pagination
  - server sort: `GeneratedAt desc`, `Id desc`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}`
  - khong co query param
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download`
  - khong co query param

## 6. Flow goi API frontend nen bam

1. Load run summary/results tu FE-07/08 va chi bat nut export khi run da `Completed` hoac `Failed`.
2. Khi vao man hinh report/export, goi `GET /reports` de lay lich su report da tao cho run hien tai.
3. Khi user chon loai report + format, goi `POST /reports`.
4. Neu `POST` tra `201`, append item moi vao list hoac goi lai `GET /reports`.
5. Khi user can tai file, goi `GET /reports/{reportId}/download` hoac dung `downloadUrl` tu metadata.
6. Neu can preview `JSON`/`HTML`, FE co the fetch download route nhu blob/text thay vi mong cho metadata endpoint tra ve noi dung file.
7. Neu tra `409 RUN_RESULTS_EXPIRED`, hide/disallow generate cho run do va huong user sang xem summary/history hoac chay lai test run.

## 7. Khuyen nghi su dung

- Disable double-click trong luc generate de tranh tao trung nhieu report.
- Hien ro `reportType`, `format`, `generatedAt`, `expiresAt`, va `coverage.coveragePercent` trong report history.
- Dung `id` cua report lam key chinh; khong dung `reportType + format` vi cung mot run co the co nhieu report cung loai.
- Neu UI can preview nhanh, uu tien `HTML` hoac `JSON`; `PDF` va `CSV` phu hop hon cho download.
- Khi render coverage, label dung nghia la "endpoint coverage" thay vi "success rate".
