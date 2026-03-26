# FE-06 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.Modules.LlmAssistant`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-06
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-06-boundary-negative-generation`
- uu tien controller, command, query, model, service, va exception handling dang chay trong codebase hien tai

## 1. Pham vi FE-06

Feature nay o runtime frontend-facing hien tai gom 1 nhom route chinh:

- `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`
- `GET /api/test-suites/{suiteId}/test-cases`
- `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}`

Thu muc nay chi cover FE-06 direct generation flow:

- generate boundary/negative test cases qua 3 pipeline `path mutations`, `body mutations`, `LLM suggestions`
- doc lai ket qua generate qua list/detail test cases

Khong cover trong handoff nay:

- `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path` cua FE-05B
- manual CRUD/reorder/toggle trong `TestCasesController`
- `LlmSuggestionsController` voi route goc `/api/test-suites/{suiteId}/llm-suggestions` vi day la preview/review flow cua FE-15/FE-17, khong phai FE-06 direct-generate contract

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Moi action con bi rang buoc boi permission policy o backend.
- `POST generate-boundary-negative` co owner check trong handler: `suite.CreatedById == CurrentUserId`.
- `GET list` va `GET detail` hien chi check permission + entity ton tai, khong co owner check rieng trong query handler.

## 3. Files trong thu muc nay

- `boundary-negative-test-cases-api.json`: contract frontend-facing cho FE-06 tren `TestCasesController`

## 4. Nhung diem FE de noi sai

1. Runtime hien tai khong doi chieu `specificationId` voi `suite.ApiSpecId` hay spec cua proposal da approve truoc khi generate. Backend chi check `Guid.Empty`, sau do dung gia tri nay de goi metadata services.
2. FE-06 van bi gate boi FE-05A. Gate pass duoc tinh boi `AppliedOrder` cua proposal active co status `Approved | ModifiedAndApproved | Applied`, khong chi dua vao `TestSuite.ApprovalStatus`.
3. It nhat 1 pipeline flag phai bat: `includePathMutations`, `includeBodyMutations`, hoac `includeLlmSuggestions`.
4. `forceRegenerate=true` chi xoa test case `Boundary` va `Negative`; khong dong den `HappyPath`.
5. Neu `forceRegenerate=true` da xoa case cu xong ma generation moi tra ve `0` case, backend van tra `201` voi `totalGenerated = 0`. O fast path rong nay suite khong duoc update `Status`/`Version`, nhung boundary-negative case cu da bi xoa.
6. `GET /api/test-suites/{suiteId}/test-cases` va `GET by id` la read endpoint dung chung cho moi test type. Muon xem FE-06 ket qua thi FE phai filter `testType=Boundary` hoac `testType=Negative`, hoac doc tat ca roi tach tren client.
7. `testType` query o `GET list` la string enum case-insensitive. Neu FE gui gia tri sai, backend silently bo qua filter thay vi tra `400`.
8. `GET list` sort co dinh `OrderIndex asc`, khong co pagination, khong co client sort.
9. FE-06 generated `orderIndex` hien tai bat dau tu `0` va tang tuan tu qua toan bo ket qua merge. Diem nay khac voi proposal order FE-05A la 1-based.
10. Trong `TestCaseModel`, `testType`, `priority`, `request.httpMethod`, `request.bodyType`, va `variables.extractFrom` deu la string enum. Nhung nhieu field nested van la chuoi JSON da serialize, khong phai object/array typed.
11. Path mutation pipeline co the tao ca `Boundary` lan `Negative`. Mot so boundary path mutations chu dong expect `200` hoac `404`, khong phai luc nao cung la `400`.
12. Body mutation pipeline chi thuc su sinh test case cho endpoint method `POST | PUT | PATCH`. Neu suite co endpoint GET/DELETE ma FE bat flag body mutations thi engine se tra rong cho cac endpoint do.
13. Body mutation test cases van luu `request.bodyType = JSON` ngay ca khi `body = null` hoac `body = ""`.
14. LLM cache hien tai la all-or-nothing tren tap endpoint da ordered. Neu cache hit, ket qua generate van co `llmSuggestionCount > 0` nhung `llmModel` va `llmTokensUsed` trong response se la `null`.
15. Cache key cua LLM khong chi dua vao suite/spec/endpoints; no con phu thuoc feedback fingerprint. Vi vay feedback o cac flow FE-15/16 co the lam FE-06 bo cache cu va goi LLM lai.
16. Tag auto-generated duoc backend chen them theo runtime:
    - path mutations: `boundary|negative`, `auto-generated`, `rule-based`, `path-mutation`
    - body mutations: `boundary|negative`, `auto-generated`, `rule-based`, `body-mutation`
    - llm suggestions: `boundary|negative`, `auto-generated`, `llm-suggested`, cong them tag tu payload neu co

## 5. Filter, param, sort hien tai

- `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`
  - body field bat buoc runtime: `specificationId`
  - body field optional: `forceRegenerate`, `includePathMutations`, `includeBodyMutations`, `includeLlmSuggestions`
  - khong co query param
- `GET /api/test-suites/{suiteId}/test-cases`
  - query `testType` la string enum case-insensitive: `HappyPath | Boundary | Negative | Performance | Security`
  - query `includeDisabled` mac dinh `false`
  - khong co pagination
  - backend sort co dinh `OrderIndex asc`
- `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}`
  - khong co query param

## 6. Flow goi API frontend nen bam

1. Tao hoac load `TestSuite` tu FE-04.
2. Hoan tat FE-05A gate: propose/review/approve de co `AppliedOrder`.
3. Goi `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`.
4. Goi `GET /api/test-suites/{suiteId}/test-cases?testType=Boundary` va/hoac `?testType=Negative` de load danh sach vua generate.
5. Goi `GET /api/test-suites/{suiteId}/test-cases/{testCaseId}` khi can man hinh detail.

## 7. Khuyen nghi su dung

- Dung response generate nhu write-summary, sau do re-fetch list neu UI can du lieu authoritative.
- Parse cac field JSON-string trong `request` va `expectation` truoc khi dua vao editor/viewer.
- Neu UI cho bat `forceRegenerate`, can warning ro rang rang backend co the xoa boundary-negative cases cu ngay ca khi lan generate moi ket thuc voi `0` case.
- Khong suy luan `llmModel = null` la "khong dung LLM". O runtime hien tai day cung co the la cache hit.
- Neu can doi chieu hanh vi that, uu tien doc controller/command/query/service hien tai thay vi planning docs goc cua FE-06.
