# REPORT: Danh gia nhan dinh "negative va boundary test luc nao cung fail"

Ngay cap nhat: 2026-04-07  
Task classification: Docs only  
Repo: GSP26SE43.ModularMonolith  
Pham vi: TestGeneration + ApiDocumentation + TestExecution

---

## 1. Executive Summary

Nhan dinh "negative va boundary test luc nao cung fail" khong dung theo nghia tuyet doi, nhung dung o muc symptom runtime.

- O tang unit test, cac khoi lien quan hien khong fail hang loat.
- O tang runtime/integration, kha nang fail rat cao va co tinh he thong.
- Van de khong nam o 1 bug don le, ma nam o chuoi lech nhau giua:
  - rule/spec,
  - test-case generation,
  - request completion,
  - runtime validation.

Ket luan quan trong nhat:

- Negative test khong phai "API phai thanh cong". Negative test se PASS neu API tu choi input sai theo dung expectation.
- Boundary test cung khong dong nghia voi 200. Co boundary hop le, boundary khong hop le, va boundary co phu thuoc precondition du lieu.
- Codebase hien tai dang thuong xuyen ep negative/boundary ve mot expectation qua cung, nen runtime rat de tra ve `STATUS_CODE_MISMATCH` va `RESPONSE_SCHEMA_MISMATCH`.

---

## 2. Du lieu va bang chung da doi chieu

### 2.1. Unit-test evidence

Da chay nhom tests lien quan bang lenh:

```powershell
dotnet test 'ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj' --filter "FullyQualifiedName~BoundaryNegativeTestCaseGeneratorTests|FullyQualifiedName~GenerateBoundaryNegativeTestCasesCommandHandlerTests|FullyQualifiedName~RuleBasedValidatorTests|FullyQualifiedName~BodyMutationEngineTests"
```

Ket qua:

- `Passed: 95`
- `Failed: 0`
- `Skipped: 0`

Y nghia:

- Nhieu khoi generator/validator dang "green" o muc unit test.
- Nhung dieu nay KHONG phu dinh van de runtime, vi mot phan unit tests dang xac nhan chinh assumption qua cung hien tai.

### 2.2. Code evidence

Da doi chieu truc tiep cac diem sau:

- `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/BodyMutationEngine.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Services/PathParameterTemplateService.cs`
- `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs`
- `ClassifiedAds.Contracts/ApiDocumentation/DTOs/ApiEndpointMetadataDto.cs`
- `ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs`
- `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`
- `ClassifiedAds.Modules.TestExecution/Services/VariableResolver.cs`
- `ClassifiedAds.UnitTests/TestGeneration/BodyMutationEngineTests.cs`
- `ClassifiedAds.UnitTests/TestGeneration/BoundaryNegativeTestCaseGeneratorTests.cs`
- `ClassifiedAds.UnitTests/TestExecution/RuleBasedValidatorTests.cs`
- `docs/features/FE-12-path-parameter-templating/FE-12-03/requirement.json`
- `docs/features/FE-06-boundary-negative-generation/mutation-rules.json`

---

## 3. Danh gia nhan dinh cua user

Nhan dinh:

> "negative va boundary test luc nao cung fail het"

Danh gia chinh xac:

- Sai neu hieu theo nghia literal "tat ca moi luc deu fail" trong toan bo codebase.
- Dung neu hieu theo nghia operational symptom: "khi dua vao runtime thi ti le fail rat cao, nhin giong nhu fail het".

Muc do tin cay cua ket luan: Cao.

Ly do:

- Generator va validator unit-level dang pass.
- Nhung runtime flow dang co nhieu diem tu dong tao expectation sai, schema sai, va request khong du precondition.

---

## 4. Goc van de that su nam o dau

### 4.1. Van de 1: Expected status dang bi hard-code qua cung

Bang chung:

- `BodyMutationEngine.cs:58-127, 130-253, 255-284, 327-335`
  - rat nhieu mutation body duoc gan `ExpectedStatusCode = 400`.
- `PathParameterTemplateService.cs:239-299, 305-423, 446-529`
  - rat nhieu path mutations duoc hard-code `400`, mot so case hard-code `200`, `404`.
- `BoundaryNegativeTestCaseGenerator.cs:252-258, 296-300`
  - expectation cuoi cung duoc serialize thanh danh sach 1 gia tri duy nhat.
- `LlmScenarioSuggester.cs:420-429`
  - LLM response bi rut gon ve `FirstOrDefault() ?? 400`.
- `LlmSuggestionMaterializer.cs:69-77`
  - expectation cua LLM bi materialize thanh list chi co 1 status.

Tac dong:

- Neu backend that su tra `401`, `403`, `404`, `409`, `422` hoac `500`, test se fail du backend dang reject dung theo nghiep vu.
- Neu LLM tra ve nhieu status hop le, code hien tai danh roi toan bo ngoai gia tri dau tien.

Nhan xet quan trong:

- Day khong chi la loi implementation.
- Cac docs thiet ke cung da encode assumption cung nay:
  - `docs/features/FE-12-path-parameter-templating/FE-12-03/requirement.json:110-126`
  - `docs/features/FE-06-boundary-negative-generation/mutation-rules.json:31-35, 166-190`

Noi cach khac: bug mang tinh spec-driven simplification, khong chi la coding mistake.

### 4.2. Van de 2: Validator dung success schema de validate negative response

Bang chung:

- `RuleBasedValidator.cs:197-215`
  - neu `expectation.ResponseSchema` rong, validator fallback sang `endpointMetadata.ResponseSchemaPayloads.FirstOrDefault()`.
- `ApiEndpointMetadataService.cs:165-170, 220-221`
  - `ResponseSchemaPayloads` chi lay schema cua response `2xx`.
- `ApiEndpointMetadataDto.cs:41-46`
  - DTO comment cung noi ro day la schema cua success responses.
- `TestExecutionOrchestrator.cs:91-100, 202-205`
  - metadata duoc batch-load roi truyen vao validator cho tung case.
- `RuleBasedValidatorTests.cs:151-170`
  - test hien tai xac nhan fallback nay dang thuc su duoc dung.

Tac dong:

- Boundary/Negative response thuong la body loi, validation problem, not-found payload, unauthorized payload...
- Nhung validator lai co the dem di so body loi do voi success schema `2xx`.
- He qua truc tiep: `RESPONSE_SCHEMA_MISMATCH` xuat hien ngay ca khi status code loi la hop ly.

### 4.3. Van de 3: Request duoc generate khong dam bao baseline-valid

Bang chung:

- `BoundaryNegativeTestCaseGenerator.cs:239-247`
  - path mutation chi set 1 path param dang mutate.
- `BoundaryNegativeTestCaseGenerator.cs:285-293`
  - body mutation tao request body nhung khong dien baseline `PathParams`.
- `VariableResolver.cs:17, 149-161`
  - chi detect unresolved `{{var}}`, khong detect route token con sot lai dang `{id}`.

Tac dong:

- Endpoint co nhieu path params rat de bi bo sot token route.
- URL cuoi cung co the van chua `{param}` ma khong bi chan som.
- Runtime luc do co the tra `404`, `405`, `500`, hoac route mismatch thay vi status du kien.

### 4.4. Van de 4: Mot so boundary cases dang sai ve mat precondition du lieu

Bang chung:

- `PathParameterTemplateService.cs:344-374`
  - `boundary_max_int32` va `boundary_max_int64` dang expect `200`.
- `docs/features/FE-12-path-parameter-templating/FE-12-03/requirement.json:113-115`
  - requirement cung dang cho cac max-value cases expect `200`.

Van de:

- Gia tri `2147483647` hoac `9223372036854775807` co the parse hop le, nhung gan nhu chac chan KHONG ton tai resource tuong ung.
- Voi endpoint dang thao tac tren resource theo ID, "parse hop le" khong dong nghia "resource ton tai".
- Vi vay expectation `200` o day thuong sai ve mat precondition du lieu, khong phai chi sai ve status policy.

Tac dong:

- Case duoc goi la "boundary valid" nhung thuc te lai la "existence-dependent boundary".
- Runtime rat de tra `404`.
- User se cam thay boundary tests "fail het", trong khi that ra generator da tao mot bai test khong co du lieu de pass.

### 4.5. Van de 5: Validator danh gia strict, nen chi can lech 1 diem la fail

Bang chung:

- `RuleBasedValidator.cs:88-126`
  - status, schema, headers, body contains, body not contains, jsonpath, response time deu la cac check doc lap.
- `RuleBasedValidator.cs:125`
  - `IsPassed = result.Failures.Count == 0`.

Tac dong:

- Day khong phai bug.
- Day la behavior dung cua validator.
- Van de nam o cho expectation dau vao khong sat runtime behavior.

Noi gon:

- Validator strict la hop ly.
- Input expectation sai moi la can nguyen cua fail rate cao.

### 4.6. Van de 6: Unit tests hien tai mot phan dang "khoa cung" assumption sai

Bang chung:

- `BodyMutationEngineTests.cs:150-153`
  - test dang assert `ExpectedStatusCode.Should().Be(400)`.
- `BodyMutationEngineTests.cs:474-519`
  - co test xac nhan all mutations la `400`.
- `BoundaryNegativeTestCaseGeneratorTests.cs:205-211, 241-243, 277-285, 384-386`
  - nhieu fixture test cung dung status mac dinh `400`.

Tac dong:

- Unit tests dang pass vi chung xac nhan implementation hien tai.
- Chung khong prove rang runtime expectation hien tai la dung.
- Muon sua dung, can sua ca production code lan tests.

---

## 5. Root-cause chain tong hop

1. Requirement va mutation rules don gian hoa expected status thanh `200/400/404`.
2. Generator persist expectation duoi dang 1 status duy nhat cho nhieu case.
3. LLM pipeline neu co nhieu allowed statuses cung bi rut gon ve 1 status.
4. Mot so generated requests khong du baseline path params.
5. Validator fallback success schema cho negative responses.
6. Validator strict danh fail ngay khi co 1 mismatch.
7. User quan sat tren runtime thay boundary/negative fail hang loat.

---

## 6. Vi sao "unit pass" nhung "runtime van fail"

Day la diem de gay hieu nham nhat.

- Unit tests chu yeu verify:
  - mutation co duoc tao ra khong,
  - tags/test type co dung khong,
  - expectation co serialize dung shape khong,
  - fallback schema co chay khong.
- Unit tests khong verify day du:
  - endpoint that su tra ma nao cho tung mutation family,
  - error payload thuc te co match success schema hay khong,
  - resource precondition co ton tai hay khong,
  - URL cuoi cung co bi route token sot lai hay khong.

Vi vay:

- "unit xanh" khong dong nghia "negative/boundary runtime dung".
- Day la classic case cua "implementation-conformant but behaviorally misaligned".

---

## 7. Solution de xuat

## Phase 0 - Fix nhanh, impact cao

1. Chan schema fallback cho non-2xx cases
- Sua `RuleBasedValidator`.
- Neu expectation khong khai bao `ResponseSchema` va expected status khong co `2xx`, thi skip schema validation.
- Khong duoc fallback sang `ResponseSchemaPayloads` cua endpoint cho negative/boundary error response.

2. Hoan thien baseline path params cho generated requests
- Sua `BoundaryNegativeTestCaseGenerator`.
- Path mutation:
  - dien day du tat ca path params bang default/example hop le,
  - chi mutate 1 param dang xet.
- Body mutation:
  - neu endpoint co path params, van phai set baseline `PathParams`.

3. Khong de route token sot lai
- Them unit test cho truong hop endpoint co nhieu path params.
- Can nhac them check som de fail fast neu URL van con token `{param}` sau khi resolve.

## Phase 1 - Centralize expected-status policy

1. Tao 1 policy/service tap trung o `ClassifiedAds.Modules.TestGeneration`
- Vi du: `BoundaryNegativeExpectationPolicy`.
- Muc tieu:
  - khong hard-code status o nhieu file rieng le,
  - mapping theo `mutationType`, `SuggestedTestType`, `httpMethod`, `dataType`, `format`,
  - co kha nang tra ve NHIEU allowed statuses.

2. Khong fix bang cach mo rong catch-all vo toi va
- KHONG nen dung `[400,401,403,404,409,422,500]` cho moi case.
- Neu lam vay thi fail rate giam nhung gia tri phat hien bug cung giam theo.

3. Mapping khuyen nghi
- `missingRequired`, `typeMismatch`, `malformedJson`, `invalidEnum`:
  - uu tien `[400, 422]`
- `nonExistent`:
  - uu tien `[404]`
- auth negative:
  - uu tien `[401, 403]`
- overflow/body length boundary:
  - tuy endpoint, uu tien `[400, 413, 422]`

## Phase 2 - Xu ly dung cac boundary co phu thuoc du lieu

1. Khong auto-expect `200` cho path ID max-value neu khong co setup du lieu
- `boundary_max_int32`
- `boundary_max_int64`
- `boundary_zero` cho mot so resource-like numeric IDs

2. Lua chon thuc dung
- Cach A: bo khong generate cac case "valid boundary but resource existence unknown"
- Cach B: van generate, nhung expected status phai theo precondition data co that
- Cach C: chi generate cac case nay khi gia tri duoc lay tu variable/extracted response truoc do

Khuyen nghi:

- Giai phap an toan nhat la Cach A hoac Cach C.
- Khong nen tiep tuc gan `200` cho resource-ID boundary khong co data setup.

## Phase 3 - Giu lai day du expected statuses cua LLM

1. Sua model `LlmSuggestedScenario`
- Khong chi giu `ExpectedStatusCode` dang `int`.
- Nen giu `ExpectedStatusCodes` dang list neu n8n/LLM tra ve duoc.

2. Sua parse/materialize flow
- `LlmScenarioSuggester` khong duoc lay chi `FirstOrDefault()`.
- `LlmSuggestionMaterializer` phai truyen full list allowed statuses vao expectation.

Giai phap toi thieu neu muon giam scope:

- Neu chua mo rong model ngay, it nhat fallback phai thong minh hon `?? 400`.

## Phase 4 - Sua tests de khoa dung behavior moi

Bat buoc them hoac cap nhat tests:

1. `RuleBasedValidatorTests`
- expected non-2xx + no schema -> KHONG fallback success schema
- expected 2xx + no schema -> duoc phep fallback

2. `BoundaryNegativeTestCaseGeneratorTests`
- endpoint co nhieu path params -> generated request co du params
- body mutation tren endpoint co path params -> khong sot route token
- expectation policy tra ve list statuses, khong chi 1 status

3. `BodyMutationEngineTests`
- bo assumption "all mutations must be 400"
- chuyen sang assert theo mutation family/policy

4. `LlmSuggestionMaterializerTests`
- giu duoc multiple expected statuses neu source co cung cap

5. Integration-like test cho runtime validator
- case response `422` cua `missingRequired` phai PASS neu expected list co `422`
- case error payload khac success schema phai KHONG bi `RESPONSE_SCHEMA_MISMATCH` khi schema expectation rong

---

## 8. Thu tu implement khuyen nghi

Thu tu an toan nhat:

1. `RuleBasedValidator` fix schema fallback
2. `BoundaryNegativeTestCaseGenerator` fix baseline path/body params
3. Centralize expected-status policy
4. Preserve multi-status cho LLM pipeline
5. Update unit tests
6. Chay lai nhom tests lien quan

Ly do:

- Buoc 1 va 2 giam fail rate nhanh nhat ma risk nho nhat.
- Buoc 3 va 4 moi la fix can co chieu sau de he thong dung ve dai han.

---

## 9. Acceptance Criteria cho ban fix

1. Boundary/Negative khong con fail hang loat vi success-schema fallback.
2. Generated requests khong con de sot route token `{param}`.
3. Expected statuses khong bi ep ve 1 gia tri mac dinh vo ly cho nhieu mutation families.
4. LLM suggestions khong bi mat allowed-statuses khi parse/materialize.
5. Nhung case runtime tra `422`, `401`, `403`, `404`, `409` dung nghiep vu co the PASS neu expectation cho phep.
6. Happy-path validator semantics khong bi noi long ngoai y muon.
7. Module boundaries va pattern hien co cua codebase van duoc giu nguyen.

---

## 10. Dieu KHONG nen lam

- Khong sua bang cach tat strict validation toan cuc.
- Khong sua bang cach bo schema validation cho happy-path.
- Khong sua bang cach mo rong 1 giant catch-all status list cho moi negative/boundary case.
- Khong sua bang find-and-replace status `400` thanh `422`.
- Khong them cross-module shortcut pha vo boundaries giua `TestGeneration`, `ApiDocumentation`, `TestExecution`.

---

## 11. Ket luan cuoi cung

Nhan dinh "negative va boundary test luc nao cung fail" khong dung theo nghia tuyet doi, nhung rat co co so neu nhin tu goc runtime.

Nguyen nhan goc khong phai do negative/boundary test la sai ve y tuong, ma do codebase hien tai:

- encode expectation qua cung,
- danh roi multi-status intent,
- dung success schema cho error response,
- va generate mot so case boundary ma khong co du lieu/precondition de pass.

Neu fix dung thu tu de xuat o tren, ti le fail se giam manh ma van giu duoc do strict va gia tri phat hien bug cua he thong.

---

## 12. Mot cau Prompt cho AI Agent implement report nay

"Trong `D:\\GSP26SE43.ModularMonolith`, hay implement cac action items trong `NEGATIVE-BOUNDARY-FAILURE-ANALYSIS-REPORT.md` theo `AGENTS.md`: classify task as `Application code only`, dung `GitNexus` neu co, uu tien sua `RuleBasedValidator` de bo success-schema fallback cho non-2xx khi expectation schema rong, sua `BoundaryNegativeTestCaseGenerator` de always dien baseline path/body params va khong de sot route token, centralize boundary/negative expected-status policy thay cho hard-code roi rac, preserve multiple allowed statuses trong LLM suggestion flow neu source co tra ve, giu nguyen module boundaries/pattern hien co, cap nhat unit tests de chung minh runtime mismatch giam ma khong gay regression, va neu scope mo rong sang EF/Docker thi phai thuc hien day du cac gates bat buoc cua repo."
