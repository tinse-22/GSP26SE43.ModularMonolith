# FE-17 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-28

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-17
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-17-llm-suggestion-bulk-review`
- uu tien controller, bulk command handler, shared review service, filter semantics, va exception handling dang chay trong codebase hien tai

## 1. Pham vi FE-17

Runtime frontend-facing hien tai cua FE-17 tap trung vao `LlmSuggestionsController`:

- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`

FE-17 khong song doc lap. No duoc thiet ke de di cung 2 surface read da co san:

- `GET /api/test-suites/{suiteId}/llm-suggestions`
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`

Thu muc nay chi cover mutation bulk-review cua FE-17 va cach FE nen dung read surface lien quan. Khong cover:

- FE-15 preview generation `POST /generate`
- FE-15 single review `PUT /review`
- FE-16 feedback write `PUT /feedback`
- bulk modify-and-approve voi noi dung rieng cho tung suggestion
- API bulk review theo danh sach `suggestionIds[]`

## 2. Auth

- Tat ca endpoint lien quan deu yeu cau Bearer token.
- `POST /bulk-review` can `Permission:UpdateTestCase`.
- 2 endpoint `GET` de load/refetch suggestion state van can `Permission:GetTestCases`.
- Ngoai permission, owner check van theo `suite.CreatedById == CurrentUserId`.

## 3. Files trong thu muc nay

- `llm-suggestions-bulk-review-api.json`: contract frontend-facing cho FE-17 tren `LlmSuggestionsController`

## 4. Nhung diem FE de noi sai

1. FE-17 hien tai la bulk review theo bo filter, khong phai bulk review theo danh sach `suggestionId` tuy chon. Backend khong nhan `ids[]`.
2. `POST /bulk-review` chi ho tro 2 action: `Approve` va `Reject`. Khong co `Modify`.
3. `action` parse case-insensitive nhung khong co trim whitespace rieng. FE nen gui exact enum value sach se.
4. Request FE-17 khong yeu cau `rowVersion`. Concurrency conflict duoc xu ly ben trong transaction server-side.
5. `reviewNotes` la mot gia tri dung chung cho toan bo batch. FE khong the gui note rieng cho tung suggestion.
6. `reviewNotes` la optional khi `Approve` nhung bat buoc khi `Reject`.
7. Matched set luon duoc server tinh lai theo `suiteId` + bo filter body. FE khong duoc gia dinh "visible rows tren client" la source of truth.
8. Filter semantics hien tai chi co 3 truong: `filterBySuggestionType`, `filterByTestType`, `filterByEndpointId`.
9. Neu gui dong thoi nhieu filter, server ket hop theo logic AND.
10. FE-17 chi xu ly suggestions dang `Pending`. Suggestions da `Approved`, `Rejected`, `ModifiedAndApproved`, hoac `Superseded` se khong nam trong matched set.
11. Khac voi `GET /llm-suggestions` cua FE-15, enum filter sai tren FE-17 khong bi ignore silently. `filterBySuggestionType` hoac `filterByTestType` sai se tra `400 ProblemDetails`.
12. Neu khong co suggestion nao match, backend van tra `200` hop le voi `matchedCount = 0`, `processedCount = 0`, `materializedCount = 0`, va khong co side effect.
13. Bulk response chi la summary batch. No khong tra lai `LlmSuggestionModel[]` da cap nhat.
14. Sau khi bulk approve/reject thanh cong, FE nen refetch `GET /llm-suggestions` hoac `GET /llm-suggestions/{suggestionId}` de lay `reviewStatus`, `rowVersion`, `appliedTestCaseId`, `currentUserFeedback`, va `feedbackSummary` moi nhat.
15. Bulk approve reuse chung shared review/materialization service cua FE-15, nen van tao `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestCaseChangeLog`, va 1 `TestSuiteVersion` cho ca batch.
16. Bulk approve append `TestCase.OrderIndex` sau `max(OrderIndex)` hien co trong suite, khong reset lai tu 0.
17. Bulk approve check `MaxTestCasesPerSuite` cho tong batch size truoc khi ghi. Neu vuot limit thi tra `400 ProblemDetails`.
18. Bulk approve van bi chan neu suite chua co approved API order. Khi do backend tra `409 ORDER_CONFIRMATION_REQUIRED`.
19. Bulk reject chi doi metadata tren suggestion rows, khong tao `TestCase`, khong tao `TestSuiteVersion`, va khong increment usage quota.
20. `suggestionIds` trong response theo thu tu `DisplayOrder asc` cua batch server-side. `appliedTestCaseIds` cung theo thu tu materialize do.
21. `reviewedAt` la timestamp server UTC. Ngay ca zero-match path cung van co timestamp nay du khong co row nao duoc sua.
22. FE-17 khong co archived guard rieng trong bulk handler. Neu FE can chan UX cho archived suite thi do la logic bo sung ben client, khong phai current server contract.
23. Generate path cua FE-15 hien tai van tao `suggestionType = BoundaryNegative`, vi vay cac filter `HappyPath`, `Security`, hoac `Performance` co the hop le ve contract nhung thuong tra `0` o runtime hien tai.
24. Neu FE muon UX checkbox multi-select tu do, contract hien tai chua du. Chi nen expose "Approve/Reject tat ca row khop bo filter server-side" hoac fallback sang single review FE-15.

## 5. Filter, param, sort hien tai

- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`
  - khong co query param
  - body: `action`, `filterBySuggestionType`, `filterByTestType`, `filterByEndpointId`, `reviewNotes`
- Matched set server-side luon:
  - filter theo `TestSuiteId`
  - filter theo `ReviewStatus = Pending`
  - apply them 3 filter optional neu co
  - sort `DisplayOrder asc` truoc khi process

## 6. Flow goi API frontend nen bam

1. Load danh sach suggestion hien tai bang `GET /llm-suggestions` cua FE-15.
2. Xac dinh ro UI dang bulk tren scope nao: tat ca pending, theo endpoint, theo test type, theo suggestion type, hay ket hop cac filter nay.
3. Khi user bam bulk approve/reject, gui `POST /bulk-review` voi bo filter server-side tuong ung.
4. Neu response `200` nhung `processedCount = 0`, show thong bao no-op thay vi coi la loi.
5. Neu action la `Approve`, co the dung `appliedTestCaseIds` de deeplink sang man hinh test case neu can.
6. Sau moi lan bulk review thanh cong, refetch `GET /llm-suggestions` de dong bo state hien thi.
7. Neu `POST /bulk-review` tra `409 ORDER_CONFIRMATION_REQUIRED`, dieu huong user sang FE-05A de review/approve API order truoc.
8. Neu `POST /bulk-review` tra `409 CONCURRENCY_CONFLICT`, reload list/detail roi moi cho user thao tac lai.
9. Neu `POST /bulk-review` tra `400` vi invalid filter, missing notes, hoac quota limit, giu nguyen man hinh va show thang message tu `ProblemDetails`.

## 7. Khuyen nghi su dung

- Build UX quanh khai niem "apply to current server-side filter", khong quanh "selected ids" neu backend chua ho tro contract do.
- Truoc khi submit, FE co the show so luong pending rows dang hien tren list nhu mot estimate, nhung van phai coi server la source of truth cho matched count.
- Disable double-click trong luc dang submit batch de tranh conflict va duplicate action.
- Sau khi approve/reject, uu tien refetch list thay vi tu suy luan local state tu summary response.
- Neu UI co filter client-side phu tro nhu search text hay grouping, dung de gui nham y tuong rang server se ton trong cac filter do.
- Neu user can reject hang loat, yeu cau note mot lan cho ca batch va noi ro note nay se duoc gan cho tat ca matched suggestions.
