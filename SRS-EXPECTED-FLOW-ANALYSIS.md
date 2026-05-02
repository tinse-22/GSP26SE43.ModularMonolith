# SRS-Expected Flow Analysis

## Muc tieu
Xay dung luong de lay SRS lam nguon Expected khi co SRS, thay vi chi phu thuoc vao LLM/heuristic. Bao cao nay mo ta hien trang, khoang trong, va huong de dua Expected dua tren SRS vao qua trinh tao test va validate luc chay.

## Hien trang (as-is)
- SRS da co luong nhap/phan tich va luu: [ClassifiedAds.Modules.TestGeneration/Entities/SrsDocument.cs](ClassifiedAds.Modules.TestGeneration/Entities/SrsDocument.cs), [ClassifiedAds.Modules.TestGeneration/Entities/SrsRequirement.cs](ClassifiedAds.Modules.TestGeneration/Entities/SrsRequirement.cs).
- SRS duoc dua vao LLM context khi tao suggestion (co SRS -> LLM nhan constraints): [ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs](ClassifiedAds.Modules.TestGeneration/Commands/GenerateLlmSuggestionPreviewCommand.cs), [ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs](ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs).
- Traceability giua requirement va test case da co link: [ClassifiedAds.Modules.TestGeneration/Entities/TestCaseRequirementLink.cs](ClassifiedAds.Modules.TestGeneration/Entities/TestCaseRequirementLink.cs), [ClassifiedAds.Modules.TestGeneration/Queries/GetSrsTraceabilityQuery.cs](ClassifiedAds.Modules.TestGeneration/Queries/GetSrsTraceabilityQuery.cs).
- Expected khi chay test hien nay lay tu TestCaseExpectation (nguon LLM/Manual), validate boi RuleBasedValidator: [ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs](ClassifiedAds.Modules.TestExecution/Services/TestExecutionOrchestrator.cs), [ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs](ClassifiedAds.Modules.TestExecution/Services/RuleBasedValidator.cs).
- TestCaseExpectation duoc build tu N8n/LLM expectation: [ClassifiedAds.Modules.TestGeneration/Services/TestCaseExpectationBuilder.cs](ClassifiedAds.Modules.TestGeneration/Services/TestCaseExpectationBuilder.cs).

## Khoang trong can xu ly
1) SRS chi la context cho LLM tao expectation, khong phai nguon Expected mang tinh rang buoc.
2) Constraint trong SRS la chuoi tu do, chua co schema cau truc hoa de map thanh Expected.
3) Khi co SRS, khong co co che uu tien SRS de override expectation LLM tai runtime.
4) Traceability co, nhung chua du du lieu de suy ra Expectation chinh xac theo requirement.

## Huong de dua SRS thanh nguon Expected (to-be)

### A. Chuan hoa du lieu SRS thanh constraint co cau truc
- Tien de: can schema constraint day du de map sang expected.
- De xuat mo hinh constraint moi (SrsTestConstraint) luu trong SrsRequirement.TestableConstraints/RefinedConstraints:
  - requirementCode
  - endpointId
  - testType (HappyPath/Boundary/Negative)
  - expectedStatusCodes
  - bodyContains
  - jsonPathChecks
  - preconditions (neu can)
  - priority
  - rationale
- Cap nhat pipeline refine de LLM tra ve dung schema (khong free-text).
- Gating: chi dung requirement da reviewed (IsReviewed = true) lam nguon Expected.

### B. Generation flow: tao test case voi Expected tu SRS (SRS-first)
1) Khi test suite co SRS (SrsDocumentId):
   - Lay requirements da reviewed.
   - Parse constraint structure (RefinedConstraints uu tien, fallback TestableConstraints).
2) Map constraint -> TestCaseExpectation:
   - expectedStatus = constraint.expectedStatusCodes
   - bodyContains/jsonPathChecks tu constraint
   - uu tien constraint thay vi LLM expectation
3) Luu metadata nguon Expected (SRS/LLM/Manual) de audit va trace.
4) Tao TestCaseRequirementLink tu constraint -> test case.

### C. Execution flow: uu tien Expected tu SRS khi co
- Khi chay test:
  - Neu Expectation co Source = SRS va day du du lieu => validate theo SRS.
  - Neu SRS constraint thieu hoac chua reviewed => fallback Expected hien tai (LLM/Manual).
- Co the them ValidationProfile moi (vd: SrsStrict) de bat/tat override.

### D. Heuristic gop Expected (neu can)
- Neu SRS va LLM khac nhau:
  - Default: SRS override LLM.
  - Hoac: Merge voi nguyen tac:
    - expectedStatus = intersection neu SRS co, else union
    - jsonPathChecks/bodyContains: lay SRS, neu SRS thieu thi lay LLM

## Thay doi du kien (data model + service)

### 1) Data model
- Mo rong SrsRequirement.TestableConstraints/RefinedConstraints sang schema co struct (SrsTestConstraint).
- Them truong trong TestCaseExpectation hoac TestCase de ghi nguon Expected:
  - ExpectationSource (enum): Srs/LLM/Manual
  - PrimaryRequirementId (da co) co the tiep tuc su dung cho traceability.

### 2) Generation layer
- Trong luong tao suggestion/test case:
  - Khi co SRS va constraint reviewed, dung SRS constraint de build expectation.
  - Luu constraint id/code vao TestCaseRequirementLink.

### 3) Execution layer
- RuleBasedValidator tiep tuc dung Expected tu TestCaseExpectation, nhung gio Expected da co the la SRS.
- Neu can override luc chay, them hook trong orchestration hoac expectation builder.

## Tieu chi dung/khong dung SRS lam Expected
- Dung SRS neu:
  - SrsDocument.AnalysisStatus = Completed
  - Requirement IsReviewed = true
  - Constraint da du truong (expectedStatus + it nhat 1 assertion)
- Khong dung SRS neu:
  - Chua reviewed / constraint thieu
  - Requirement khong map duoc endpoint

## Test plan (goi y)
- Unit tests cho:
  - Parser constraint tu RefinedConstraints
  - Mapping constraint -> TestCaseExpectation
  - Quy tac uu tien SRS/LLM
- Integration tests:
  - Full flow tao test tu SRS -> run -> validate pass/fail dung
  - Fallback LLM khi SRS thieu

## Rủi ro va giam thieu
- Rủi ro: constraint SRS bi sai/khong day du -> Expected sai.
  - Mitigation: bat buoc reviewed; thong bao confidence; log source.
- Rủi ro: mâu thuan voi OpenAPI schema
  - Mitigation: validate constraint voi swagger response schema (warning).

## De xuat rollout
1) Them schema constraint va refine pipeline.
2) Bao cao/preview expectation tu SRS o UI.
3) Bat SrsStrict theo feature flag.
4) Theo doi fail rate va canh bao mismatch.

## Ket luan
Muon su dung SRS lam nguon Expected, can chuyen constraint sang schema co cau truc va luong tao expectation SRS-first. Execution se su dung Expected duoc luu san (source = SRS), thay vi lay truc tiep tu LLM. Traceability da san co, chi can mo rong data va them policy uu tien.
