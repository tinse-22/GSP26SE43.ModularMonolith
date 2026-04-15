# PHAN TICH GOC RE VAN DE PASS/FAIL CHO NEGATIVE TEST CASE

## 1) Tom tat nhanh

Van de ban gap la dung va co the tai hien duoc tu code hien tai:

- Happy case: expected 2xx, actual 2xx -> PASS.
- Negative case: expected thuong bi set qua hep (vd chi [400]), actual co the la 422/415/401/403 tuy endpoint -> bi danh FAIL.

Diem quan trong:

- Logic cham PASS/FAIL trong execution KHONG sai ve nguyen tac.
- Goc re nam o quality cua du lieu expectation va mat du lieu expectation list o mot so luong pipeline generate.

---

## 2) Trace tu code de thay vi sao bi FAIL oan

### 2.1 Execution engine cham theo expected vs actual (dung)

- `RuleBasedValidator.ValidateStatusCode` parse `ExpectedStatus` thanh danh sach va check `Contains`.
- Neu actual status khong nam trong danh sach expected thi them `STATUS_CODE_MISMATCH`.
- Cuoi cung `IsPassed = result.Failures.Count == 0`.

Bang chung:

- `ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs`:
  - dong 125: `IsPassed = result.Failures.Count == 0;`
  - dong 141-153: parse expected status list va so sanh `Contains`
  - dong 158: tao failure `STATUS_CODE_MISMATCH`

Ket luan: execution layer dang lam dung theo expectation duoc cap.

### 2.2 Rule-based body mutation tao expected qua cung

- `BodyMutationEngine` gan `ExpectedStatusCode = 400` cho rat nhieu mutation body.
- Khi API tra 422 (rat pho bien voi validation), test se FAIL vi expected chi co [400].

Bang chung:

- `ClassifiedAds.Modules.TestGeneration/Services/BodyMutationEngine.cs`
  - nhieu vi tri (66, 77, 88, 102, 113, 124, 150, 189, 249, 279, 333) deu hard-code `ExpectedStatusCode = 400`.
- `ClassifiedAds.UnitTests/TestGeneration/BodyMutationEngineTests.cs`
  - dong 474+: test hien tai con khang dinh tat ca mutation phai la 400.

### 2.3 LLM co ho tro expected status list, nhung preview pipeline lam mat list

Data model da ho tro multi-status:

- `N8nTestCaseExpectation.ExpectedStatus` la `List<int>`.
- `LlmSuggestedScenario` co ca:
  - `ExpectedStatusCode` (primary, backward-compat)
  - `ExpectedStatusCodes` (full list)
  - `GetEffectiveExpectedStatusCodes()`.

Nhung khi persist suggestion preview:

- `GenerateLlmSuggestionPreviewCommandHandler` hien dang luu:
  - `ExpectedStatus = new List<int> { scenario.ExpectedStatusCode }`
- Nghia la list expected tu LLM (vd [400, 422]) bi cat con [400] ngay luc luu DB.

Bang chung:

- `ClassifiedAds.Modules.TestGeneration/Services/ILlmScenarioSuggester.cs`: ho tro full list.
- `ClassifiedAds.Modules.TestGeneration/Services/LlmSuggestionMaterializer.cs` dong 72: dung `GetEffectiveExpectedStatusCodes()` (dung).
- `ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs` dong 191: chi luu 1 status (`scenario.ExpectedStatusCode`).

=> Day la diem mat du lieu quan trong tao ra FAIL oan o luong LLM preview.

---

## 3) Root-cause tree (goc re that su)

### Root cause A (chinh): Expectation generation policy qua hep

- Rule-based body mutations default 400 cho nhieu tinh huong ma backend co the tra 422/415/...
- Expected set khong phan anh dung "accepted error envelope" cua endpoint.

### Root cause B (chinh): Mat du lieu expected status list trong luong LLM preview

- LLM / model co the de xuat nhieu status hop le.
- Pipeline preview luu lai chi 1 status -> he thong execution nhan du lieu sai ngay tu dau.

### Root cause C (gop phan): Unit tests dang khoa cung hanh vi sai

- Test khang dinh body mutations deu phai 400.
- Chua co test bao ve truong hop "LLM expected list phai duoc persist nguyen ven".

### Khong phai root cause

- `RuleBasedValidator` khong phai tac nhan chinh.
- Validator da co test cho multi-status PASS (`[200, 201]`).

---

## 4) Best solution de giai quyet dung goc

## Muc tieu thiet ke

- PASS/FAIL phai dua tren "expected status set" dung nghia vu.
- Khong duoc mat expected status list tren duong generate -> preview -> save -> execute.

### Phase 0 - Quick win (nen lam ngay)

1. Sua mat du lieu o preview pipeline:
   - Thay trong `GenerateLlmSuggestionPreviewCommand`:
   - Tu: `ExpectedStatus = new List<int> { scenario.ExpectedStatusCode }`
   - Thanh: `ExpectedStatus = scenario.GetEffectiveExpectedStatusCodes()`

2. Mo rong expected statuses cho body mutation muc toi thieu:
   - `missingRequired`, `typeMismatch`, `invalidEnum`, `overflow`: `[400, 422]`
   - `emptyBody`: `[400, 415, 422]` (tu stack co the tra khac nhau)
   - `malformedJson`: `[400]` (giu chat)

3. Backfill nhanh du lieu da sinh (neu can):
   - Cac suggestion/testcase pending co expectedStatus `[400]` va tag negative/body-mutation
   - Chuyen thanh `[400,422]` theo mapping an toan da thong nhat.

### Phase 1 - Solution ben vung (best long-term)

1. Tao policy trung tam:
   - `IExpectedStatusPolicy.GetAllowedStatuses(context)`
   - Context gom: endpoint metadata, mutation type, auth context, content-type

2. Chuan hoa model:
   - Body mutation va scenario deu lam viec voi danh sach status (list-first)
   - `ExpectedStatusCode` chi con de backward compatibility va danh dau deprecated

3. Tat ca generator (path/body/llm/callback) deu serialize full list vao `ExpectedStatus`.

### Phase 2 - Guardrails va test hardening

1. Them unit test bat buoc:
   - Preview command phai persist du full expected list tu scenario.
   - Body mutation khong duoc all-400 nua; test theo mapping policy.
   - E2E: expected `[400,422]`, actual 422 -> PASS.

2. Them observability:
   - Log structured: expected statuses, actual status, source (rule-based/llm/manual).
   - Dashboard dem `STATUS_CODE_MISMATCH` theo mutation type de phat hien policy sai.

---

## 5) Mapping de xuat ban dau (co the tune theo endpoint)

| Mutation type | Allowed status de xuat |
|---|---|
| missingRequired | [400, 422] |
| typeMismatch | [400, 422] |
| invalidEnum | [400, 422] |
| overflow | [400, 422] |
| emptyBody | [400, 415, 422] |
| malformedJson | [400] |
| nonExistent resource | [404] |
| auth-related negative | [401, 403] |

Nguyen tac:

- Khong mo rong qua da (tranh false pass).
- Chi mo rong khi co can cu tu contract/OpenAPI/thuc te runtime.

---

## 6) Tieu chi nghiem thu sau khi fix

1. Negative test expected fail:
   - Expected `[400,422]`, actual 422 -> PASS.
2. Negative test expected fail nhung API thanh cong:
   - Expected `[400,422]`, actual 200 -> FAIL.
3. LLM scenario co expected list:
   - Persist xuong DB giu nguyen list, khong bi rut gon.
4. Regression:
   - Happy path va strict status matching van dung nhu cu.

---

## 7) Ket luan

Van de ban gap KHONG phai do he thong hieu sai khai niem "negative test".
Van de nam o 2 diem goc:

1. Expected status duoc sinh ra qua hep (dac biet body mutation hard-code 400).
2. Luong LLM preview lam mat expected status list khi luu.

Neu sua dung 2 diem nay + cap nhat test guardrails, hien tuong
"test case fail ma API fail dung van bi danh fail"
se duoc xu ly dung goc va on dinh lau dai.
