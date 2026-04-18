# Báo Cáo Full Luồng A-Z Từ Upload Swagger Đến Khi Hoàn Thành Sinh Test

## Mục tiêu của tài liệu

Tài liệu này viết lại toàn bộ luồng theo **code thực tế hiện tại** của repo, bắt đầu từ lúc người dùng thả file Swagger/OpenAPI vào hệ thống cho tới khi:

1. specification được lưu và parse,
2. test suite được tạo,
3. thứ tự endpoint được đề xuất và approve,
4. test case được sinh ra,
5. suite chuyển sang trạng thái `Ready`.

Tài liệu này dừng ở điểm **test case đã được persist thành công**. Sau đó module `TestExecution` có thể dùng dữ liệu này để chạy thực tế.

---

## 1. Luồng người dùng nhìn thấy từ ngoài vào trong

Nếu đi theo đúng hành trình sử dụng chức năng từ UI/API, chuỗi thao tác đầy đủ là:

1. Upload file Swagger/OpenAPI vào project:
   `POST /api/projects/{projectId}/specifications/upload`
2. Hệ thống lưu file, tạo `ApiSpecification`, parse endpoint, parameter, response.
3. Người dùng tạo test suite và chọn endpoint muốn sinh test:
   `POST /api/projects/{projectId}/test-suites`
4. Hệ thống đề xuất thứ tự chạy endpoint:
   `POST /api/test-suites/{suiteId}/order-proposals`
5. Người dùng có thể reorder nếu muốn:
   `PUT /api/test-suites/{suiteId}/order-proposals/{proposalId}/reorder`
6. Người dùng approve thứ tự:
   `POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`
7. Sau khi gate đã pass, người dùng sinh test:
   `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`
   hoặc
   `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`
   hoặc
   `POST /api/test-suites/{suiteId}/generate-tests`
8. Hệ thống gọi n8n/LLM hoặc rule engine, materialize test case, enrich dependency, lưu DB.
9. `TestSuite.Status` được cập nhật thành `Ready`.

---

## 2. Full luồng A-Z theo call stack thật trong code

## 2.1. Bước A - Upload file Swagger/OpenAPI

Điểm vào đầu tiên là `SpecificationsController.Upload()` trong `ClassifiedAds.Modules.ApiDocumentation/Controllers/SpecificationsController.cs`.

Controller nhận `multipart/form-data`, sau đó tạo `UploadApiSpecificationCommand` và dispatch sang `UploadApiSpecificationCommandHandler.HandleAsync()`.

### `UploadApiSpecificationCommandHandler` làm gì

Handler này là điểm bắt đầu thực sự của flow import spec. Nó xử lý theo thứ tự:

1. Validate input:
   - chỉ chấp nhận `StorageGatewayContract`
   - file bắt buộc có dữ liệu
   - giới hạn 10MB
   - chỉ cho phép `.json`, `.yaml`, `.yml`
   - `SourceType` chỉ được là `OpenAPI` hoặc `Postman`
2. Đọc toàn bộ file content vào memory để validate nội dung.
3. Nếu `SourceType = OpenAPI`, kiểm tra file có dấu hiệu `openapi` hoặc `swagger`.
4. Nạp `Project`, kiểm tra project tồn tại và đúng owner.
5. Trừ quota storage qua `ISubscriptionLimitGatewayService`.
6. Upload file vật lý sang Storage module qua `IStorageFileGatewayService.UploadAsync(...)`.

### Nhánh parse ngay trong lúc upload

Code hiện tại có một nhánh đặc biệt:

- nếu là `OpenAPI`
- và file là `.json`

thì `UploadApiSpecificationCommandHandler` sẽ parse nhanh ngay trong handler bằng hàm private `ParseOpenApiJsonEndpoints(...)`.

Nhánh parse nhanh này sẽ:

1. đọc `paths`,
2. duyệt từng method,
3. map parameter,
4. map request body,
5. map response,
6. tạo danh sách `ParsedEndpoint`.

Nếu parse nhanh thành công:

- `ParseStatus = Success`
- endpoint/parameter/response sẽ được persist luôn cùng lúc với `ApiSpecification`

Nếu parse nhanh thất bại:

- `ParseStatus = Failed`
- lỗi được ghi vào `ParseErrors`

Nếu không rơi vào nhánh parse nhanh này, spec thường sẽ đi vào trạng thái `Pending` để chờ parse nền.

### Kết quả của bước upload

Sau khi xong, handler tạo:

- `ApiSpecification`
- các `ApiEndpoint`
- các `EndpointParameter`
- các `EndpointResponse`

và nếu `AutoActivate = true` thì spec mới sẽ được gắn làm active spec của project.

---

## 2.2. Bước B - Outbox event và parse nền

Ngay sau khi `ApiSpecification` được tạo, `SpecCreatedEventHandler` sẽ ghi outbox event `SPEC_UPLOADED`.

Từ đây flow nền tiếp tục như sau:

1. `PublishEventWorker` chạy nền
2. worker dispatch `PublishEventsCommand`
3. `SpecOutboxMessagePublisher` nhận event `SPEC_UPLOADED`
4. publisher dispatch `ParseUploadedSpecificationCommand`

### `ParseUploadedSpecificationCommandHandler` làm gì

Đây là luồng parse chuẩn hóa ở tầng background.

Handler thực hiện:

1. Load `ApiSpecification`
2. Chỉ tiếp tục nếu `ParseStatus == Pending`
3. Kiểm tra spec có `OriginalFileId`
4. Chọn parser phù hợp từ `IEnumerable<ISpecificationParser>`
5. Download file từ Storage module
6. Parse file thật bằng parser được chọn
7. Nếu parse fail thì set `ParseStatus = Failed`
8. Nếu parse success thì xóa dữ liệu con cũ và ghi lại dữ liệu mới theo kiểu replace-all

### Idempotency rất quan trọng

Ở đây có một nuance phải hiểu đúng:

- nếu upload JSON OpenAPI đã parse nhanh và đã set `ParseStatus = Success` từ trước,
- khi outbox chạy tới `ParseUploadedSpecificationCommand`,
- handler sẽ **skip** vì spec không còn ở trạng thái `Pending`.

Nói ngắn gọn:

- **nhánh upload nhanh** dùng cho OpenAPI JSON
- **nhánh outbox/background** dùng cho các spec còn `Pending`

---

## 2.3. Bước C - Parser OpenAPI bóc tách spec thành dữ liệu cấu trúc

Parser chính là `OpenApiSpecificationParser` trong `ClassifiedAds.Modules.ApiDocumentation/Services/OpenApiSpecificationParser.cs`.

### Parser này được gọi khi nào

Nó được gọi từ `ParseUploadedSpecificationCommandHandler` sau khi handler:

1. download file xong,
2. xác định `SourceType = OpenAPI`,
3. chọn parser qua `CanParse(SourceType.OpenAPI)`.

### Parser này làm gì

Nó đọc bytes file, parse JSON, rồi tạo ra `SpecificationParseResult` gồm:

- `Endpoints`
- `SecuritySchemes`
- `DetectedVersion`
- `Errors`

### Các bước parse cụ thể

1. Kiểm tra document có key `swagger` hoặc `openapi`.
2. Đọc `info.version`.
3. Parse security schemes:
   - Swagger 2 dùng `securityDefinitions`
   - OpenAPI 3 dùng `components.securitySchemes`
4. Duyệt `paths`.
5. Với mỗi operation:
   - đọc `operationId`, `summary`, `description`, `deprecated`, `tags`
   - merge path-level parameters và operation-level parameters
   - map `requestBody` cho OpenAPI 3
   - map response schema cho từng status code
   - map security requirements
6. Resolve `$ref` đệ quy bằng `ResolveSchemaJson(...)`.

### Điểm quan trọng nhất của parser

Điểm mạnh lớn nhất của parser này là nó không chỉ giữ `$ref` nguyên bản, mà còn có khả năng:

- follow `#/components/schemas/...`
- inline schema được tham chiếu
- tiếp tục resolve nested `$ref` trong:
  - `properties`
  - `items`
  - `allOf`
  - `oneOf`
  - `anyOf`

Điều này rất quan trọng vì toàn bộ tầng dependency analysis và prompt generation ở phía sau đều dựa nhiều vào payload schema đã được resolve.

---

## 2.4. Bước D - Tạo test suite và chốt phạm vi endpoint

Sau khi spec đã có endpoint, người dùng tạo test suite qua:

`POST /api/projects/{projectId}/test-suites`

Controller gọi `AddUpdateTestSuiteScopeCommandHandler`.

### Handler này làm gì

1. Validate:
   - `ProjectId`
   - `CurrentUserId`
   - `ApiSpecId`
   - `Name`
   - phải chọn ít nhất một endpoint
2. Gọi `IApiEndpointMetadataService.GetEndpointMetadataAsync(...)` để kiểm tra toàn bộ endpoint được chọn thật sự thuộc spec đang dùng.
3. Lưu `TestSuite` với:
   - `ApiSpecId`
   - `SelectedEndpointIds`
   - `EndpointBusinessContexts`
   - `GlobalBusinessRules`
   - `Status = Draft`

Từ thời điểm này hệ thống đã có đủ:

- spec gốc
- danh sách endpoint được chọn
- business context

để bắt đầu tính thứ tự phụ thuộc.

---

## 2.5. Bước E - Lấy endpoint metadata và dựng dependency nền

Trước khi chạy thuật toán order chính, hệ thống lấy metadata từ `ApiEndpointMetadataService.GetEndpointMetadataAsync(...)`.

Service này là tầng tiền xử lý rất quan trọng vì nó dựng trước các dependency "thô" dựa trên dữ liệu endpoint/parameter/response/security đã parse.

### Service này trả về gì

Nó tạo ra `ApiEndpointMetadataDto` cho từng endpoint, gồm:

- `EndpointId`
- `HttpMethod`
- `Path`
- `OperationId`
- `IsAuthRelated`
- `DependsOnEndpointIds`
- `ParameterSchemaRefs`
- `ResponseSchemaRefs`
- `ParameterSchemaPayloads`
- `ResponseSchemaPayloads`

### 5 rule dependency được tính sẵn ở đây

#### Rule 1 - Resource producer rule

Nếu endpoint có path item như `/users/{id}` hoặc `/orders/{orderId}` và bản thân nó không phải `POST`, thì nó sẽ phụ thuộc vào `POST /users` hoặc `POST /orders` tương ứng.

Mục tiêu:

- `POST /users` phải chạy trước `GET /users/{id}`
- `POST /orders` phải chạy trước `PUT /orders/{id}`

#### Rule 2 - Operation-schema dependency

Nếu request schema của endpoint A cần schema mà response schema của endpoint B tạo ra, thì A phụ thuộc B.

Ví dụ:

- request của `POST /orders` dùng `Customer`
- response của `POST /customers` sinh ra `Customer`
- khi đó order flow sẽ nghiêng về `POST /customers` trước rồi mới `POST /orders`

#### Rule 3 - Semantic token dependency

Service tách token từ:

- path parameter
- parameter name
- token tài nguyên trong path của producer

để tìm ra quan hệ kiểu:

- `userId` có thể phụ thuộc `/users`
- `categoryIds` có thể phụ thuộc `/categories`

#### Rule 4 - Auth bootstrap rule

Nếu endpoint cần security, hệ thống buộc nó phụ thuộc vào endpoint auth bootstrap gần nhất, thường là login/signin.

#### Rule 5 - Auth chain rule

Các endpoint auth-related sẽ tự nối thành chuỗi bootstrap:

- `register`
- rồi `login`
- rồi các auth step khác

Nhờ vậy hệ thống tránh chuyện gọi login trước khi có account.

---

## 2.6. Bước F - Đề xuất thứ tự endpoint

Điểm vào người dùng gọi là:

`POST /api/test-suites/{suiteId}/order-proposals`

Flow trong backend là:

1. `TestOrderController.Propose()`
2. `ProposeApiTestOrderCommandHandler`
3. `ApiTestOrderService.BuildProposalOrderAsync(...)`
4. `IApiEndpointMetadataService.GetEndpointMetadataAsync(...)`
5. `ApiTestOrderAlgorithm.BuildProposalOrder(...)`

### Điều kiện trước khi đề xuất order

`ProposeApiTestOrderCommandHandler` sẽ:

1. kiểm tra suite tồn tại,
2. kiểm tra quyền owner,
3. kiểm tra `SpecificationId` truyền vào khớp với `suite.ApiSpecId`,
4. nếu request không truyền `SelectedEndpointIds` thì fallback sang `suite.SelectedEndpointIds`.

Sau đó nó gọi `ApiTestOrderService`, nhận về thứ tự đã sắp xếp và lưu thành `TestOrderProposal` với trạng thái `Pending`.

---

## 3. Các thuật toán được gọi khi nào và làm gì

## 3.1. `SchemaRelationshipAnalyzer`

### Được gọi khi nào

Được gọi trong `ApiTestOrderAlgorithm.BuildProposalOrder(...)`, ở bước:

- gom schema payload từ endpoint metadata
- tìm thêm cạnh phụ thuộc ngoài những rule đã được tính sẵn

### Input

- `ParameterSchemaRefs` của từng endpoint
- `ResponseSchemaRefs` của từng endpoint
- tất cả `ParameterSchemaPayloads` và `ResponseSchemaPayloads`

### Output

- danh sách `DependencyEdge` kiểu `SchemaSchema`

### Thuật toán bên trong

#### Bước 1 - Trích `$ref`

`ExtractSchemaRefsFromPayload(...)` dùng regex để tìm tất cả schema ref như:

- `#/components/schemas/User`
- `#/definitions/Order`

#### Bước 2 - Dựng đồ thị schema

`BuildSchemaReferenceGraphLegacy(...)` tạo graph schema nào liên hệ schema nào.

#### Bước 3 - Tính phụ thuộc bắc cầu

`ComputeTransitiveClosure(...)` dùng kiểu Warshall để biết:

- nếu `A -> B`
- và `B -> C`
- thì suy ra `A -> C`

Ý nghĩa:

request schema có thể không tham chiếu trực tiếp schema producer, nhưng lại đi qua chuỗi tham chiếu nhiều tầng.

#### Bước 4 - Sinh edge operation-level

`FindTransitiveSchemaDependencies(...)` biến phụ thuộc ở mức schema thành phụ thuộc ở mức endpoint:

- endpoint consumer cần schema X
- schema X transitively chạm tới schema Y
- schema Y được producer trả về
- vậy consumer phụ thuộc producer

#### Bước 5 - Fuzzy schema name matching

`FindFuzzySchemaNameDependencies(...)` xử lý các trường hợp schema không trùng tên tuyệt đối nhưng cùng "gốc nghĩa".

Ví dụ:

- `CreateUserRequest`
- `UserResponse`

cùng rút về base name là `User`.

### Vì sao cần thuật toán này

Nếu chỉ nhìn path hoặc method thì sẽ bỏ sót các phụ thuộc nằm trong schema payload. Thuật toán này kéo dependency từ tầng schema lên tầng operation.

---

## 3.2. `SemanticTokenMatcher`

### Được gọi khi nào

Được gọi trong `ApiTestOrderAlgorithm.BuildProposalOrder(...)` sau khi đã có pre-computed dependency và schema dependency.

### Input

- token phía consumer:
  - path param
  - parameter name nếu có
  - base name rút ra từ schema ref
- token phía producer:
  - resource token của path `POST/PUT`

### Output

- danh sách `DependencyEdge` kiểu `SemanticToken`

### Luật chấm điểm

`SemanticTokenMatcher.Match(...)` so khớp theo pipeline ưu tiên:

1. exact match: `1.00`
2. plural/singular: `0.95`
3. abbreviation expansion: `0.85`
4. stem match: `0.80`
5. substring: `0.70`

Trong `ApiTestOrderAlgorithm`, chỉ những match có score `>= 0.80` mới được dùng để tạo edge.

### Ví dụ

- `usrId` và `users`
- `categoryIds` và `categories`
- `org` và `organization`

### Vì sao cần thuật toán này

API thật thường không đặt tên hoàn hảo. Có nhiều tài liệu dùng viết tắt, số nhiều, hoặc naming không đồng nhất. Semantic matcher giúp bắt lại các quan hệ mà exact match sẽ bỏ sót.

---

## 3.3. `DependencyAwareTopologicalSorter`

### Được gọi khi nào

Được gọi ở bước cuối của `ApiTestOrderAlgorithm`, sau khi toàn bộ edge đã được gom xong.

### Input

- `SortableOperation`
- toàn bộ `DependencyEdge`

### Output

- danh sách endpoint đã có `OrderIndex`
- `Dependencies`
- `ReasonCodes`

### Logic sắp xếp

Sorter này là phiên bản Kahn topological sort có thêm heuristic.

#### Bước 1 - Xây graph

Graph gồm:

- `dependenciesMap`: tôi phụ thuộc ai
- `dependentsMap`: ai phụ thuộc tôi

Chỉ edge có `Confidence >= 0.5` mới thực sự ép thứ tự.

#### Bước 2 - Tính `inDegree`

Node nào `inDegree = 0` là node đủ điều kiện chạy trước.

#### Bước 3 - Ưu tiên chọn node "tốt nhất"

Khi có nhiều node cùng sẵn sàng, sorter ưu tiên theo thứ tự:

1. auth-related trước
2. fan-out cao trước
3. in-degree thấp hơn
4. HTTP method weight:
   - `POST`
   - `PUT`
   - `PATCH`
   - `GET`
   - `DELETE`
5. path alphabet
6. GUID để đảm bảo deterministic

#### Bước 4 - Cycle break fallback

Nếu không còn node nào có `inDegree = 0`, nghĩa là graph có vòng. Sorter sẽ chọn node có:

- `inDegree` thấp nhất
- `fanOut` cao nhất

để phá vòng một cách có kiểm soát.

### Vì sao cần thuật toán này

Topological sort thường chỉ giải được "phụ thuộc cứng". Bản nâng cấp này còn ưu tiên producer/auth bootstrap lên đầu để tăng khả năng chuỗi test sinh ra chạy thành công ngoài thực tế.

---

## 3.4. `ObservationConfirmationPromptBuilder`

### Được gọi khi nào

Được gọi ở 3 chỗ chính:

1. `HappyPathTestCaseGenerator`
2. `TestGenerationPayloadBuilder` cho unified callback mode
3. `LlmScenarioSuggester` cho boundary/negative suggestion

### Vai trò

Nó không sắp thứ tự endpoint, mà sắp thứ tự **tư duy của LLM**.

Mục tiêu:

- buộc LLM quan sát spec trước
- rồi xác nhận từng constraint bằng evidence
- rồi mới sinh expectation/test case

### 3 lớp prompt được tạo

1. `ObservationPrompt`
2. `ConfirmationPromptTemplate`
3. `CombinedPrompt`

Ngoài ra còn có `SystemPrompt`.

### Logic bên trong

#### Bước 1 - `BuildSpecBlock(...)`

Gom toàn bộ ngữ cảnh endpoint:

- method
- path
- operationId
- summary/description
- parameters
- request schema
- response schema
- examples
- business rules do user nhập

#### Bước 2 - `BuildObservationPrompt(...)`

Yêu cầu LLM liệt kê tất cả constraint có thể test được.

#### Bước 3 - `BuildConfirmationPromptTemplate(...)`

Yêu cầu LLM giữ lại chỉ những constraint có bằng chứng thật trong spec/business rules.

#### Bước 4 - `BuildCombinedPrompt(...)`

Dùng cho model/workflow cần single-shot.

#### Bước 5 - `BuildCrossEndpointContext(...)`

Nếu endpoint hiện tại nằm sau các endpoint liên quan trước đó, prompt builder sẽ append cross-endpoint context.

Ví dụ:

- `POST /users`
- rồi `GET /users/{id}`

thì prompt của endpoint sau sẽ biết endpoint trước có thể đã tạo dữ liệu đầu vào.

### Vì sao cần thuật toán này

Điểm yếu lớn nhất của LLM là "bịa thêm". Prompt builder này dùng cấu trúc observation -> confirmation để giảm hallucination và ép output bám spec hơn.

---

## 3.5. `GeneratedTestCaseDependencyEnricher`

### Được gọi khi nào

Được gọi sau khi generator đã tạo xong test case nhưng trước khi persist:

1. trong `GenerateHappyPathTestCasesCommandHandler`
2. trong `GenerateBoundaryNegativeTestCasesCommandHandler`

### Vai trò

LLM hoặc rule engine có thể sinh request đúng về mặt hình thức nhưng vẫn thiếu:

- dependency link giữa test case
- route param placeholder
- variable extraction từ producer

Enricher là tầng vá lại những phần này.

### Nó làm gì

1. xây danh sách producer candidate từ các happy-path case
2. thêm dependency link theo `approvedOrder`
3. đọc route token từ path như `{userId}`
4. nếu request hiện tại chưa có giá trị cho token đó, tìm producer phù hợp nhất
5. tạo biến extract ở producer nếu cần
6. gắn placeholder vào consumer, ví dụ `{{userId}}`

### Cách chọn producer

Nó chấm producer theo:

1. tài nguyên ở path có trùng resource đích không
2. token sau khi bỏ hậu tố `Id/Ids`
3. producer có phải happy-path không
4. method priority

### Vì sao cần thuật toán này

Nếu không có bước này, test case rất dễ bị đúng schema nhưng sai execution chain, đặc biệt với URL dạng:

- `/users/{userId}`
- `/orders/{orderId}`

---

## 3.6. `PathParameterTemplateService.GenerateMutations(...)`

### Được gọi khi nào

Chỉ được gọi trong flow:

`POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`

và chỉ khi `IncludePathMutations = true`.

### Tầng gọi thực tế

`BoundaryNegativeTestCaseGenerator` gọi `IPathParameterMutationGatewayService`, gateway này chỉ bọc lại `PathParameterTemplateService.GenerateMutations(...)` từ module `ApiDocumentation`.

### Thuật toán sinh mutation

Mỗi path param sẽ được sinh một tập mutation dựa trên type/format:

#### Mutation chung cho mọi type

- empty
- specialChars
- sqlInjection
- nonExistent

#### Integer/long

- wrongType
- zero
- negative
- float instead of integer
- max int32 / overflow int32
- max int64

#### Number

- wrongType
- zero
- negative
- very large
- very small

#### Boolean

- wrongType
- invalid numeric boolean

#### UUID/string

- invalid format
- partial UUID
- all-zero UUID

### Vì sao cần thuật toán này

Đây là nguồn sinh boundary/negative mang tính deterministic, không cần chờ LLM và rất phù hợp cho các case validate input.

---

## 3.7. `BodyMutationEngine`

### Được gọi khi nào

Chỉ trong flow boundary/negative và chỉ khi `IncludeBodyMutations = true`.

`BoundaryNegativeTestCaseGenerator` sẽ tạo `BodyMutationContext` rồi gọi:

`BodyMutationEngine.GenerateMutations(...)`

### Điều kiện áp dụng

Engine này chỉ áp dụng cho:

- `POST`
- `PUT`
- `PATCH`

GET/DELETE/HEAD/OPTIONS sẽ không sinh body mutation.

### Các nhóm mutation chính

#### Whole-body mutations

- `null`
- empty string
- empty JSON object
- malformed JSON
- plain text thay vì JSON

#### Per-field mutations

- thiếu field bắt buộc
- sai kiểu dữ liệu
- overflow / very large value
- invalid enum

#### Schema-based fallback

Nếu có raw request schema, engine còn đọc `required` từ schema để sinh thêm case thiếu field kể cả khi parameter detail chưa bóc đủ.

### Vì sao cần thuật toán này

Nó giúp tạo nhanh những negative/boundary case "máy móc nhưng đúng hướng", đặc biệt hiệu quả với API có validate body chặt.

---

## 3.8. `LlmScenarioSuggester`

### Được gọi khi nào

Chỉ trong flow boundary/negative và chỉ khi `IncludeLlmSuggestions = true`.

### Vai trò

Đây là tầng dùng LLM để sinh các scenario mà rule-based mutation không tự nghĩ ra tốt, ví dụ:

- sai auth context
- forbidden access
- logic kết hợp nhiều field
- business-rule negative case

### Pipeline bên trong

1. optional dependency-aware ordering
2. optional feedback context
3. cache lookup
4. build prompt bằng `ObservationConfirmationPromptBuilder`
5. gọi n8n webhook
6. parse scenario
7. adaptive coverage
8. cache kết quả

### Ý nghĩa

Rule-based mutation mạnh ở input validation. LLM suggester mạnh ở scenario giàu ngữ cảnh nghiệp vụ.

---

## 4. Từ proposal sang approved order

Sau khi `ApiTestOrderAlgorithm` trả về kết quả, `ProposeApiTestOrderCommandHandler` lưu một `TestOrderProposal` ở trạng thái `Pending`.

Người dùng có 2 khả năng:

1. giữ nguyên rồi approve
2. reorder trước rồi approve

### Approve hoạt động thế nào

`ApproveApiTestOrderCommandHandler` sẽ:

1. load proposal
2. kiểm tra quyền
3. kiểm tra row version
4. lấy `finalOrder`:
   - ưu tiên `UserModifiedOrder`
   - nếu không có thì dùng `ProposedOrder`
5. ghi `AppliedOrder`
6. cập nhật:
   - `proposal.Status = Approved` hoặc `ModifiedAndApproved`
   - `suite.ApprovalStatus = Approved` hoặc `ModifiedAndApproved`

### Gate trước khi generate

Từ thời điểm này `ApiTestOrderGateService.RequireApprovedOrderAsync(...)` mới cho phép generate test case.

Nếu chưa approve, mọi API generate đều bị chặn với lỗi:

- `ORDER_CONFIRMATION_REQUIRED`

---

## 5. Bước G - Sinh happy-path test case

Điểm vào:

`POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`

### Flow đầy đủ

1. `TestCasesController.GenerateHappyPath()`
2. `GenerateHappyPathTestCasesCommandHandler`
3. gate check qua `IApiTestOrderGateService`
4. kiểm tra existing happy-path cases
5. kiểm tra subscription limit
6. gọi `HappyPathTestCaseGenerator.GenerateAsync(...)`

### `HappyPathTestCaseGenerator` làm gì

#### Bước 1 - Lấy metadata theo đúng approved order

Nó gọi lại `IApiEndpointMetadataService.GetEndpointMetadataAsync(...)`, nhưng lần này chỉ lấy đúng các endpoint đã approved và giữ nguyên thứ tự đó.

#### Bước 2 - Map sang `EndpointPromptContext`

`EndpointPromptContextMapper` tạo prompt context gồm:

- request body schema
- response schema
- parameter prompt context
- response prompt context
- global business rules
- endpoint-specific business rules

#### Bước 3 - Build prompt

Gọi `ObservationConfirmationPromptBuilder.BuildForSequence(...)`.

#### Bước 4 - Build payload gửi n8n

Payload gồm:

- suite info
- endpoint ordered list
- prompt từng endpoint
- dependency info
- schema payloads

#### Bước 5 - Gọi webhook

`N8nIntegrationService.TriggerWebhookAsync(...)` gọi webhook `generate-happy-path`.

#### Bước 6 - Parse response n8n

Response được map sang:

- `TestCase`
- `TestCaseRequest`
- `TestCaseExpectation`
- `TestCaseVariable`

#### Bước 7 - Nối dependency chain

`WireDependencyChains(...)` gắn dependency giữa các test case dựa trên `DependsOnEndpointIds` của approved order.

#### Bước 8 - Enrich thêm dependency và route param

`GeneratedTestCaseDependencyEnricher.Enrich(...)` chạy thêm một lượt để vá missing link, missing variable, missing route placeholders.

#### Bước 9 - Persist

Handler lưu:

- test case
- request
- expectation
- variable
- dependency
- changelog
- version snapshot

và cuối cùng:

- tăng `suite.Version`
- set `suite.Status = Ready`

---

## 6. Bước H - Sinh test theo callback/unified workflow

Nếu `N8nIntegrationOptions.UseDotnetIntegrationWorkflowForGeneration = true` thì controller không gọi trực tiếp `GenerateHappyPathTestCasesCommand`.

Thay vào đó nó chuyển sang luồng async/callback:

1. `GenerateTestCasesCommandHandler`
2. tạo `TestGenerationJob` trạng thái `Queued`
3. gửi `TriggerTestGenerationMessage` lên message bus
4. `TriggerTestGenerationConsumer` nhận message
5. consumer build payload qua `TestGenerationPayloadBuilder`
6. consumer gọi webhook unified của n8n
7. nếu trigger thành công, job sang `WaitingForCallback`
8. n8n gọi ngược lại:
   `POST /api/test-suites/{suiteId}/test-cases/from-ai`
9. `TestOrderController.ReceiveAiGeneratedTestCases(...)` validate `x-callback-api-key`
10. dispatch `SaveAiGeneratedTestCasesCommand`
11. command lưu test case, request, expectation, variable, version snapshot
12. cập nhật job sang `Completed`
13. set `suite.Status = Ready`

### Vai trò của `TestGenerationPayloadBuilder`

Builder này vẫn dùng:

- approved order
- `ObservationConfirmationPromptBuilder`
- schema payload
- business rules

nhưng đóng gói thành một payload thống nhất cho workflow callback.

---

## 7. Bước I - Sinh boundary/negative test case

Điểm vào:

`POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`

### Flow đầy đủ

1. `GenerateBoundaryNegativeTestCasesCommandHandler`
2. gate check approved order
3. kiểm tra existing boundary/negative cases
4. kiểm tra limit test case
5. nếu bật LLM suggestion thì kiểm tra thêm limit LLM call
6. gọi `BoundaryNegativeTestCaseGenerator.GenerateAsync(...)`
7. enrich dependency bằng `GeneratedTestCaseDependencyEnricher`
8. persist toàn bộ dữ liệu
9. set `suite.Status = Ready`

### `BoundaryNegativeTestCaseGenerator` làm gì

#### Bước 1 - Lấy endpoint metadata

Gọi `IApiEndpointMetadataService`.

#### Bước 2 - Lấy parameter detail

Gọi `IApiEndpointParameterDetailService`.

#### Bước 3 - Rule-based generation

Cho từng endpoint:

- path mutation qua `IPathParameterMutationGatewayService`
- body mutation qua `IBodyMutationEngine`

#### Bước 4 - LLM suggestion

Nếu bật `IncludeLlmSuggestions`, generator gọi `ILlmScenarioSuggester`.

#### Bước 5 - Materialize thành test case

Scenario LLM được convert thành entity qua `ILlmSuggestionMaterializer`.

#### Bước 6 - Gán `OrderIndex`

Toàn bộ case được đánh số tuần tự trước khi trả về handler.

---

## 8. Khi nào từng thuật toán được gọi trong full flow

| Thời điểm | Thành phần/thuật toán | Vai trò |
|---|---|---|
| Upload spec | `UploadApiSpecificationCommandHandler.ParseOpenApiJsonEndpoints` | Parse nhanh OpenAPI JSON ngay lúc upload |
| Parse nền | `OpenApiSpecificationParser` | Parse chuẩn hóa file spec thành endpoint/param/response/security |
| Chuẩn bị order | `ApiEndpointMetadataService` | Tính dependency nền theo 5 rule |
| Đề xuất order | `SchemaRelationshipAnalyzer` | Tìm dependency schema-schema và fuzzy schema |
| Đề xuất order | `SemanticTokenMatcher` | Tìm dependency theo ngữ nghĩa token |
| Đề xuất order | `DependencyAwareTopologicalSorter` | Sắp thứ tự endpoint cuối cùng |
| Trước khi gọi LLM | `ObservationConfirmationPromptBuilder` | Tạo prompt giảm hallucination |
| Sau khi LLM sinh happy-path | `GeneratedTestCaseDependencyEnricher` | Vá dependency + route param placeholder |
| Boundary/negative | `PathParameterTemplateService` | Sinh mutation cho path param |
| Boundary/negative | `BodyMutationEngine` | Sinh mutation cho request body |
| Boundary/negative | `LlmScenarioSuggester` | Sinh scenario khó bằng LLM |

---

## 9. Trạng thái kết thúc của flow

Khi flow thành công, kết quả cuối cùng trong DB là:

1. `ApiSpecification` đã có dữ liệu parse
2. `TestSuite` đã có:
   - selected endpoints
   - approved order
   - version tăng
   - `Status = Ready`
3. `TestCase` đã được persist
4. `TestCaseRequest`, `TestCaseExpectation`, `TestCaseVariable`, `TestCaseDependency` đã được persist
5. `TestSuiteVersion` đã được chụp snapshot

Tại thời điểm này hệ thống đã hoàn tất luồng "từ thả file Swagger tới khi sinh xong test case" và sẵn sàng chuyển sang pha execution.

---

## 10. Kết luận ngắn gọn

Full flow thật sự của hệ thống không phải chỉ là:

`Swagger -> parse -> LLM -> test case`

Mà là một chuỗi chặt chẽ hơn:

`Upload file -> validate -> upload storage -> parse spec -> tạo test suite -> lấy metadata -> dựng dependency -> đề xuất order -> review/approve -> build prompt -> gọi n8n/LLM hoặc rule engine -> enrich dependency/variables -> persist -> suite Ready`

Điểm quan trọng nhất của thiết kế hiện tại là:

1. **dependency được tính trước khi gọi LLM**
2. **LLM chỉ sinh nội dung test, không quyết định toàn bộ execution graph**
3. **sau khi LLM trả về vẫn còn một lớp hậu xử lý để vá dependency và route param**
4. **mọi luồng generate đều bị chặn bởi gate approved order**

Đó là lý do vì sao hệ thống vừa tận dụng được AI, vừa giữ được sự ổn định của execution chain.
