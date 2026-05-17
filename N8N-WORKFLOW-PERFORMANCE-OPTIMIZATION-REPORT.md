# N8n Workflow Performance Optimization Report

Ngày phân tích: 2026-05-17

## 1. Scope

Đối tượng review và chỉnh sửa:

- `LLM API Test Generator.json`
- Backend contract liên quan trong `ClassifiedAds.Modules.TestGeneration`
- Docker/compose/config wiring cho webhook n8n

Mục tiêu:

- Đồng bộ workflow n8n với unified generation path của BE.
- Giảm latency cảm nhận khi generate test cases.
- Giảm rủi ro timeout/parse lỗi khi LLM trả JSON không đúng format.
- Đảm bảo payload callback về BE đúng DTO hiện tại.

## 2. Kết Luận Chính

Workflow cũ đã có một số tối ưu đúng hướng, đặc biệt là nhánh `generate-llm-suggestions` trả `202` sớm rồi callback async.

Điểm lệch lớn nhất là BE đang bật unified generation qua logical webhook `generate-test-cases-unified`, nhưng workflow n8n chưa có webhook path này. BE vì vậy có thể trigger đúng cấu hình nhưng n8n không có entrypoint tương ứng trong file workflow đang mở.

Backend hiện đã đủ cho unified async flow:

- API tạo `TestGenerationJob` và trả `202`.
- Background consumer build compact payload rồi trigger n8n.
- Callback endpoint đã có: `/api/test-suites/{suiteId}/test-cases/from-ai`.
- Docker/compose đã map env cho `generate-test-cases-unified`.

Vì vậy lần này không cần sửa BE code. Phần cần sync là workflow n8n.

## 3. Vấn Đề Trước Khi Sửa

### 3.1 Missing unified webhook

BE dùng:

- `N8nWebhookNames.GenerateTestCasesUnified = "generate-test-cases-unified"`
- `TestGenerationPayloadBuilder.WebhookName => GenerateTestCasesUnified`
- appsettings/docker compose đều map `generate-test-cases-unified`

Workflow cũ chỉ có các path:

- `explain-failure`
- `analyze-srs`
- `refine-srs-requirements`
- `generate-llm-suggestions`

Thiếu `generate-test-cases-unified`, nên unified flow không khớp workflow export.

### 3.2 Callback payload cần normalize

BE callback DTO hiện nhận một số trường là stringified JSON:

- `tags`
- `request.headers`
- `request.pathParams`
- `request.queryParams`
- `expectation.responseSchema`
- `expectation.headerChecks`
- `expectation.bodyContains`
- `expectation.bodyNotContains`
- `expectation.jsonPathChecks`

Trong prompt BE, LLM có thể trả object/array tự nhiên. Nếu n8n callback nguyên object/array vào các field string này, System.Text.Json có thể deserialize lỗi. Workflow mới normalize các field này trước khi POST callback.

### 3.3 Prompt lớn và dễ timeout

Workflow cũ gom endpoint prompt, SRS và rules vào prompt dài. Với suite lớn, chi phí token và latency tăng nhanh. BE đã có compact/truncate knobs, nhưng n8n vẫn cần giữ prompt endpoint-scoped và tránh gửi full SRS content không cần thiết.

## 4. Thay Đổi Đã Implement

File đã sửa: `LLM API Test Generator.json`

Đã thêm nhánh mới cho unified test-case generation:

1. `Webhook Generate Tests Unified`
   - Path: `generate-test-cases-unified`
   - Method: `POST`
   - Response mode: `responseNode`

2. `Parse Unified Generation Input`
   - Validate `testSuiteId`, `endpoints`, `callbackUrl`, `callbackApiKey`, `promptConfig`.
   - Giữ payload compact từ BE.

3. `Respond Ack Unified Generation`
   - Trả `202` ngay.
   - Giảm latency cảm nhận và giúp BE background trigger không chờ LLM hoàn tất.

4. `Prepare Unified Generation Prompt`
   - Tạo prompt compact theo endpoint.
   - Chỉ đưa endpoint-scoped SRS requirement brief, không đưa full SRS content.
   - Giữ dependency order để LLM tạo producer trước consumer.

5. `AI Agent Unified Generation`
   - Dùng Groq chat model hiện có trong workflow.
   - Có `continueRegularOutput` để nhánh parse có cơ hội fallback khi LLM lỗi.

6. `Parse Unified Generation Response`
   - Parse markdown-fenced JSON hoặc raw JSON.
   - Normalize test cases về đúng callback DTO của BE.
   - Stringify các object/array field mà BE đang nhận dưới dạng string.
   - Bổ sung fallback smoke test cases nếu LLM empty hoặc JSON parse failed, tránh job kẹt mãi ở `WaitingForCallback`.

7. `POST Unified Test Cases Callback`
   - POST `{ testCases: [...] }` về `callbackUrl`.
   - Gửi header `x-callback-api-key`.

## 5. Expected Performance Impact

### Cải thiện ngay

- BE trigger n8n nhận `202` nhanh hơn vì workflow ACK trước khi gọi LLM.
- Unified generation path của BE đã có entrypoint đúng trong n8n.
- Prompt gọn hơn ở n8n nhờ endpoint-scoped context và SRS brief.
- Ít callback failure hơn do normalize payload trước khi POST về BE.

### Chưa giải quyết triệt để

- Chưa chia batch theo dependency graph. Với suite rất lớn, một LLM call vẫn có thể chậm.
- Chưa có n8n-level retry/backoff cho callback HTTP request.
- Chưa persist metric riêng cho n8n prompt bytes, LLM ms, parse ms, callback ms.
- `analyze-srs` và `refine-srs-requirements` vẫn là synchronous webhook, có thể chậm với SRS dài.

## 6. BE Sync Assessment

Không sửa BE code vì các phần cần thiết đã tồn tại:

- `TestGenerationPayloadBuilder` đã build payload compact và webhook name đúng.
- `TestOrderController.ReceiveAiGeneratedTestCases` đã có callback endpoint.
- `SaveAiGeneratedTestCasesCommandHandler` đã persist test cases, requests, expectations, variables, dependencies và traceability links.
- `docker-compose.yml` đã có env wiring cho `N8N_WEBHOOK_GENERATE_TEST_CASES_UNIFIED`.

Điểm BE nên cân nhắc ở bước sau:

- Cho DTO callback accept object/array linh hoạt hơn với các JSON field string, để workflow bớt phải normalize.
- Thêm failure callback endpoint cho n8n để job không bị kẹt nếu LLM/callback fail.
- Thêm batch id nếu triển khai dependency-aware batching.

## 7. Risk Notes

- Workflow mới dùng credential Groq hiện có trong file. Nếu môi trường n8n production muốn dùng provider khác, cần đổi model node hoặc thêm provider branch.
- Fallback smoke test giúp job không kẹt, nhưng chất lượng thấp hơn LLM output. Các test fallback có tag `fallback` và traceability score thấp.
- Nếu BE payload `callbackUrl` trỏ qua ngrok/cloud tunnel, latency callback vẫn biến động. Benchmark nên dùng Docker network/internal URL khi có thể.

## 8. Verification

Đã kiểm tra:

- JSON workflow parse được bằng Node.js.
- Các node mới tồn tại trong workflow.
- JavaScript của các code node mới parse cú pháp được.
- Workflow hiện có webhook paths:
  - `explain-failure`
  - `analyze-srs`
  - `refine-srs-requirements`
  - `generate-llm-suggestions`
  - `generate-test-cases-unified`

Không chạy migration verification vì thay đổi không chạm EF model/DbContext/migration/seed.

Không chạy docker compose config/build vì không sửa Dockerfile hoặc `docker-compose.yml`.

## 9. Recommended Next Steps

1. Import lại `LLM API Test Generator.json` vào n8n.
2. Trigger BE endpoint `POST /api/test-suites/{suiteId}/generate-tests`.
3. Kiểm tra n8n execution:
   - webhook ACK 202
   - LLM node chạy
   - callback POST về BE 204
4. Kiểm tra BE:
   - generation job chuyển `Completed`
   - test cases được tạo
   - tags có `auto-generated`, `llm-suggested`
5. Nếu suite lớn vẫn chậm, bước tối ưu tiếp theo là dependency-aware batching.
