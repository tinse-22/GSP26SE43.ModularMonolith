# FE-05 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-05A + FE-05B
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-05-test-generation`
- uu tien controller, command, query, model, service, va exception handler dang chay trong codebase hien tai

## 1. Pham vi FE-05

Feature nay gom 2 phase runtime can noi:

- FE-05A: `API order proposal + review/reorder/approve gate` qua route goc `/api/test-suites/{suiteId}`
- FE-05B: `Happy-path generation + read generated test cases` qua route goc `/api/test-suites/{suiteId}/test-cases`

Frontend handoff nay chi cover FE-05-facing endpoints. Cac endpoint test-case khac trong `TestCasesController` cho boundary-negative, CRUD thu cong, toggle, va reorder thuoc scope sau FE-05 nen khong duoc handoff day du o day.

## 2. Auth

- Tat ca frontend-facing endpoint trong thu muc nay deu yeu cau Bearer token.
- Moi action con bi rang buoc boi permission policy o backend.
- Toan bo endpoint FE-05A write/read deu co them owner check trong handler: `suite.CreatedById == CurrentUserId`.
- `GET /api/test-suites/{suiteId}/test-cases` va `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}` hien chi check permission + suite/test case ton tai, khong co owner check rieng trong query handler.

## 3. Files trong thu muc nay

- `order-proposals-api.json`: contract cho FE-05A va cac route `TestOrderController` co lien quan den generation gate
- `happy-path-test-cases-api.json`: contract cho FE-05B primary route `generate-happy-path` va cac route read generated test cases

## 4. Nhung diem FE de noi sai

1. FE-05 runtime hien tai khong co endpoint list proposal history. Frontend chi co `GET latest`.
2. `GET /api/test-suites/{suiteId}/order-proposals/latest` tra proposal moi nhat theo `ProposalNumber desc`, bat ke no dang `Pending`, `Rejected`, `Superseded`, hay `Approved`.
3. `POST /api/test-suites/{suiteId}/order-proposals` se fallback sang `TestSuite.SelectedEndpointIds` da luu o FE-04 neu request gui `selectedEndpointIds = []` hoac omit field nay.
4. Tao proposal moi se mark cac proposal cu o trang thai `Pending`, `Approved`, va `ModifiedAndApproved` thanh `Superseded`.
5. `ApiTestOrderProposalModel.status` va `source` la string enum trong JSON response, nhung `order-gate-status.activeProposalStatus` lai la numeric enum vi model nay khong gan `JsonStringEnumConverter`.
6. `ApiOrderItemModel.orderIndex` duoc backend normalize thanh day so 1..N moi lan deserialize/serialize; FE khong nen coi order index cu la on dinh sau reorder.
7. `rowVersion` la base64 string va bat buoc cho `reorder`, `approve`, `reject`. FE phai luu rowVersion moi nhat tu response sau moi lan mutate.
8. `approve` co fast-path idempotent: neu proposal da `Approved`, `ModifiedAndApproved`, hoac `Applied` va da co `AppliedOrder`, backend tra ve ngay snapshot hien tai. Trong fast-path nay rowVersion stale van khong bi check tiep, mien request body qua duoc model validation.
9. Gate pass runtime duoc tinh boi `AppliedOrder` cua proposal active, khong chi dua vao `TestSuite.ApprovalStatus`.
10. `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path` bat buoc `specificationId != Guid.Empty`, nhung code hien tai khong doi chieu field nay voi `suite.ApiSpecId` truoc khi generate.
11. `generate-happy-path` block neu da co happy-path test case va `forceRegenerate = false`. Neu `forceRegenerate = true`, backend chi xoa `TestType = HappyPath`, khong dong den Boundary/Negative cases.
12. Neu pipeline generate tra ve 0 test case, backend van tra `201 Created` voi `totalGenerated = 0`, nhung khong persist test case nao va cung khong update `TestSuite.Status` sang `Ready`.
13. `GET /api/test-suites/{suiteId}/test-cases` co query `testType` dang string. Neu FE gui gia tri enum sai, backend silently bo qua filter thay vi tra `400`.
14. `GET /api/test-suites/{suiteId}/test-cases` sort co dinh theo `OrderIndex asc`, khong co pagination, khong co client sort.
15. Trong `TestCaseModel`, cac field sau hien la chuoi JSON da serialize, khong phai object/array typed: `request.headers`, `request.pathParams`, `request.queryParams`, `expectation.expectedStatus`, `expectation.headerChecks`, `expectation.bodyContains`, `expectation.bodyNotContains`, `expectation.jsonPathChecks`.
16. Happy-path generator se auto chen tag `happy-path` va `auto-generated` neu n8n/LLM khong tra ve hoac tra ve thieu.
17. Runtime van con route legacy `POST /api/test-suites/{suiteId}/generate-tests` trong `TestOrderController`, tra `202 Accepted` va trigger n8n callback flow. Day khong phai route frontend primary cho FE-05B nua; frontend moi nen uu tien `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`.
18. Callback `/api/test-suites/{suiteId}/test-cases/from-ai` dung `x-callback-api-key`, khong dung JWT, va khong phai endpoint frontend-facing.

## 5. Filter, param, sort hien tai

- `GET /api/test-suites/{suiteId}/order-proposals/latest`
  - khong co query param
  - khong co pagination
- `GET /api/test-suites/{suiteId}/order-gate-status`
  - khong co query param
- `GET /api/test-suites/{suiteId}/test-cases`
  - query `testType` la string enum case-insensitive: `HappyPath | Boundary | Negative | Performance | Security`
  - query `includeDisabled` mac dinh `false`
  - khong co pagination
  - backend sort co dinh `OrderIndex asc`

## 6. Flow goi API frontend nen bam

1. Tao hoac load `TestSuite` tu FE-04.
2. Goi `POST /api/test-suites/{suiteId}/order-proposals`.
3. Render de xuat tu `proposedOrder`.
4. Neu user thay doi thu tu, goi `PUT /api/test-suites/{suiteId}/order-proposals/{proposalId}/reorder`.
5. Goi `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`.
6. Truoc khi mo nut generate, co the goi `GET /api/test-suites/{suiteId}/order-gate-status`.
7. Goi `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`.
8. Goi `GET /api/test-suites/{suiteId}/test-cases?testType=HappyPath` de doc danh sach vua generate.
9. Goi `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}` khi can man hinh detail.

## 7. Khuyen nghi su dung

- Dung `order-gate-status` de bat/tat nut generate thay vi chi nhin `approvalStatus` o `TestSuite`.
- Luon cap nhat `rowVersion` moi nhat tu response proposal sau `reorder` va `approve/reject`.
- Khi render `TestCaseModel`, parse cac field JSON-string thanh object/array truoc khi dua vao editor/viewer.
- Neu can support route legacy `generate-tests`, treat no nhu fallback/ops path, khong dung lam FE-05B primary integration.
