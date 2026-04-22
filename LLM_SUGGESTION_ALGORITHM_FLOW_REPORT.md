# LLM Suggestion Algorithm Flow Report

Tài liệu này mô tả thuật toán tạo LLM suggestion trong dự án, nó được gọi ở đâu, chạy qua những bước nào, và dữ liệu đi như thế nào từ input đến output.

---

## 1. Mục tiêu của thuật toán

Thuật toán LLM suggestion trong dự án được dùng để:

- phân tích API spec / endpoint metadata
- sinh ra các suggestion cho test case
- cho phép người dùng review, modify, approve, reject
- sau khi approved thì materialize thành test case thật để dùng ở runtime execution

Nói ngắn gọn:

**Spec / Endpoint metadata → LLM suggestion → Human review → Test case materialization**

---

## 2. Thuật toán được gọi ở đâu

### 2.1 Controller entry point

Luồng bắt đầu từ controller:

- `ClassifiedAds.Modules.TestGeneration.Controllers.LlmSuggestionsController`

Các endpoint liên quan:

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`
  - tạo preview suggestions
- `GET /api/test-suites/{suiteId}/llm-suggestions`
  - list suggestions
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`
  - lấy chi tiết suggestion
- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`
  - review / modify / approve / reject suggestion
- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`
  - upsert feedback helpful / not helpful
- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-review`
- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-delete`
- `POST /api/test-suites/{suiteId}/llm-suggestions/bulk-restore`

### 2.2 Command / service layer

Các command handler liên quan:

- `GenerateLlmSuggestionPreviewCommandHandler`
- `ReviewLlmSuggestionCommandHandler`
- `BulkReviewLlmSuggestionsCommandHandler`
- `UpsertLlmSuggestionFeedbackCommandHandler`

Các service liên quan:

- `ILlmSuggestionReviewService`
- `ILlmSuggestionFeedbackUpsertService`
- `ILlmSuggestionGenerator` hoặc service tương đương trong generation pipeline

---

## 3. Flow tổng thể của thuật toán

### Step 1. FE gọi generate preview

FE gọi:

- `POST /api/test-suites/{suiteId}/llm-suggestions/generate`

Backend nhận request `GenerateLlmSuggestionPreviewRequest` và tạo command:

- `GenerateLlmSuggestionPreviewCommand`

Sau đó pipeline generation được chạy để tạo ra suggestion preview.

### Step 2. Backend phân tích spec và endpoint metadata

Thuật toán thường dựa trên:

- API spec của test suite
- endpoint metadata
- business context / generation profile
- rules của project

Mục tiêu là tạo ra các suggestion có cấu trúc như:

- `suggestedName`
- `suggestedDescription`
- `suggestedRequest`
- `suggestedExpectation`
- `suggestedVariables`
- `suggestedTags`
- `priority`
- `testType`
- `suggestionType`

### Step 3. Persist suggestion preview

Suggestion được lưu như record pending để người dùng review sau.

Thường các field quan trọng lúc này là:

- `reviewStatus = Pending`
- `rowVersion`
- `createdDateTime`
- `updatedDateTime`
- `llmModel`
- `tokensUsed`

### Step 4. User review suggestion

User gọi:

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`

Với `reviewAction` là:

- `Approve`
- `Reject`
- `Modify`

Nếu `Modify`, user gửi thêm `modifiedContent`.

### Step 5. Materialize thành test case khi approve/modify

Đây là bước quan trọng nhất.

Trong `ReviewLlmSuggestionCommandHandler`:

- nếu `Approve` → review service sẽ approve suggestion
- nếu `Modify` → modified content được truyền xuống review service
- nếu `Reject` → chỉ cập nhật trạng thái reject, không materialize

Vì vậy, **modify không lưu trực tiếp thành test case ngay ở controller**, mà được chuyển xuống service review để tạo output cuối cùng.

### Step 6. Xuất feedback riêng nếu user đánh giá suggestion

Endpoint feedback:

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`

Chỉ lưu tín hiệu:

- `Helpful`
- `NotHelpful`

Endpoint này **không phải** nơi materialize test case.

---

## 4. Luồng chi tiết của Review / Modify

### 4.1 Validate request

Trong `ReviewLlmSuggestionCommandHandler`:

- `SuggestionId` bắt buộc
- `TestSuiteId` bắt buộc
- `CurrentUserId` bắt buộc
- `RowVersion` bắt buộc
- `ReviewAction` phải là `Approve`, `Reject`, hoặc `Modify`

Nếu `Reject`:

- `ReviewNotes` bắt buộc

Nếu `Modify`:

- `ModifiedContent` bắt buộc

### 4.2 Check ownership

Backend kiểm tra:

- user hiện tại phải là chủ sở hữu của test suite

### 4.3 Check current suggestion state

Backend chỉ cho review khi suggestion còn ở trạng thái:

- `Pending`

Nếu suggestion đã ở trạng thái khác, request review sẽ bị chặn.

Ngoại lệ:

- `Approve` có idempotent path nếu suggestion đã `Approved` / `ModifiedAndApproved` và đã có `AppliedTestCaseId`

### 4.4 Dispatch review service

Sau khi pass validation:

- `Approve` / `Modify` → gọi `ApproveManyAsync(...)`
- `Reject` → gọi `RejectAsync(...)`

Ở nhánh approve/modify, `ModifiedContent` được truyền xuống service để phục vụ materialization.

---

## 5. `ModifiedContent` được dùng như thế nào

`ModifiedContent` là input phục vụ nhánh `Modify`.

Nó được tạo từ:

- `EditableLlmSuggestionInput`

và được truyền vào:

- `LlmSuggestionApprovalItem.ModifiedContent`
- `ILlmSuggestionReviewService.ApproveManyAsync(...)`

Ý nghĩa:

- user không chỉ approve suggestion gốc
- mà có thể chỉnh lại nội dung trước khi materialize thành test case
- backend sẽ dùng modified content thay cho suggestion gốc trong quá trình tạo test case

---

## 6. Feedback flow khác review flow

Endpoint feedback:

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`

Command handler:

- `UpsertLlmSuggestionFeedbackCommandHandler`

Validation thêm:

- suite không được archived
- suggestion không được superseded
- suite phải thuộc current user

Đây chỉ là feedback record:

- `Signal = Helpful | NotHelpful`
- `Notes`

Không tạo test case mới.

---

## 7. Cụm thuật toán chính trong dự án

### A. Generation algorithm

Chạy khi generate preview.

Input:

- suite
- spec
- endpoint metadata
- generation profile
- business context

Output:

- list suggestion preview

### B. Review algorithm

Chạy khi review suggestion.

Input:

- suggestion
- review action
- row version
- modified content
- current user

Output:

- trạng thái suggestion được cập nhật
- nếu approve/modify thì tạo materialized test case

### C. Feedback algorithm

Chạy khi user đánh giá suggestion.

Input:

- signal
- notes
- suiteId
- suggestionId

Output:

- feedback entity được upsert

---

## 8. Dữ liệu đi qua các lớp nào

### Flow điển hình

1. `LlmSuggestionsController`
2. `GenerateLlmSuggestionPreviewCommand` hoặc `ReviewLlmSuggestionCommand`
3. command handler
4. domain/service layer
5. repository / unit of work
6. response model trả về FE

### Ví dụ với review

- controller nhận request
- command handler validate
- review service thực thi business logic
- repository lưu suggestion/test case
- trả về `LlmSuggestionModel`

---

## 9. Trạng thái suggestion quan trọng

Các trạng thái review hiện thấy trong backend:

- `Pending`
- `Approved`
- `Rejected`
- `ModifiedAndApproved`
- `Superseded`

Ý nghĩa ngắn:

- `Pending`: chờ review
- `Approved`: duyệt nguyên bản
- `Rejected`: từ chối
- `ModifiedAndApproved`: đã chỉnh rồi duyệt
- `Superseded`: bị thay thế bởi suggestion khác

---

## 10. Tóm tắt dễ hiểu cho người đọc nhanh

Nếu viết ngắn gọn theo pipeline:

- FE gọi generate preview
- backend sinh LLM suggestions
- user review / modify / reject
- nếu modify thì content chỉnh được truyền vào review service
- review service materialize thành test case khi approve
- feedback là luồng riêng, chỉ ghi nhận helpful / not helpful

---

## 11. Kết luận

Thuật toán LLM suggestion trong dự án là một pipeline nhiều bước, không phải một hàm đơn lẻ.

Nó được gọi từ controller `LlmSuggestionsController`, đi qua command handler và service review/generation, sau đó mới đến repository để lưu dữ liệu.

Quan trọng nhất:

- `GeneratePreview` tạo suggestion
- `Review` / `Modify` quyết định suggestion có được materialize thành test case hay không
- `UpsertFeedback` chỉ ghi feedback, không materialize

