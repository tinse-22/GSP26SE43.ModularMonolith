# FE-17 LLM Suggestion Workflow Report

Ngay cap nhat: 2026-04-06
Nguon xac minh: Backend code + tai lieu docs trong workspace D:/GSP26SE43.ModularMonolith

## 1) Ket luan nhanh

- Luong Suggestion + Review/Approve dang la luong duoc thiet ke ro cho FE-15/FE-17.
- Khi Approve (single hoac bulk), backend se materialize suggestion thanh TestCase that.
- Luong Generate HappyPath va Generate Boundary/Negative la luong tao truc tiep test case, khong qua buoc review suggestion.
- Workspace hien tai khong chua source FE .ts/.tsx, nen cac file FE ban neu (testSuiteLlmSuggestionService.ts, TestSuiteDetailPage.tsx, GeneratingTestCasesPage.tsx) khong the doi chieu truc tiep trong repo nay.

## 2) Bang chung backend cho luong Suggestion -> Approve -> TestCase

### 2.1 Surface API review suggestion

LlmSuggestionsController xac dinh ro day la workflow FE-15 suggestion review:
- Route goc: api/test-suites/{suiteId}/llm-suggestions
- Single review: PUT {suggestionId}/review
- Bulk review: POST bulk-review

### 2.2 Suggestion duoc persist o trang thai Pending

Generate preview tao row LlmSuggestion va gan ReviewStatus = Pending, chua tao TestCase ngay.

### 2.3 Approve se materialize thanh TestCase that

LlmSuggestionReviewService.ApproveManyAsync:
- Materialize tu suggestion hoac modified content
- Ghi TestCase + Request + Expectation + Variables
- Gan AppliedTestCaseId vao suggestion
- Cap nhat review status (Approved hoac ModifiedAndApproved)

Dieu nay xac nhan: Approve trong luong suggestion la diem chuyen tu preview sang TestCase that.

## 3) Bang chung backend cho luong generate truc tiep

### 3.1 HappyPath direct generate

GenerateHappyPathTestCasesCommand:
- Goi generator truc tiep
- Persist truc tiep TestCase + Request + Expectation (+ dependencies neu co)
- Khong qua buoc review suggestion

### 3.2 Boundary/Negative direct generate

GenerateBoundaryNegativeTestCasesCommand:
- Cho phep bat/tat cac nguon PathMutations, BodyMutations, LlmSuggestions
- Goi generator truc tiep
- Persist truc tiep TestCase + Request + Expectation (+ dependencies)
- Khong qua buoc review suggestion

## 4) Rule dung luong nao khi nao

### Dung Suggestion + Approve khi:
- Can nguoi dung review tung case truoc khi tao TestCase that.
- Can sua noi dung (Modify) truoc khi approve.
- Can luong bulk approve/reject theo filter cho danh sach Pending.
- Muon kiem soat chat luong va giam rui ro case xau truoc khi materialize.

### Dung Generate HappyPath hoac Boundary/Negative truc tiep khi:
- Can tao nhanh so luong lon.
- Chap nhan it buoc kiem duyet hon.
- Muon pipeline tao va ghi test cases ngay trong 1 lan goi.

## 5) Ve nhan dinh "FE hien tai dang nghieng ve Suggestion workflow"

Nhan dinh nay phu hop voi:
- Cac docs frontend FE-15/FE-17 trong repo dang tap trung vao llm-suggestions va review/bulk-review.
- Controller backend cung duoc to chuc ro thanh surface llm-suggestions rieng cho review workflow.

Dong thoi, luong direct generate van ton tai ro rang tren TestCasesController (generate-happy-path, generate-boundary-negative), nen viec "con dau vet flow cu generate truc tiep" la hop ly o goc nhin architecture tong the.

## 6) Khoang trong xac minh trong workspace nay

Khong tim thay cac file FE sau trong workspace hien tai:
- testSuiteLlmSuggestionService.ts
- TestSuiteDetailPage.tsx
- GeneratingTestCasesPage.tsx

Vi vay, phan "Step 2 review nam o TestSuiteDetailPage.tsx" va "dau vet flow cu o GeneratingTestCasesPage.tsx" duoc ghi nhan theo thong tin ban cung cap, chua doi chieu truc tiep bang source FE trong repo nay.

## 7) Mapping goi FE -> BE de doi chieu nhanh

- FE review single suggestion -> PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review -> ReviewLlmSuggestionCommand -> LlmSuggestionReviewService.ApproveManyAsync hoac RejectAsync
- FE bulk review suggestion -> POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review -> BulkReviewLlmSuggestionsCommand -> LlmSuggestionReviewService.ApproveManyAsync hoac RejectManyAsync
- FE generate happy-path truc tiep -> POST /api/test-suites/{suiteId}/test-cases/generate-happy-path -> GenerateHappyPathTestCasesCommand
- FE generate boundary/negative truc tiep -> POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative -> GenerateBoundaryNegativeTestCasesCommand

## 8) Recommendation cho FE team

- Neu man hinh chinh la review quality truoc khi tao test case: keep Suggestion workflow as default.
- Neu can quick generation (POC, mass seeding, speed mode): expose direct generate nhu fast path co canh bao ro.
- Neu cung ton tai 2 luong trong UI, nen dat label va UX states ro rang:
  - Suggestion mode: Preview -> Review -> Approve -> Materialize
  - Direct mode: Generate -> Persist ngay
