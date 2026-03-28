# FE-07-08 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestExecution`
- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-07 + FE-08
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-07-08-test-execution-validation`
- uu tien controller, command, query, gateway, orchestrator, validator, exception handling, va test dang chay trong codebase hien tai

## 1. Pham vi FE-07-08

Runtime frontend-facing hien tai cua FE-07-08 tap trung vao `TestRunsController`:

- `POST /api/test-suites/{suiteId}/test-runs`
- `GET /api/test-suites/{suiteId}/test-runs`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`

Thu muc nay chi cover execution + validation API surface. Khong cover:

- FE-04 execution environment CRUD trong `ExecutionEnvironmentsController`
- FE-05 / FE-06 test case read/detail endpoints dung de hien metadata nguon
- FE-09 failure explanation
- FE-10 report export
- progress realtime, cancel API, background queue, webhook trigger

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- `POST /test-runs` can `Permission:StartTestRun`.
- Cac endpoint `GET` can `Permission:GetTestRuns`.
- Ngoai permission, ca 4 endpoint deu co owner check theo `suite.CreatedById == CurrentUserId`.

## 3. Files trong thu muc nay

- `test-runs-api.json`: contract frontend-facing cho FE-07 + FE-08 tren `TestRunsController`

## 4. Nhung diem FE de noi sai

1. Runtime hien tai la synchronous request-response. `POST /test-runs` se chay run ngay trong request hien tai va tra ve `TestRunResultModel` sau khi run da ket thuc, khong phai fire-and-poll async flow.
2. Hien chua co frontend primary API cho progress live, cancel run, SignalR progress, webhook trigger, hay background execution queue.
3. Neu request khong truyen `environmentId`, backend se fallback sang default execution environment cua project. Neu project khong con default environment thi tra `404`.
4. `selectedTestCaseIds` neu omit hoac rong sau khi backend bo `Guid.Empty` va duplicate thi runtime se chay tat ca test case dang `IsEnabled = true`.
5. `selectedTestCaseIds` chi la tap chon subset, khong phai execution order. Backend se sap lai theo approved endpoint order cua FE-05A, sau do theo `CustomOrderIndex/OrderIndex`, `Name`, `Id`.
6. Neu subset duoc chon thieu dependency can thiet thi API tra `400` validation error, khong auto them dependency bi thieu.
7. Neu user chon test case da bi disable hoac test case khong con nam trong suite thi API tra `400`.
8. Start run chi duoc phep khi `TestSuite.Status == Ready`.
9. Start run van bi gate boi FE-05A. Backend goi `RequireApprovedOrderAsync` va block neu khong co active proposal da apply order. Day la `409 ORDER_CONFIRMATION_REQUIRED`, khong chi la warning UI.
10. `POST /test-runs` tra `201` voi body chi tiet, nhung controller hien tai khong set `Location` header.
11. Trong success path thong thuong, `run.status` trong response POST se la `Completed` hoac `Failed` vi command doi run xong roi moi tra ve.
12. `GET /test-runs` sort theo `CreatedDateTime desc`, tie-break `Id desc`; khong sort theo `RunNumber desc`.
13. Query `status` o `GET /test-runs` parse enum khong phan biet hoa thuong. Neu FE gui gia tri sai, backend silently bo qua filter thay vi tra `400`.
14. `pageNumber` duoc clamp toi thieu `1`; `pageSize` duoc clamp trong khoang `1..100`.
15. `run.hasDetailedResults` duoc tinh theo `resultsExpireAt > utcNow`, khong phai bang cach check cache real-time.
16. `GET /results` neu `RedisKey` rong, `ResultsExpireAt` da qua han, hoac cache miss thi tra `409 RUN_RESULTS_EXPIRED`, khong phai `410`.
17. Ke ca khi chi tiet da het han, FE van doc duoc summary qua `GET /test-runs` va `GET /test-runs/{runId}`.
18. `resultsSource` trong detailed result hien luon la `"cache"` cho ca POST success response va GET results response.
19. `TestCaseRunResultModel.orderIndex` la execution order runtime duoc reindex tu `0..N-1`, co the khac voi `orderIndex` khi FE doc lai test case qua `GET /test-cases`.
20. `resolvedUrl` trong detailed result khong gom query string runtime. Query params va query-based auth duoc append trong `HttpTestExecutor` sau do, nhung khong co field nao tra lai day du URL cuoi cung.
21. Detailed result khong tra ve resolved body, resolved query params, hay bodyType. Neu UI can render day du request goc, FE phai ghep voi metadata tu `GET /api/test-suites/{suiteId}/test-cases` hoac detail test case.
22. `requestHeaders` trong detailed result la final headers sau khi env auth/default headers da inject va request-level headers override xong. Cac gia tri nay hien khong duoc mask.
23. `extractedVariables` chi duoc mask bang keyword-based rule tren ten bien co chua `token`, `secret`, `password`, hoac `apikey`. Day khong phai che do secret detection day du.
24. `responseBodyPreview` bi truncate toi da `65536` ky tu. Hien khong co flag nao bao da bi truncate.
25. `run.durationMs` la tong `DurationMs` cua tung case, khong phai wall-clock end-to-end duration cua toan request.
26. Validation engine la rule-based 100%, khong goi LLM de quyet dinh pass/fail. Cac check hien co: status code, response schema, header exact-match, body contains, body not contains, JSONPath equality, max response time.
27. Neu expectation khong co `responseSchema`, validator co the fallback sang schema dau tien tra ve tu endpoint metadata service. Neu van khong co schema thi check schema bi bo qua, khong fail ca run.
28. Loi transport HTTP bi map thanh failed case voi `HTTP_REQUEST_ERROR`, `httpStatusCode = null`, va khong co schema/header/body/jsonpath assertion tiep theo.
29. Request-level header co quyen override env-level auth header. Vi vay negative auth cases co the co chu y de "de-auth" request trong runtime.
30. URL absolute trong test case se duoc giu nguyen; chi URL relative moi ghep voi `ExecutionEnvironment.baseUrl`.

## 5. Filter, param, sort hien tai

- `POST /api/test-suites/{suiteId}/test-runs`
  - body field optional: `environmentId`
  - body field optional: `selectedTestCaseIds`
  - khong co query param
- `GET /api/test-suites/{suiteId}/test-runs`
  - query `pageNumber` mac dinh `1`, min runtime `1`
  - query `pageSize` mac dinh `20`, runtime clamp `1..100`
  - query `status` la string enum case-insensitive: `Pending | Running | Completed | Failed | Cancelled`
  - backend sort co dinh `CreatedDateTime desc`, tie-break `Id desc`
- `GET /api/test-suites/{suiteId}/test-runs/{runId}`
  - khong co query param
- `GET /api/test-suites/{suiteId}/test-runs/{runId}/results`
  - khong co query param

## 6. Flow goi API frontend nen bam

1. Tao/quan ly execution environment o FE-04, dam bao project con default environment neu UI muon cho phep `environmentId = null`.
2. Hoan tat FE-05A gate de suite co applied API order hop le.
3. Tao/generate test cases qua FE-05B va FE-06.
4. Goi `POST /api/test-suites/{suiteId}/test-runs`.
5. Render ngay ket qua chi tiet tu response POST neu can UX "run va xem ngay".
6. Goi `GET /api/test-suites/{suiteId}/test-runs` de load lich su run co paging.
7. Goi `GET /api/test-suites/{suiteId}/test-runs/{runId}` khi can summary nhanh hoac khi chi tiet cache da het han.
8. Goi `GET /api/test-suites/{suiteId}/test-runs/{runId}/results` khi can nap lai chi tiet tu cache con han.
9. Khi can render du lieu request/expectation day du, ghep them metadata tu `GET /api/test-suites/{suiteId}/test-cases` hoac detail test case tu FE-05/06 handoff.

## 7. Khuyen nghi su dung

- Treat `POST /test-runs` nhu long-running mutation: co loading state, timeout UX, va khong can poll ngay neu request POST da thanh cong vi no da tra detailed result roi.
- Neu UI co nut "chay subset", validate dependency closure tren client neu co the, nhung van phai xu ly `400` tu backend.
- Dung `GET /test-runs` lam lich su authoritative, khong suy luan thu tu run chi bang `runNumber`.
- Dung `run.hasDetailedResults` + `resultsExpireAt` de bat/tat nut "xem chi tiet", nhung van xu ly truong hop cache miss dot xuat voi `409 RUN_RESULTS_EXPIRED`.
- Treat `requestHeaders` va `extractedVariables` nhu sensitive data trong UI/logging.
- Neu UI can hien "final URL da goi", can ghi nho `resolvedUrl` hien tai khong kem query string runtime.
