# Testcase Generation Performance Optimization Report

Ngày phân tích: 2026-05-17

## 1. Executive Summary

Report này phân tích hiệu năng của flow:

`Generate LLM Suggestions -> n8n/LLM -> Callback -> Bulk Approve -> Materialize Test Cases`

Kết luận chính:

- Flow hiện tại đã đi đúng hướng ở điểm queue job bất đồng bộ, nên API không cần chờ toàn bộ LLM generation hoàn tất.
- Trước khi tối ưu sâu, cần bổ sung structured metrics theo `JobId`, `TestSuiteId` và `BatchId`. Nếu không có metric, rất dễ tối ưu sai tầng: FE, BE, DB, n8n, LLM hoặc network.
- Các tối ưu ít rủi ro nên làm trước: giảm duplicate fetch ở FE, xử lý EF `MultipleCollectionIncludeWarning`, và benchmark không đi qua ngrok.
- Sau khi có baseline, tối ưu có impact lớn nhất thường là cache-first cho async LLM generation và batching payload theo dependency graph.
- Bulk insert/update chỉ nên triển khai theo threshold và phải giữ đúng audit, concurrency, versioning, traceability và transaction behavior.

Ưu tiên triển khai:

1. P0: Measurement, duplicate fetch, EF query optimization, benchmark không qua ngrok.
2. P1: Async LLM cache-first, partial cache, dependency-aware batching.
3. P2: Bulk persistence/materialization có threshold.
4. P3: Callback context snapshot, n8n workflow tuning, prompt/model profile.

## 2. Current Flow

Flow hiện tại theo log và code đang chạy như sau:

1. User tạo hoặc cập nhật test suite scope.
2. BE tạo test order proposal.
3. User có thể reorder proposal.
4. User approve proposal.
5. User trigger LLM suggestion generation.
6. `GenerateLlmSuggestionPreviewCommand` validate suite, approved order, limit, metadata, parameter details và SRS context.
7. BE tạo `TestGenerationJob`, build payload và gửi message trigger n8n.
8. Background consumer gọi n8n workflow.
9. n8n/LLM xử lý và gọi callback về BE.
10. Callback parse/refine scenarios, persist `LlmSuggestion`.
11. User bulk approve suggestions.
12. BE materialize suggestions thành `TestCase`, `TestCaseRequest`, `TestCaseExpectation`, variables, dependencies, changelog, version và traceability links.

Điểm quan trọng: thời gian LLM/n8n và thời gian BE callback/materialize phải được đo tách riêng. Không nên gộp tất cả vào một con số "gen testcase chậm".

## 3. Observed Bottlenecks

### FE duplicate fetch

Log cho thấy `Fetching specifications` và `Fetching endpoints` bị gọi lặp lại nhiều lần trong cùng một workflow.

Tầng ảnh hưởng: FE và BE.

Tác động:

- FE tạo cảm giác màn hình chậm do refetch sau nhiều thao tác.
- BE và DB nhận thêm request không cần thiết.
- Với spec lớn, endpoint list có thể trở thành chi phí đáng kể dù LLM chưa chạy.

Khuyến nghị:

- FE cache `projects/specifications/endpoints` theo `projectId + specId`.
- Sau approve/reorder, chỉ invalidate order/proposal state nếu API spec không đổi.
- Tránh refetch endpoints nếu chỉ thay đổi test suite scope hoặc test order.
- BE có thể trả thêm order snapshot hoặc endpoint summary cần thiết để FE không phải fetch lại ngay.

### EF Core query/include issue

Log có warning:

```text
Compiling a query which loads related collections for more than one collection navigation...
QuerySplittingBehavior.SingleQuery ... can potentially result in slow query performance.
```

Tầng ảnh hưởng: BE và DB.

Query nghi vấn trong flow list testcase:

- `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCasesByTestSuiteQuery.cs`
- Include nhiều navigation: `Request`, `Expectation`, `Variables`, `Dependencies`.

Tác động:

- EF có thể sinh một SQL join lớn.
- Số dòng trả về có thể bị nhân lên theo số variables/dependencies.
- Khi số testcase tăng sau bulk materialize, list testcase có thể chậm rõ rệt.

Khuyến nghị:

- Trong dev/perf, cấu hình warning thành exception để xác định query chính xác.
- Với list view, ưu tiên projection sang model nhẹ thay vì load full aggregate.
- Nếu vẫn cần load nhiều collection, dùng `AsSplitQuery()` có kiểm soát.
- Tách list query và detail query:
  - List: fields hiển thị nhanh.
  - Detail: request, expectation, variables, dependencies cho một testcase.

### Async LLM payload quá lớn

Sync path trong `LlmScenarioSuggester.SuggestScenariosAsync` có batching/adaptive split. Async path hiện build payload cho toàn bộ approved order qua `BuildAsyncRefinementPayloadAsync`.

Tầng ảnh hưởng: BE, n8n và LLM.

Tác động tiềm năng:

- Payload lớn làm tăng token count và LLM latency.
- n8n dễ timeout hoặc trả lỗi khi payload quá lớn.
- Retry kém hiệu quả vì phải retry cả payload.
- Callback nhận một response lớn, khó persist từng phần.

Lưu ý: với log hiện tại chỉ có 3 endpoint và 12 suggestions, chưa đủ metric để kết luận payload đang là bottleneck chính. Đây là rủi ro sẽ rõ hơn khi suite lớn.

Khuyến nghị:

- Dùng batching cho async path sau khi đã có metric baseline.
- Batch phải dependency-aware, không chia tùy tiện.
- Endpoint tiêu thụ dữ liệu/token/id từ endpoint trước phải ở cùng batch hoặc batch sau dependency.
- Mỗi batch cần `BatchId`, `BatchIndex`, `BatchCount`, `EndpointCount`, `PayloadBytes`.

### Async path thiếu cache-first

Sync path có cache lookup trước khi gọi n8n. Async path hiện vẫn build payload và queue n8n dù context có thể đã từng được generate.

Tầng ảnh hưởng: BE, DB, n8n, LLM.

Tác động:

- Cache hit không được tận dụng.
- User trigger lại cùng suite/spec/order vẫn có thể tốn LLM roundtrip.
- n8n và model chịu tải không cần thiết.

Cache-first async path chỉ an toàn nếu cache key đủ chặt. Cache key tối thiểu phải bao gồm:

- `TestSuiteId`
- `SpecificationId`
- approved endpoint order
- endpoint method/path/schema signature
- SRS/requirement fingerprint
- feedback fingerprint
- algorithm/profile version
- prompt version
- model/version nếu output phụ thuộc model

Khuyến nghị:

- All cache hit: persist suggestions và mark job `Completed`, không gọi n8n.
- Partial cache hit: persist phần hit, chỉ gửi n8n cho endpoint miss.
- Cache miss: gửi n8n theo batch.
- Log rõ `cache_hit_count`, `cache_miss_count`, `cache_key`, `prompt_version`, `model_version`.

### Callback load lại context

Callback hiện load lại job, suite, approved order, endpoint metadata, parameter details, SRS document và SRS requirements trước khi parse response.

Tầng ảnh hưởng: BE và DB.

Tác động:

- Tăng DB roundtrip trong callback.
- Với spec/SRS lớn, callback BE processing có thể chậm.
- Tuy nhiên đây cũng là cơ chế đảm bảo parser dùng metadata mới nhất.

Khuyến nghị:

- Không tối ưu vội ở P0/P1 nếu chưa có metric cho `callback_context_load_ms`.
- Context snapshot là tối ưu P3, chỉ nên làm khi metric chứng minh context load là bottleneck.
- Nếu triển khai snapshot, cần lưu compact context theo `JobId` kèm context hash/version để tránh parse bằng metadata cũ.

### Persist/materialize nhiều entity từng dòng

Persist suggestions và bulk approve/materialize hiện add/update nhiều entity trong vòng lặp rồi `SaveChangesAsync`.

Tầng ảnh hưởng: BE và DB.

Các entity liên quan:

- `LlmSuggestion`
- `TestCase`
- `TestCaseRequest`
- `TestCaseExpectation`
- `TestCaseVariable`
- `TestCaseDependency`
- `TestCaseChangeLog`
- `TestCaseRequirementLink`
- `TestSuiteVersion`
- `TestSuite`

Tác động:

- Với 12 suggestions, EF normal path có thể vẫn đủ tốt.
- Với 30+ suggestions/testcases, EF change tracking và số SQL statements có thể tăng đáng kể.

Khuyến nghị:

- Dưới 30 suggestions/testcases: giữ EF normal path để giảm rủi ro.
- Từ 30 trở lên: dùng bulk path có kiểm soát.
- Log `materialized_count`, `db_save_ms`, `bulk_path_enabled`.

### Ngrok không phù hợp làm benchmark

Log callback dùng URL ngrok.

Tầng ảnh hưởng: network/tunnel.

Tác động:

- Ngrok thêm latency, proxy hop, TLS overhead và biến động mạng.
- Kết quả đo qua ngrok không đại diện production hoặc Docker network nội bộ.
- Ngrok phù hợp demo callback, không phù hợp làm baseline performance.

Khuyến nghị:

- Benchmark local bằng Docker network, ví dụ n8n gọi `http://webapi:8080/...`.
- Nếu bắt buộc dùng n8n cloud, đo riêng `callback_network_ms` và không tính vào BE processing time.

## 4. Recommended Solution

### P0 - Measure and Remove Low-Risk Bottlenecks

Mục tiêu P0 là tạo baseline đáng tin cậy và loại bỏ bottleneck ít rủi ro.

Việc cần làm:

1. Bổ sung structured logs theo `JobId`, `TestSuiteId`, `BatchId`.
2. Đo riêng từng stage: API queue, message queue delay, n8n trigger, LLM processing, callback context load, callback persist, bulk materialize.
3. FE giảm duplicate fetch specifications/endpoints khi spec không đổi.
4. Xử lý EF `MultipleCollectionIncludeWarning` trong flow list testcase:
   - projection nhẹ cho list view, hoặc
   - `AsSplitQuery()` nếu vẫn cần include nhiều collection.
5. Benchmark không qua ngrok.

Lý do:

- Đây là nhóm thay đổi rủi ro thấp.
- Có thể cải thiện UX ngay.
- Tạo số liệu để quyết định P1/P2/P3 có đáng làm không.

### P1 - Optimize Async LLM Generation

Mục tiêu P1 là giảm số lần gọi n8n/LLM và giảm kích thước payload.

Việc cần làm:

1. Thêm cache-first cho async path.
2. Hỗ trợ partial cache:
   - endpoint hit cache không gọi n8n,
   - endpoint miss mới đi n8n.
3. Thiết kế cache key an toàn gồm:
   - `TestSuiteId`
   - `SpecificationId`
   - approved endpoint order
   - endpoint method/path/schema signature
   - SRS/requirement fingerprint
   - feedback fingerprint
   - algorithm/profile version
   - prompt version
   - model/version nếu output phụ thuộc model
4. Chia payload theo dependency-aware batching.
5. Giới hạn concurrency, ví dụ `MaxConcurrentLlmBatches=2..4`.
6. Retry theo batch, không retry toàn bộ suite khi chỉ một batch lỗi.
7. Log `payload_bytes`, `endpoint_count`, `cache_hit_count`, `cache_miss_count`, `llm_latency_ms`, `tokens_used`.

Guardrail quan trọng:

- Không được chia batch tùy tiện nếu endpoint sau phụ thuộc token/id/body variable từ endpoint trước.
- Dependency graph phải là input của batching.
- Output order phải deterministic để materialize và UI ổn định.

### P2 - Optimize Persistence and Materialization

Mục tiêu P2 là giảm DB/EF overhead khi số suggestions/testcases lớn.

Threshold đề xuất:

- `< 30 suggestions/testcases`: giữ EF normal path.
- `>= 30 suggestions/testcases`: dùng bulk path.

Việc cần làm:

1. Bulk update suggestions cũ sang `Superseded`.
2. Bulk insert suggestions mới.
3. Bulk insert các entity materialized theo từng table:
   - `TestCase`
   - `TestCaseRequest`
   - `TestCaseExpectation`
   - `TestCaseVariable`
   - `TestCaseDependency`
   - `TestCaseChangeLog`
   - `TestCaseRequirementLink`
4. Bulk update approved suggestions với `AppliedTestCaseId`.
5. Vẫn giữ transaction bao quanh toàn bộ materialization.
6. Log `normal_path_ms`, `bulk_path_ms`, `dependency_enrich_ms`, `db_write_ms`.

Rủi ro bắt buộc phải xử lý:

- `CreatedDateTime`
- `UpdatedDateTime`
- `CreatedBy/UpdatedBy`
- `RowVersion` hoặc concurrency token
- domain events
- audit log
- `TestSuiteVersion`
- traceability links
- transaction behavior

Nếu bulk path bypass repository hooks hoặc EF tracking, phải explicitly set đầy đủ các field/audit side effects tương đương normal path.

### P3 - Optimize Callback and LLM Workflow

Mục tiêu P3 là tối ưu sâu sau khi metric chứng minh còn bottleneck ở callback hoặc n8n/model.

Việc cần làm:

1. Context snapshot theo `JobId` nếu `callback_context_load_ms` cao:
   - approved order
   - compact endpoint metadata
   - compact parameter details
   - SRS requirement briefs
   - algorithm/profile version
   - prompt version
   - context hash
2. Callback đọc snapshot thay vì query lại nhiều bảng, nhưng phải validate hash/version.
3. Tuning n8n workflow:
   - tách parse/format JSON khỏi bước LLM nếu có thể,
   - giảm transform lặp,
   - log duration từng node quan trọng.
4. Tuning prompt/model:
   - prompt compact nhưng không làm mất contract rules,
   - profile theo nhu cầu: draft nhanh, standard, high accuracy,
   - model/version nằm trong cache key nếu output phụ thuộc model.

Lưu ý:

- Context snapshot không nên làm ở P0 vì tăng complexity.
- Chỉ triển khai khi metric chứng minh callback context load là bottleneck thực tế.

## 5. Metrics to Add

### Generate request

- `generation.validate_ms`
- `generation.load_suite_ms`
- `generation.require_order_ms`
- `generation.limit_check_ms`
- `generation.pending_suggestions_check_ms`
- `generation.metadata_ms`
- `generation.parameter_detail_ms`
- `generation.srs_load_ms`
- `generation.build_payload_ms`
- `generation.payload_bytes`
- `generation.enqueue_message_ms`
- `generation.total_api_ms`
- `generation.cache_hit_count`
- `generation.cache_miss_count`

### Background trigger

- `generation.queue_delay_ms = TriggeredAt - QueuedAt`
- `generation.webhook_accept_ms`
- `generation.webhook_url`
- `generation.payload_bytes`
- `generation.endpoint_count`
- `generation.batch_id`
- `generation.batch_index`
- `generation.batch_count`
- `generation.retry_count`

### Callback

- `generation.callback_received_at`
- `generation.callback_age_ms = CallbackReceivedAt - TriggeredAt`
- `generation.callback_parse_ms`
- `generation.callback_context_load_ms`
- `generation.callback_persist_ms`
- `generation.suggestion_count`
- `generation.tokens_used`
- `generation.model`
- `generation.batch_id`

### Bulk review/materialize

- `suggestion_review.load_suggestions_ms`
- `suggestion_review.limit_check_ms`
- `suggestion_review.load_existing_testcases_ms`
- `suggestion_review.materialize_ms`
- `suggestion_review.dependency_enrich_ms`
- `suggestion_review.db_save_ms`
- `suggestion_review.materialized_count`
- `suggestion_review.path = normal|bulk`

## 6. Risks and Guardrails

### Cache key sai

Rủi ro:

- Trả suggestions cũ khi Swagger, SRS, feedback, prompt hoặc model đã đổi.

Guardrail:

- Cache key phải include đủ suite/spec/order/schema/SRS/feedback/profile/prompt/model signature.
- Log cache key và các version/fingerprint liên quan.

### Batch sai dependency

Rủi ro:

- Endpoint sau cần token/id/email/resource từ endpoint trước nhưng bị chạy/generate tách sai context.

Guardrail:

- Batching dựa trên dependency graph.
- Producer phải nằm cùng batch hoặc batch trước consumer.
- Output phải giữ order deterministic.

### Bulk path bypass domain/audit

Rủi ro:

- Mất `CreatedDateTime`, `UpdatedDateTime`, `CreatedBy/UpdatedBy`, `RowVersion`, audit log, domain events, `TestSuiteVersion`, traceability links hoặc transaction behavior.

Guardrail:

- Bulk path chỉ bật từ threshold 30+.
- Unit/integration tests so sánh normal path và bulk path.
- Transaction bao quanh toàn bộ write set.

### Prompt compact quá mức

Rủi ro:

- Testcase sai expected status, sai dependency, thiếu assertion hoặc không bám OpenAPI/SRS.

Guardrail:

- A/B test bằng benchmark suite cố định.
- Theo dõi pass rate, false failure rate, token count và latency.

### Benchmark qua ngrok

Rủi ro:

- Kết luận sai vì tunnel latency không đại diện production performance.

Guardrail:

- Baseline bằng Docker network hoặc môi trường production-like.
- Nếu dùng ngrok, tách riêng network/tunnel latency khỏi BE processing time.

## 7. Acceptance Criteria

Performance improvement chỉ được xem là đạt khi có đủ các tiêu chí sau:

1. Có structured logs theo `JobId`, `TestSuiteId`, `BatchId`.
2. API queue request p95 `< 1s` với suite nhỏ.
3. Callback BE processing p95 `< 2s` cho 12 suggestions, không tính LLM time.
4. Bulk materialize p95 `< 1s` cho 12 suggestions.
5. Không còn EF `MultipleCollectionIncludeWarning` trong list testcase flow.
6. FE không refetch specifications/endpoints nhiều lần khi spec không đổi.
7. Suite lớn được batch payload theo dependency-aware batching.
8. Partial cache hit không gọi n8n cho endpoint đã hit cache.
9. Benchmark chính không đi qua ngrok.
10. Nếu bulk path được bật, normal path và bulk path tạo cùng audit/version/traceability/concurrency output.

## 8. Final Recommendation

Không nên bắt đầu bằng bulk rewrite hoặc context snapshot. Hai nhóm đó có rủi ro cao hơn và chỉ đáng làm khi metric chứng minh BE/DB persistence hoặc callback context load là bottleneck.

Thứ tự nên triển khai:

1. Làm P0 trước để có baseline đúng và loại bỏ bottleneck ít rủi ro.
2. Làm P1 nếu metric cho thấy n8n/LLM payload, latency hoặc duplicate generation là vấn đề.
3. Làm P2 nếu số suggestions/testcases tăng và `db_save_ms` hoặc materialization p95 vượt target.
4. Làm P3 cuối cùng, khi callback context load hoặc n8n/model/prompt thực sự là phần chậm còn lại.

Ưu tiên kỹ thuật thực tế nhất: đo đúng trước, giảm request thừa, sửa query list testcase, benchmark không qua ngrok, sau đó mới tối ưu async LLM bằng cache-first và dependency-aware batching.
