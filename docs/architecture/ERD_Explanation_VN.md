# Giải thích chi tiết Biểu đồ Thực thể - Liên kết (ERD)
Dự án: **GSP26SE43.ModularMonolith**

Tài liệu này cung cấp lời giải thích chi tiết về cấu trúc cơ sở dữ liệu dựa trên file `erDiagram.txt`. Bạn có thể sử dụng tài liệu này để hiểu sâu về hệ thống và trình bày với giảng viên.

## 1. Tổng quan Kiến trúc Cơ sở dữ liệu
Hệ thống được thiết kế theo kiến trúc **Modular Monolith** (Nguyên khối theo Module). Cấu trúc Database được chia thành nhiều **Schema/Module** khác nhau, nhận diện qua các tiền tố (prefix) của bảng:
- `identity_`: Quản lý người dùng, phân quyền và xác thực.
- `apidoc_`: Quản lý tài liệu API (Projects, Specifications, Endpoints).
- `testgen_`: Quản lý việc sinh ra các kịch bản kiểm thử (Test Cases, Test Suites).
- `testexec_`: Quản lý quá trình thực thi kiểm thử.
- `testreport_`: Quản lý báo cáo và độ bao phủ sau kiểm thử.
- `sub_`: Quản lý gói cước (Subscription), thanh toán và hạn mức sử dụng (Quotas).
- `storage_`: Quản lý file lưu trữ.
- `notif_`: Quản lý thông báo (Email, SMS).
- `config_`: Quản lý cấu hình chung và đa ngôn ngữ.
- `auditlog_`: Quản lý nhật ký kiểm toán chung.
- `llm_`: Quản lý tương tác với các mô hình ngôn ngữ lớn (AI/LLM).

### Các Pattern chung (Rất quan trọng để trình bày)
1. **Outbox Pattern**: Trong hầu hết các module đều có bảng `*_OutboxMessages` và `*_ArchivedOutboxMessages`. Đây là một Design Pattern phổ biến trong Microservices/Modular Monolith dùng để **đảm bảo tính nhất quán dữ liệu** khi gửi message/event giữa các module với nhau (Event-Driven Architecture) bằng cách lưu event vào database cùng transaction với việc lưu dữ liệu nghiệp vụ sau đó mới gửi đi.
2. **Audit Logging**: Mỗi module đều có bảng `*_AuditLogEntries` để độc lập tự quản lý lịch sử thao tác của các object thuộc module đó.
3. **Optimistic Concurrency Control**: Hầu hết các bảng chính đều có cột `binary RowVersion`. Cột này được ORM dùng để xử lý đụng độ đồng thời (khi 2 user cùng update 1 dòng dữ liệu tại 1 thời điểm).

---

## 2. Phân tích chi tiết từng Module cốt lõi

### 2.1. Module Identity (Xác thực & Phân quyền)
- **Cốt lõi**: `identity_Users` và `identity_Roles`. 
- **Mối quan hệ**: 
  - N-N thông qua bảng trung gian `identity_UserRoles` (Một User có nhiều Role, một Role thuộc về nhiều User).
  - Một `identity_Users` có quan hệ 1-1 với `identity_UserProfiles` (chứa các thông tin bổ sung và hiển thị như Avatar, Timezone).
- Các bảng khác như `UserClaims`, `RoleClaims`, `UserTokens`, `UserLogins` là cấu trúc và các thực thể tiêu chuẩn của Identity Framework.

### 2.2. Module ApiDoc (Quản lý API)
Module này dùng để parse và lưu trữ cấu trúc API được import vào.
- **`apidoc_Projects`**: Chứa thông tin cấu hình cao nhất của Project (ví dụ: BaseUrl, Tên dự án). Có quan hệ 1-N với `apidoc_ApiSpecifications`.
- **`apidoc_ApiSpecifications`**: Mỗi Project có thể có nhiều phiên bản tài liệu API (ví dụ: các file Swagger/OpenAPI tải lên qua các đợt cập nhật). Trong mỗi Project luôn có 1 file spec đang active (`ActiveSpecId`).
- **`apidoc_ApiEndpoints`**: Mỗi file Specification chứa hàng loạt API Endpoint.
  - Mỗi Endpoint chứa các bảng dữ liệu liên quan chặt chẽ phía sau (Quan hệ 1-N):
    - **`apidoc_EndpointParameters`**: Quản lý chi tiết từng tham số (Parameter) mà API yêu cầu. 
      - Cột `Location` xác định tham số nằm ở đâu: Query String (vd: `?id=1`), Path (vd: `/users/{id}`), Header (vd: `x-api-key`), hoặc Cookie.
      - Cột `DataType` và `Format` quy định kiểu dữ liệu (string, int, boolean) và định dạng (uuid, date-time).
      - Cột `Schema` (kiểu `jsonb`) lưu trữ cấu trúc phức tạp nếu tham số là một object.
      - Bảng này giúp hệ thống TestGen biết chính xác cần sinh dữ liệu gì và nhúng vào đâu khi tạo Request giả lập.

    - **`apidoc_EndpointResponses`**: Định nghĩa cấu trúc dữ liệu trả về cho từng mã trạng thái HTTP (HTTP Status Code).
      - Cột `StatusCode` lưu mã lỗi (ví dụ: 200 OK, 400 Bad Request, 404 Not Found, 500 Server Error).
      - Cột `Schema` (kiểu `jsonb`) lưu toàn bộ cấu trúc JSON Data Model trả về.
      - Cây dữ liệu này là cơ sở để module TestGen tự động đối chiếu (Assertion) xem API có trả về đúng Cấu trúc (Schema Validation) mà bản thiết kế ban đầu đề ra hay không.

    - **`apidoc_EndpointSecurityReqs`**: Xác định yêu cầu bảo mật riêng cho từng Endpoint.
      - API này có cần truyền Token (Bearer) hay API Key không? 
      - Nếu dùng OAuth2, API này yêu cầu những quyền (`Scopes` kiểu `jsonb`) nào? (ví dụ: `read:users`, `write:orders`).
      - Bảng này liên kết chặt chẽ với bảng `apidoc_SecuritySchemes` để TestGen biết cách tự động gắn Token/Auth Header hợp lệ vào Request trước khi gửi đi test.

### 2.3. Module TestGen (Sinh kịch bản kiểm thử)
Đây là module nghiệp vụ lõi (Core Business) đảm nhận việc sinh Test (tự động hoặc thủ công).
- **`testgen_TestSuites`**: Nhóm gộp tập hợp các Test Cases lại với nhau. Mỗi TestSuite được sinh ra từ một file ApiSpec. Bảng này quản lý phiên bản (`Version`), trạng thái duyệt (`ApprovalStatus`) và người thực hiện.
- **`testgen_TestSuiteVersions`**: Lưu trữ lịch sử thay đổi của một Test Suite (Data Versioning). Cột `PreviousState` và `NewState` (chuỗi JSON) giúp người dùng undo, audit hoặc so sánh thay đổi giữa các version của 1 bộ test.
- **`testgen_TestCases`**: Thông tin kịch bản kiểm thử độc lập (ví dụ: "Test đăng nhập sai password"). Một TestSuite có nhiều TestCase. Điểm đặc biệt:
  - Cột `DependsOnId`: Là quan hệ tự liên kết (Self-referencing), giúp định nghĩa Test B phải chạy sau Test A (ví dụ: Phải lấy được token ở Test A mới truyền vào Test B được).
  - Cột `Priority`, `IsEnabled`, `OrderIndex`: Dùng để quản lý luồng thực thi ưu tiên và có loại bỏ ca test này đi trong Run sắp tới hay không.
- Đi sâu vào thiết kế một Test Case, hệ thống chia ra rất cụ thể qua quan hệ 1-1:
    - **`testgen_TestCaseRequests`**: Lưu định dạng cụ thể của request muốn test (Url, Body JSON, Headers).
    - **`testgen_TestCaseExpectations`**: Kết quả đầu ra mong chờ, để assert lúc chạy thực tế (ExpectedStatus, Check Body JsonPath).
- **`testgen_TestOrderProposals`**: Đây là nơi ghi lại sự tương tác của AI trong việc phân tích API Specification và *đề xuất thứ tự chạy* các Test Cases sao cho hợp business flow nhất (ví dụ: tạo user -> login -> tạo order -> thanh toán).
  - Bảng này lưu lại Output của LLM (`ProposedOrder`, `AiReasoning`) giúp con người review và áp dụng (`AppliedOrder`) mà không lệ thuộc 100% vào máy.
- **`testgen_TestCaseVariables`**: Định nghĩa việc "Bóc tách dữ liệu" (Data Extraction) sau khi chạy 1 Test Case. Ví dụ: Extract `access_token` từ Response JSON bằng JSONPath để lưu vào biến, dùng cho Test Case tiếp theo.
- **`testgen_TestDataSets`**: Lưu tập dữ liệu mẫu (Data-driven testing). Ví dụ thay vì tạo 10 Test Cases cho 10 user khác nhau, chỉ tạo 1 Test Case nhưng loop qua 1 Dataset có 10 dòng JSON.
- **`testgen_TestCaseChangeLogs`**: Tracking chi tiết đến từng Field của TestCase đã bị thay đổi bởi ai (`ChangeType`, `OldValue`, `NewValue`). Rất tốt cho tính năng Audit History.

### 2.4. Module TestExec & TestReport (Thực thi và Báo cáo)
Hai module này tách biệt với TestGen để tối ưu performance khi Runner Engine thực hiện việc call API hàng loạt.
- **`testexec_ExecutionEnvironments`**: Lưu thông tin môi trường chạy (Local, Staging, Production). Bảng này cho phép User lưu sẵn các `Variables` (BaseURL) và `AuthConfig` (Username/Pass test mặc định) riêng cho hệ thống đó lặp đi lặp lại.
- **`testexec_TestRuns`**: Mỗi lần nhấn "Test", hệ thống sinh ra một `TestRun`. Bảng này đóng vai trò như vé theo dõi (Tracking ticket) tiến độ:
  - Cột `Status` (Queued, Running, Completed).
  - Thống kê đếm tổng (Aggregated Counters): `PassedCount`, `FailedCount`, `DurationMs`.
  - Cột `RedisKey` & `ResultsExpireAt`: Điểm kỹ thuật cực kỳ đắt giá! Việc chạy test sẽ sinh ra log request/response khổng lồ. Thiết kế này ám chỉ log chi tiết không ném hết vào SQL Database gây tắc nghẽn, mà được đẩy vào Caching/NoSQL (Redis) có thời hạn (TTL) thông qua Key.
- **`testreport_CoverageMetrics`**: Report toán học về mật độ và độ bao phủ sau khi Test. Bảng này lưu Metrics như:
  - Cột `TotalEndpoints` vs `TestedEndpoints` để tính ra `CoveragePercent` (%).
  - Cột `ByMethod`, `ByTag`, `UncoveredPaths` ở dạng JSONB để tiện vẽ biểu đồ trên UI.
- **`testreport_TestReports`**: Chứa thông tin Metadata về file báo cáo (PDF, HTML) sau test đã cấu trúc lại để User download. Liên kết qua `storage_FileEntries` qua `FileId`.

### 2.5. Module Sub (Subscription - Gói cước và Thanh toán SaaS)
Đây là thiết kế tiêu chuẩn của một dự án SaaS B2B/B2C tính phí.
- **`sub_SubscriptionPlans`**: Danh mục các Gói cước (Free, Starter, Pro, Enterprise) kèm số tiền từng chu kỳ (Tháng/Năm).
- **`sub_PlanLimits`**: Bảng này là "Trái tim" của hệ thống Quota. Nó gắp 1 Plan với N Limit (Ví dụ: Giao diện định nghĩa Gói Pro có LimitValue=5 cho LimitType="ProjectCount").
- **`sub_UserSubscriptions`**: Ghi nhận một Gói cước đang được thuê bởi 1 User (hoặc Tenant). 
  - Chứa ngày bắt đầu hợp đồng (`StartDate`), ngày hết hạn (`EndDate`), và ID liên kết bên ngoài nếu dùng cổng quốc tế như Stripe (`ExternalSubId`).
  - Điểm sáng: Snapshot (lưu vết tạm) thông tin gói cước lúc mua (`SnapshotPriceMonthly`). Nếu sau này Admin đổi giá gói Pro, ông User cũ vẫn bị tính giá cũ. Cực kỳ hợp lý!
- **`sub_PaymentIntents`** & **`sub_PaymentTransactions`**: Theo dõi quy trình thanh toán Checkout.
  - `PaymentIntents`: Phác thảo ý định thanh toán. Khởi tạo một order, sinh `CheckoutUrl` cho user nhấn vào. Quá giờ (`ExpiresAt`) thì huỷ.
  - `PaymentTransactions`: Bảng lưu biên lai. Lưu vết Transaction thực sự thành công hay thất bại trên nhà cung cấp (`Provider` vd VNPay/Stripe, cột `FailureReason`). 
- **`sub_SubscriptionHistories`**: Tracking luồng Upgrade/Downgrade gói cước của User để biết tháng nào họ lên đời / xuống đời.
- **`sub_UsageTrackings`**: Tracking mức độ dùng tài nguyên của User (Đã tạo bao nhiêu Project, Dùng bao nhiêu token AI) trong chu kỳ tháng đó (`PeriodStart` -> `PeriodEnd`) để so sánh với cái `sub_PlanLimits`. Dùng lố limit hệ thống sẽ chặn.

### 2.6. Module LLM (Tích hợp AI)
Quản lý hoàn toàn tương tác của người dùng và hệ thống back-end với bên thứ 3 cung cấp AI (Ví dụ OpenAI/Gemini/Anthropic).
- **`llm_LlmInteractions`**: Viết xuống mọi tương tác (Prompt/Response) dưới dạng Log.
  - Cột `InteractionType`: Định dạng câu hỏi là sinh Test Case hay cấu hình luồng.
  - Cột `ModelUsed`: Lưu lại đang gọi GPT-4 hay GPT-3.5 cho mục đích tracing chi phí.
  - Cột `TokensUsed` & `LatencyMs`: Đo đếm độ trễ và số Token. Rất quan trọng khi làm dự án thực tế vì Token là Tiền. Quản trị được cost ở cấp độ User ID.
- **`llm_LlmSuggestionCaches`**: AI Caching layer để giảm tải API Cost.
  - Khi 1 User yêu cầu AI sinh test cho API `/api/v1/users`, nếu `CacheKey` này (có thể là hash của cấu trúc input) đã có trong DB và chưa `ExpiresAt`, hệ thống bốc luôn `Suggestions` JSON trả về mà KO gọi LLM. -> Đây là một Pattern cực xịn để pass qua các câu hỏi hóc búa của giảng viên về chi phí duy trì dự án sinh viên.

### 2.7. Module Config (Cấu hình)
- **`config_ConfigurationEntries`**: Lưu biến số hệ thống dạng Key-Value có thể sửa đổi trên Admin UI ở Runtime (mà không cần deploy lại code). Đặc biệt cột `IsSensitive` giúp che các API Key tránh chui vào log.
- **`config_LocalizationEntries`**: Hệ thống đã sẵn sàng cho đa ngôn ngữ (i18n). Lưu các translation key text theo `Culture` (VD: "vi-VN", "en-US").

---

## 3. Gợi ý Cách Thuyết trình và "Khoe" điểm mạnh với Giảng viên
Khi bảo vệ đồ án/giới thiệu project với thầy cô, bạn nên nhấn mạnh những điểm kiến trúc đắt giá sau:

1. **Khẳng định tính "Modular" rõ rệt**:
   - Thay vì vứt chung tất cả 50-60 bảng vào chung một rổ, bạn hãy chỉ ra tiền tố các bảng (`identity_`, `testgen_`, `sub_`...) phân rã miền dữ liệu rất tốt (DDD - Domain Driven Design contexts). Nếu quy mô dự án mở rộng, việc cắt 1 nhóm bảng ra thành 1 Service/Database riêng biệt (Microservices) là cực kỳ khả thi.

2. **Cách phối hợp Event-Driven (Cực kỳ "ăn điểm" kỹ thuật)**: 
   - Trình bày về **Outbox Pattern**. Hỏi giảng viên "Tưởng tượng khi có user thanh toán xong Subscription, làm sao để cộng số lượt sử dụng LLM mà hệ thống không bị crash lúc kết nối mạng lỗi?". Việc bạn thiết kế bảng `OutboxMessages` giải quyết nhược điểm lưu trữ event trên hệ thống phân tán rất tinh tế.

3. **Cấu trúc lõi chuyên sâu**: 
   - Giải thích Flow chính: `ApiSpecifications` -> Trích xuất thành `ApiEndpoints` -> Bóc tách kịch bản thành `TestCases` -> Cấu trúc độc lập phần gửi đi (`Requests`) và kết quả kì vọng (`Expectations`).
   - Sơ đồ này cho thấy bạn xử lý Cấu trúc Dữ Liệu động (dynamic schemas/JSON payloads) bài bản thông qua việc lưu các cột dưới dạng `jsonb`.

4. **Biết quản lý chi phí & tài nguyên bằng AI**: 
   - Đừng chỉ nói "Dự án tụi em dùng AI", hãy đưa giảng viên xem cơ chế phân nhỏ module `llm_LlmInteractions` và `llm_LlmSuggestionCaches`. Nhấn mạnh rằng nhóm đã giải quyết được bài toán lớn của thực tế là "Audit Tokens" và "Dùng Cache để giảm tải chi phí gọi API OpenAI/AI". Trình bày điều này cho thấy tư duy kỹ sư và tính ứng dụng thương mại của dự án (Commercial Mindset SaaS).
