# FE-09 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-26

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.LlmAssistant`
- `ClassifiedAds.Modules.TestExecution`
- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, response, status code, va runtime notes de tich hop nhanh cho FE-09
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-09-failure-explanation`
- uu tien controller, command/query handler, gateway, cache behavior, va exception handling dang chay trong codebase hien tai

## 1. Pham vi FE-09

Runtime frontend-facing hien tai cua FE-09 tap trung vao `FailureExplanationsController`:

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
- `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`

Thu muc nay chi cover API surface cho failure explanation. Khong cover:

- FE-07/08 test run start/list/detail/results APIs
- report/export cua FE-10
- async queue, retry worker, polling status, webhook callback, hay background pre-generation cho explanation

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Ca `GET` va `POST` deu can `Permission:GetTestRuns`.
- Ngoai permission, ca 2 endpoint deu co owner check trong handler: `suite.CreatedById == CurrentUserId`.

## 3. Files trong thu muc nay

- `failure-explanations-api.json`: contract frontend-facing cho FE-09 tren `FailureExplanationsController`

## 4. Nhung diem FE de noi sai

1. Ca `GET` va `POST` deu bat dau bang viec build failure context tu detailed run results cua FE-07/08. Neu detailed run results da het han thi FE-09 cung tra `409 RUN_RESULTS_EXPIRED`.
2. `GET /explanation` la cache-only. Backend khong build prompt, khong goi provider, khong tao audit, va khong refresh cache TTL.
3. `POST /explanation` la cache-first. Neu fingerprint da co cached explanation hop le thi response van la `200` voi `source = "cache"`.
4. `POST /explanation` khong nhan request body. Tat ca du lieu de giai thich deu lay tu suite, run, test case definition, va failed case result.
5. Chi test case co `status = Failed` moi hop le. `Passed` hoac `Skipped` deu tra `409 TEST_CASE_NOT_FAILED`.
6. `GET` cache miss hien duoc map thanh `404 Not Found` va message prefix `FAILURE_EXPLANATION_NOT_FOUND:`. Response 404 hien khong co field `reasonCode` rieng.
7. Owner check dung `suite.CreatedById`, khong dung `run.TriggeredById`.
8. `FailureExplanationModel` khong tra request/response chi tiet hay deterministic `failureReasons` full object. Neu UI can man hinh detail day du, FE phai join them voi FE-07/08 results va test case metadata tu FE-05/06.
9. `source` chi co 2 gia tri runtime `cache` va `live`; `latencyMs` se la `0` tren cache hit.
10. `provider` va `model` la gia tri runtime. Appsettings hien tai cua `ClassifiedAds.WebAPI` dang dat `Provider=N8n`, `Model=deepseek-chat`.
11. `confidence` la string thuong; prompt yeu cau `Low|Medium|High` nhung backend khong enforce enum sau khi parse xong.
12. Endpoint metadata tu `ApiDocumentation` la optional. Neu `ApiSpecId` hoac `EndpointId` thieu thi `POST` van co the generate explanation dua tren failure context san co.
13. Gia tri nhay cam trong headers, extracted variables, body preview, va expectation payload duoc sanitize truoc khi tao fingerprint, goi provider, va luu cache/audit.
14. Provider tra HTTP non-2xx hien duoc map thanh `409 Conflict` voi `reasonCode = FAILURE_EXPLANATION_PROVIDER_HTTP_ERROR`, khong phai `502`.
15. Provider JSON sai schema hoac `BaseUrl` chua cau hinh hien duoc map thanh `400 Bad Request` qua `ValidationException`, khong phai `502`.
16. Loi luu audit hoac cache sau khi live generation khong fail request; backend log warning va van tra explanation `source = "live"`.
17. Hien chua co async queue, progress, polling, cancel, hay webhook callback cho FE-09. UX nen treat `POST /explanation` nhu mot long-running mutation synchronous.
18. Explanation cache TTL mac dinh la 24h, nhung cached explanation van khong doc duoc neu detailed run results cua chinh test run da het han trong distributed cache.

## 5. Filter, param, sort hien tai

- `GET /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
  - khong co query param
  - khong co request body
- `POST /api/test-suites/{suiteId}/test-runs/{runId}/failures/{testCaseId}/explanation`
  - khong co query param
  - khong co request body

## 6. Flow goi API frontend nen bam

1. Goi FE-07 `POST /test-runs` hoac `GET /test-runs/{runId}/results` de co danh sach `cases`.
2. Chi hien nut "Explain failure" cho case co `status = Failed`.
3. Khi mo panel detail, co the goi `GET /explanation` truoc de check cache.
4. Neu `GET` tra `404 FAILURE_EXPLANATION_NOT_FOUND`, moi bat `POST /explanation` de generate live.
5. Render ket qua tu `FailureExplanationModel`, kem badge `source`, `provider`, `model`, `generatedAt`, va `confidence`.
6. Neu tra `409 RUN_RESULTS_EXPIRED`, disable generate/regenerate cho run do va fallback ve summary/history tu FE-07/08.

## 7. Khuyen nghi su dung

- Neu muon toi uu chi phi provider, hay goi `GET` truoc roi moi `POST` khi cache miss.
- Treat explanation nhu lop "assistant insight" cho failed case, khong dung de thay doi pass/fail, status, hay counters.
- Giu local cache theo cap `suiteId + runId + testCaseId` de tranh goi lap lai khong can thiet trong mot session UI.
- Vi FE-07/08 detailed results hien con co header/body nhay cam chua mask day du, UI/logging phia frontend nen che mat hoac han che hien thi cac field nay.
