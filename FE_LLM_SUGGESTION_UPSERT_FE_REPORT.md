# FE LLM Suggestion Upsert Contract Report

Tài liệu này dùng để FE kiểm tra xem đã nối đúng API `UpsertFeedback` hay chưa, gồm route, param, enum, response và các rule chặn ở backend.

---

## 1. Endpoint

`PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`

### Purpose

- Tạo mới hoặc cập nhật feedback của current user cho một LLM suggestion.
- Không phải endpoint review nội dung suggestion.
- Không materialize suggestion thành test case.
- Chỉ lưu feedback tín hiệu `Helpful` / `NotHelpful`.

---

## 2. Request

### Path params

- `suiteId` (`guid`)
- `suggestionId` (`guid`)

### Body

`UpsertLlmSuggestionFeedbackRequest`

```json
{
  "signal": "Helpful",
  "notes": "Nội dung feedback tuỳ chọn"
}
```

### Body fields

- `signal`
  - required
  - max length: `20`
  - enum hợp lệ ở backend:
    - `Helpful`
    - `NotHelpful`

- `notes`
  - optional
  - max length: `4000`

### FE cần lưu ý

- `signal` là field bắt buộc, không được để rỗng.
- FE nên render `signal` dưới dạng dropdown/radio để tránh sai enum.
- `notes` là optional, nhưng nên trim trước khi gửi.
- Nếu FE dùng serializer camelCase chuẩn thì request key nên là `signal`, `notes`.

---

## 3. Response

### Status

- `200 OK`

### Response model

`LlmSuggestionFeedbackModel`

### Confirmed fields

- `id`
- `suggestionId`
- `testSuiteId`
- `endpointId`
- `userId`
- `signal`
- `notes`
- `createdDateTime`
- `updatedDateTime`
- `rowVersion`

### FE note về response

- `signal` được trả về dạng string enum, ví dụ:
  - `Helpful`
  - `NotHelpful`
- `rowVersion` là chuỗi Base64 nếu entity có concurrency token.
- `updatedDateTime` có thể `null` nếu đây là bản ghi tạo mới lần đầu.

---

## 4. Backend validation / chặn sửa

Backend có các rule chặn rõ ràng:

### 4.1 Validate input bắt buộc

- `TestSuiteId` không được rỗng
- `SuggestionId` không được rỗng
- `CurrentUserId` không được rỗng
- `signal` phải là `Helpful` hoặc `NotHelpful`
- `notes` không vượt quá `4000` ký tự

### 4.2 Validate test suite

- Không tìm thấy suite thì trả `NotFound`
- Current user phải là chủ sở hữu của suite
- Suite đã `Archived` thì không cho feedback

### 4.3 Validate suggestion

- Không tìm thấy suggestion thì trả `NotFound`
- Không cho feedback nếu suggestion đã `Superseded`

---

## 5. FE checklist để confirm nối đúng API

### Must check

- [ ] Đã gọi đúng method `PUT`
- [ ] Đã truyền đúng path:
  - `/api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`
- [ ] Body có `signal`
- [ ] `signal` chỉ dùng `Helpful` / `NotHelpful`
- [ ] Có handle response `200 OK`
- [ ] Có parse đúng response `LlmSuggestionFeedbackModel`

### Should check

- [ ] Hiển thị lỗi khi suite không thuộc user hiện tại
- [ ] Hiển thị lỗi khi suite archived
- [ ] Hiển thị lỗi khi suggestion không tồn tại
- [ ] Hiển thị lỗi khi suggestion superseded
- [ ] `notes` không vượt quá 4000 ký tự
- [ ] Có refresh lại list/detail sau khi upsert thành công

---

## 6. Phân biệt với endpoint review

Endpoint này **không phải** review/modify suggestion.

### `UpsertFeedback`

- chỉ lưu feedback user
- payload nhỏ
- trả về feedback record

### `Review`

`PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/review`

- dùng để approve/reject/modify suggestion
- có `reviewAction`
- có `rowVersion`
- nếu `Modify` thì cần `modifiedContent`
- đây là endpoint thay đổi trạng thái suggestion

---

## 7. Kết luận ngắn

Nếu FE đang nối `UpsertFeedback`, chỉ cần bảo đảm:

- đúng route `.../feedback`
- đúng body `signal`, `notes`
- đúng enum `Helpful | NotHelpful`
- hiểu rằng response là `LlmSuggestionFeedbackModel`, không phải suggestion model

