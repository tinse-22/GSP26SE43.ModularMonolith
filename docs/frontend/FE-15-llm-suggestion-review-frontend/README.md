# FE-15 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-28

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Modules.LlmAssistant`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-15
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-15-llm-suggestion-review`
- uu tien controller, command/query handler, review service, materializer, raw cache behavior, va exception handling dang chay trong codebase hien tai

Neu FE can noi tron ven tu API order -> LLM suggestion review -> generated test cases, dung tai lieu tong hop:

- `docs/frontend/FULL-TEST-FLOW-FE-CONTRACT.md`

## 1. Pham vi FE-15

Runtime frontend-facing hien tai cua FE-15 tap trung vao 4 endpoint core tren `LlmSuggestionsController`:

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
- `GET /api/test-suites/{suiteId}/llm-suggestions`
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`
- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`

Sau cap nhat async refinement ngay 2026-05-16, FE-15 generate preview co them 1 endpoint poll status tren `TestOrderController`:

- `GET /api/test-suites/{suiteId}/generation-status?jobId={jobId}`

Callback moi `POST /api/test-generation/llm-suggestions/callback/{jobId}` chi danh cho n8n, FE khong goi route nay.

Thu muc nay chi cover API surface core cho preview, list, detail, va single-item review. Khong cover:

- FE-05A review/approve API order proposal UI
- FE-05/06 test case CRUD sau khi suggestion da duoc materialize thanh `TestCase`
- FE-16 feedback write endpoint `PUT /feedback`
- FE-17 bulk review endpoint `POST /bulk-review`
- batch preview history UI

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- `POST /generate` can `Permission:GenerateBoundaryNegativeTestCases`.
- `GET /llm-suggestions` va `GET /llm-suggestions/{suggestionId}` can `Permission:GetTestCases`.
- `PUT /review` can `Permission:UpdateTestCase`.
- Ngoai permission, tat ca flow deu owner-check theo `suite.CreatedById == CurrentUserId`.

## 3. Files trong thu muc nay

- `llm-suggestions-api.json`: contract frontend-facing cho FE-15 tren `LlmSuggestionsController`
- `ASYNC-REFINEMENT-FE-FLOW.md`: flow FE moi cho async n8n generation + polling status

## 4. Nhung diem FE de noi sai

1. `POST /generate` la async start endpoint. No tao job, queue n8n, va tra `202 Accepted` voi `jobId`.
2. `POST /generate` khong tao `TestCase` rows va cung khong tao `LlmSuggestion` rows truoc callback n8n.
3. FE khong co suggestion moi de review cho den khi generation job `Completed`.
4. Response `202` khong co `Location` header tro ve suggestion resource.
5. `forceRefresh = false` va con bat ky suggestion `Pending` nao trong suite se tra `400`, khong phai `409`.
6. `forceRefresh = true` supersede bo `Pending` hien tai ngay truoc khi queue job moi.
7. Generate preview van check `MaxLlmCallsPerMonth` truoc khi tao job.
8. Generate preview yeu cau suite da co approved API order. Neu gate fail backend tra `409 ORDER_CONFIRMATION_REQUIRED`.
9. Generate preview hien tai chi sinh `suggestionType = BoundaryNegative`. FE-15 runtime chua mo mot happy-path preview flow rieng.
10. `GET /llm-suggestions` chi co 3 filter: `reviewStatus`, `testType`, `endpointId`. Khong co pagination, search, hay `suggestionType` filter.
11. Sort cua `GET /llm-suggestions` la co dinh `displayOrder asc`.
12. `reviewStatus` hoac `testType` query value sai enum hien tai bi backend bo qua trong silence, khong tra loi validation error.
13. `LlmSuggestionModel.SuggestedRequest` va `SuggestedExpectation` la chuoi JSON, khong phai object da parse. `ModifiedContent` trong response cung la chuoi JSON neu suggestion da Modify.
14. `SuggestedTags` va `SuggestedVariables` trong response da duoc parse san thanh array/list.
15. `CacheKey` co trong response model nhung generate path hien tai dang set `null`; FE khong nen dung no lam batch id.
16. Moi review request deu phai gui `rowVersion` base64. Day la optimistic concurrency token, khong phai optional field.
17. `action` hop le la `Approve`, `Reject`, `Modify`, parse case-insensitive.
18. `Reject` bat buoc co `reviewNotes`. `Modify` bat buoc co `modifiedContent`.
19. `Modify` cho phep gui partial top-level fields. Neu bo trong `name`, `description`, `testType`, `priority`, `tags` thi backend fallback ve suggestion goc.
20. Tuy nhien `modifiedContent.request`, `modifiedContent.expectation`, va `modifiedContent.variables` la whole-block replacement. Neu FE gui 1 block thi backend dung nguyen block do, khong deep-merge tung field con.
21. `Approve` retry sau khi suggestion da duoc materialize la idempotent. Backend tra `200` suggestion hien tai thay vi tao duplicate `TestCase`.
22. `Approve` va `Modify` tao real `TestCase`, `TestCaseChangeLog`, `TestSuiteVersion`, va set `appliedTestCaseId`. `Reject` chi doi review state.
23. Non-owner access hien dang map thanh `400 ProblemDetails` do ValidationException, khong phai `403`.
24. `currentUserFeedback` va `feedbackSummary` chi duoc hydrate o list/detail query. Review response hien tai thuong tra `null` cho 2 field nay du model van co field.
25. Generate preview co archived guard; list/detail/review hien tai khong them archived guard rieng trong handler.
26. Generate path FE-15 khong con tra `local-draft`.
27. Sau `202 Accepted`, FE phai poll `/generation-status?jobId=...`.
28. Khi job status thanh `Completed`, FE phai refetch `GET /llm-suggestions` de lay suggestions va rowVersion moi.
29. Khi job status thanh `Failed`, FE hien loi va cho regenerate voi `forceRefresh=true`; khong co draft moi de review.

## 5. Filter, param, sort hien tai

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
  - khong co query param
  - body: `specificationId`, `forceRefresh`, `algorithmProfile`
- `GET /api/test-suites/{suiteId}/generation-status`
  - query required khi poll async LLM suggestion refine: `jobId`
  - server co the fallback latest job theo suite neu bo `jobId`, nhung FE nen luon truyen `jobId` de tranh lay nham job generate test cases khac
- `GET /api/test-suites/{suiteId}/llm-suggestions`
  - query optional: `reviewStatus`, `testType`, `endpointId`
  - khong co pagination
  - server sort: `displayOrder asc`
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`
  - khong co query param
- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`
  - khong co query param
  - body: `action`, `rowVersion`, `reviewNotes`, `modifiedContent`

## 6. Flow goi API frontend nen bam

1. Truoc khi mo FE-15 review screen, dam bao suite da qua FE-05A va co approved API order.
2. Khi vao man hinh review, goi `GET /llm-suggestions` de lay suggestion set hien tai.
3. Neu list rong va user muon sinh preview moi, goi `POST /generate`.
4. Nhan `jobId` tu response `202 Accepted`, sau do poll `GET /generation-status?jobId={jobId}`.
5. Trong luc poll, hien trang thai dang generate; chua render suggestion moi de review.
6. Khi job `Completed`, refetch `GET /llm-suggestions` de lay suggestions moi.
7. Khi job `Failed` hoac `Cancelled`, hien loi va cho user regenerate.
9. Neu user can payload day du hoac rowVersion moi nhat cho 1 item, goi `GET /llm-suggestions/{suggestionId}` truoc khi review.
10. Khi user approve/reject/modify, luon gui `rowVersion` moi nhat tu list/detail.
11. Sau review thanh cong, update local row hoac refetch list. Neu action la approve/modify va response co `appliedTestCaseId`, FE co the deeplink sang man hinh test case neu can.
12. Neu tra `409 CONCURRENCY_CONFLICT`, reload detail/list roi moi cho user thao tac lai.
13. Neu `POST /generate` tra `400` vi dang con `Pending` suggestions, FE nen cho user chon review tiep bo hien tai hoac regenerate voi `forceRefresh = true`.

## 7. Khuyen nghi su dung

- Parse `suggestedRequest`, `suggestedExpectation`, va `modifiedContent` thanh client-side editor state truoc khi render form chinh sua.
- Dung `id` cua suggestion lam primary key; khong dung `cacheKey`.
- Disable double-click khi review de tranh gui duplicate mutation cung `rowVersion`.
- Neu FE cho edit `request`, `expectation`, hoac `variables`, hay gui full block da sua thay vi patch tung field con.
- Hien ro badge `reviewStatus`, `testType`, `priority`, `llmModel`, `tokensUsed`, `reviewedAt`, va `appliedTestCaseId`.
- Hien trang thai generation theo job status poll.
- Co the tan dung `currentUserFeedback`/`feedbackSummary` de render read-only metadata ngay tu FE-15, nhung write action cho feedback nen theo tai lieu FE-16.
