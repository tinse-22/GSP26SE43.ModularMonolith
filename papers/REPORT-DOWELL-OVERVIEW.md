# Overview Luồng Upload Swagger Đến Sinh Test

## Mục tiêu

File này là bản **tóm tắt ngắn** của luồng từ lúc upload Swagger/OpenAPI đến lúc hệ thống sinh xong test case và đưa `TestSuite` về trạng thái `Ready`.

---

## Luồng tổng quát

1. Người dùng upload Swagger/OpenAPI:
   `POST /api/projects/{projectId}/specifications/upload`
2. Hệ thống validate file, upload sang Storage, tạo `ApiSpecification`.
3. Spec được parse để tách ra:
   - endpoint
   - parameter
   - response
   - security scheme
4. Người dùng tạo test suite:
   `POST /api/projects/{projectId}/test-suites`
5. Test suite lưu:
   - spec đang dùng
   - endpoint được chọn
   - business rules
6. Hệ thống đề xuất thứ tự endpoint:
   `POST /api/test-suites/{suiteId}/order-proposals`
7. Người dùng review, có thể reorder, rồi approve proposal.
8. Sau khi approve, gate mở cho phép generate test case.
9. Hệ thống sinh:
   - happy-path
   - boundary/negative
   - hoặc unified callback workflow qua n8n
10. Test case được enrich dependency, variable, route-param placeholder rồi lưu DB.
11. `TestSuite.Status` được cập nhật thành `Ready`.

---

## Code được gọi cho từng ý trong luồng tổng quát

1. Upload Swagger/OpenAPI

  Điểm vào HTTP của bước này là `SpecificationsController.Upload()` trong `ClassifiedAds.Modules.ApiDocumentation/Controllers/SpecificationsController.cs`. Method này nhận `multipart/form-data` từ route `POST /api/projects/{projectId}/specifications/upload`, map dữ liệu request thành `UploadApiSpecificationCommand`, rồi dùng dispatcher chuyển control sang `UploadApiSpecificationCommandHandler` trong `ClassifiedAds.Modules.ApiDocumentation/Commands/UploadApiSpecificationCommand.cs`. Nói ngắn gọn, controller chỉ là cửa vào; toàn bộ xử lý nghiệp vụ thật sự bắt đầu từ command handler này.

2. Validate file, upload Storage, tạo `ApiSpecification`

  Logic chính của bước này nằm trong `UploadApiSpecificationCommandHandler.HandleAsync(...)`. Handler đọc file, kiểm tra extension, dung lượng, `SourceType`, tính hợp lệ của nội dung OpenAPI/Postman, sau đó gọi `IStorageFileGatewayService.UploadAsync(...)` để đẩy file gốc sang module Storage. Sau khi upload xong, chính handler này tạo entity `ApiSpecification`, gắn `OriginalFileId`, thông tin version/trạng thái parse, và trong một số trường hợp còn tạo luôn dữ liệu endpoint con ban đầu trước khi commit vào database.

3. Parse spec để tách endpoint, parameter, response, security scheme

  Bước parse này trong code có 2 nhánh. Nhánh thứ nhất là parse nhanh ngay lúc upload: nếu file là OpenAPI JSON phù hợp, `UploadApiSpecificationCommandHandler` sẽ gọi hàm private `ParseOpenApiJsonEndpoints(...)` để duyệt `paths`, bóc từng operation, rồi dựng endpoint, parameter và response ngay trong luồng request. Nhánh thứ hai là nhánh parse nền chuẩn hóa: sau khi spec được tạo, `SpecCreatedEventHandler` ghi outbox event, `SpecOutBoxMessagePublisher` publish event đó, rồi `ParseUploadedSpecificationCommandHandler` được dispatch để download file từ storage và gọi `OpenApiSpecificationParser` trong `ClassifiedAds.Modules.ApiDocumentation/Services/OpenApiSpecificationParser.cs`. Parser này là nơi parse sâu hơn các thành phần như `paths`, `parameters`, `requestBody`, `responses`, `securityDefinitions` hoặc `components.securitySchemes`, rồi trả về cấu trúc dữ liệu để handler persist lại vào DB.

4. Tạo test suite

  Điểm vào của bước tạo suite là controller `TestSuitesController` trong `ClassifiedAds.Modules.TestGeneration/Controllers/TestSuitesController.cs`, tương ứng route `POST /api/projects/{projectId}/test-suites`. Từ request của user, controller tạo command và dispatch sang `AddUpdateTestSuiteScopeCommandHandler` trong `ClassifiedAds.Modules.TestGeneration/Commands/AddUpdateTestSuiteScopeCommand.cs`. Tại đây hệ thống bắt đầu chuyển từ dữ liệu spec sang khái niệm “suite để generate test”.

5. Lưu spec đang dùng, endpoint được chọn, business rules

  `AddUpdateTestSuiteScopeCommandHandler` là nơi chốt scope thật sự của test suite. Handler này kiểm tra `ApiSpecId` có thuộc project hay không, `SelectedEndpointIds` có thật sự nằm trong spec hay không, đồng thời validate user/project ownership trước khi ghi dữ liệu. Sau đó nó persist thông tin suite gồm spec đang dùng, danh sách endpoint được chọn, business rules và các metadata liên quan. Đây là điểm rất quan trọng vì từ bước này trở đi, luồng order proposal và generation không còn đọc “toàn bộ spec” một cách mơ hồ nữa mà đọc lại snapshot scope đã được lưu trong suite.

6. Đề xuất thứ tự endpoint

  Điểm vào HTTP là `TestOrderController.Propose()` trong `ClassifiedAds.Modules.TestGeneration/Controllers/TestOrderController.cs`, route `POST /api/test-suites/{suiteId}/order-proposals`. Controller tạo command rồi đẩy sang `ProposeApiTestOrderCommandHandler`. Trong handler này, hệ thống load suite, xác định tập endpoint cần order, rồi gọi `ApiEndpointMetadataService` để lấy metadata đã parse từ spec. Sau đó `ApiTestOrderService` điều phối `ApiTestOrderAlgorithm` để dựng dependency graph giữa các endpoint, tính thứ tự phù hợp, giải thích reason codes nếu có, và cuối cùng lưu kết quả thành entity `TestOrderProposal` với trạng thái chờ review.

7. Review, reorder, approve proposal

  Các bước review và reorder vẫn đi qua `TestOrderController`, chỉ khác route cụ thể cho reorder hoặc approve. Khi người dùng chốt proposal, controller dispatch sang `ApproveApiTestOrderCommandHandler` trong `ClassifiedAds.Modules.TestGeneration/Commands/ApproveApiTestOrderCommand.cs`. Handler này đọc proposal hiện tại, kiểm tra `rowVersion`, xác định thứ tự cuối cùng sẽ dùng là `UserModifiedOrder` hay `ProposedOrder`, rồi ghi `AppliedOrder` vào proposal. Đồng thời nó cập nhật trạng thái approval của cả proposal và test suite để đánh dấu rằng suite này đã có execution order hợp lệ cho bước generate.

8. Mở gate cho phép generate test case sau khi approve

  Sau khi approve xong, quyền “được phép generate” không mở bằng cờ ở controller mà được kiểm tra tập trung trong `ApiTestOrderGateService` thuộc `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderGateService.cs`. Các handler generate như `GenerateHappyPathTestCasesCommandHandler`, `GenerateBoundaryNegativeTestCasesCommandHandler` và cả luồng unified/callback đều gọi service này trước khi chạy. Nếu suite chưa có approved order hoặc proposal chưa hợp lệ, gate sẽ ném lỗi và dừng flow ngay tại đây.

9. Sinh happy-path, boundary/negative hoặc unified callback qua n8n

  Phần generate có 3 nhánh rõ ràng trong code. Nhánh happy-path đi từ `TestCasesController.GenerateHappyPath()` sang `GenerateHappyPathTestCasesCommandHandler`, rồi handler gọi `HappyPathTestCaseGenerator` để build prompt context, gọi n8n/LLM hoặc workflow tương ứng, parse kết quả và tạo cấu trúc test case. Nhánh boundary/negative đi từ `TestCasesController.GenerateBoundaryNegative()` sang `GenerateBoundaryNegativeTestCasesCommandHandler`, rồi dùng `BoundaryNegativeTestCaseGenerator` để sinh mutation theo parameter/body/path và kết hợp thêm LLM suggestion nếu bật cấu hình đó. Nhánh unified callback bắt đầu ở `TestOrderController.GenerateTests()`, đi vào `GenerateTestCasesCommandHandler`, đẩy message cho `TriggerTestGenerationConsumer`, consumer dùng `TestGenerationPayloadBuilder` để build payload hoàn chỉnh rồi gọi `N8nIntegrationService`. Sau khi n8n xử lý xong, callback nội bộ quay về `TestOrderController.ReceiveAiGeneratedTestCases()` và được persist bởi `SaveAiGeneratedTestCasesCommandHandler`.

10. Enrich dependency, variable, route-param placeholder rồi lưu DB

  Sau khi generator trả về test case thô, hệ thống chưa lưu ngay mà đi qua tầng hậu xử lý `GeneratedTestCaseDependencyEnricher` trong `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs`. Lớp này được gọi từ `GenerateHappyPathTestCasesCommandHandler` và `GenerateBoundaryNegativeTestCasesCommandHandler` để dò các quan hệ producer-consumer giữa test case, thêm dependency link còn thiếu, bổ sung variable extraction ở test case phía trước và chèn placeholder route-param như `{{userId}}` cho test case phía sau. Chỉ sau bước enrich này các entity như `TestCase`, `Request`, `Expectation`, `Variable`, `Dependency` mới được persist để đảm bảo chuỗi test sinh ra có thể chạy nối tiếp được.

11. Cập nhật `TestSuite.Status = Ready`

  Việc chuyển suite sang trạng thái `Ready` không diễn ra ở controller mà được set trực tiếp tại các handler hoàn tất generation. Cụ thể, `GenerateHappyPathTestCasesCommandHandler` trong `ClassifiedAds.Modules.TestGeneration/Commands/GenerateHappyPathTestCasesCommand.cs`, `GenerateBoundaryNegativeTestCasesCommandHandler` trong `ClassifiedAds.Modules.TestGeneration/Commands/GenerateBoundaryNegativeTestCasesCommand.cs`, và `SaveAiGeneratedTestCasesCommandHandler` trong `ClassifiedAds.Modules.TestGeneration/Commands/SaveAiGeneratedTestCasesCommand.cs` đều có đoạn cập nhật `suite.Status = TestSuiteStatus.Ready` sau khi dữ liệu test case đã được lưu thành công. Đây là mốc đánh dấu backend xem suite đã sẵn sàng cho giai đoạn execution.

---

## 3 khối chính của hệ thống

## 1. ApiDocumentation

Khối này xử lý đầu vào từ Swagger/OpenAPI.

Nhiệm vụ:

- nhận file upload
- parse spec
- lưu `ApiSpecification`
- lưu `ApiEndpoint`, `EndpointParameter`, `EndpointResponse`

## 2. TestGeneration

Khối này xử lý:

- chọn endpoint
- tính dependency
- đề xuất thứ tự chạy
- build prompt
- gọi n8n/LLM hoặc rule engine
- tạo `TestCase`

## 3. TestExecution

Khối này không nằm trong flow sinh test ban đầu.

Nó dùng bộ test case đã được tạo để chạy thực tế sau đó.

---

## Thứ tự xử lý chi tiết nhưng ngắn gọn

### Bước 1 - Upload và parse spec

- Controller nhận file upload.
- `UploadApiSpecificationCommandHandler` validate file.
- File được upload sang Storage.
- Nếu là OpenAPI JSON, handler có thể parse nhanh ngay lúc upload.
- Nếu spec còn `Pending`, background outbox sẽ gọi parser chuẩn để parse tiếp.

Kết quả:

- có spec
- có endpoint/param/response trong DB

### Bước 2 - Tạo test suite

- User chọn spec và chọn danh sách endpoint cần test.
- `AddUpdateTestSuiteScopeCommandHandler` kiểm tra endpoint có thật sự thuộc spec hay không.
- Suite được lưu ở trạng thái `Draft`.

### Bước 3 - Tính dependency và đề xuất order

- `ApiEndpointMetadataService` dựng dependency nền.
- `ApiTestOrderAlgorithm` chạy các thuật toán order.
- Kết quả được lưu thành `TestOrderProposal`.

### Bước 4 - Review và approve

- User có thể reorder proposal.
- `ApproveApiTestOrderCommandHandler` chốt `AppliedOrder`.
- Nếu chưa approve thì mọi API generate đều bị chặn.

### Bước 5 - Generate test case

Có 3 hướng:

1. Happy-path sync:
   `generate-happy-path`
2. Boundary/negative sync:
   `generate-boundary-negative`
3. Unified async callback:
   `generate-tests`

### Bước 6 - Persist và hoàn tất

Sau khi generator trả về:

- hệ thống build entity
- nối dependency test case
- bổ sung variable extraction / route placeholder nếu thiếu
- lưu `TestCase`, `Request`, `Expectation`, `Variable`, `Dependency`
- tăng version suite
- set `TestSuite.Status = Ready`

---

## Các thuật toán chính và lúc được gọi

### `SchemaRelationshipAnalyzer`

Được gọi khi build order.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Commands/ProposeApiTestOrderCommand.cs`, tìm `BuildProposalOrderAsync(...)`
- Từ đó sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs`, method `BuildProposalOrderAsync(...)`
- Trong method này gọi `_apiTestOrderAlgorithm.BuildProposalOrder(endpoints)`
- Sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderAlgorithm.cs`, tìm `BuildProposalOrder(...)`
- Trong `BuildProposalOrder(...)` có dòng gọi `FindSchemaSchemaEdges(deduplicatedEndpoints)`
- Ngay bên dưới trong cùng file, method `FindSchemaSchemaEdges(...)` lần lượt gọi:
  - `_schemaAnalyzer.BuildSchemaReferenceGraphLegacy(allSchemaPayloads)`
  - `_schemaAnalyzer.ComputeTransitiveClosure(directGraph)`
  - `_schemaAnalyzer.FindTransitiveSchemaDependencies(...)`
  - `_schemaAnalyzer.FindFuzzySchemaNameDependencies(...)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Algorithms/SchemaRelationshipAnalyzer.cs`

Muốn nhảy thẳng tới thuật toán thì mở `SchemaRelationshipAnalyzer.cs` và Ctrl+F các method:

- `BuildSchemaReferenceGraphLegacy`
- `ComputeTransitiveClosure`
- `FindTransitiveSchemaDependencies`
- `FindFuzzySchemaNameDependencies`

Vai trò:

- đọc schema ref
- tìm quan hệ schema-schema
- suy ra consumer endpoint phụ thuộc producer endpoint nào

### `SemanticTokenMatcher`

Được gọi khi build order.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Commands/ProposeApiTestOrderCommand.cs`, tìm `BuildProposalOrderAsync(...)`
- Sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs`, method `BuildProposalOrderAsync(...)`
- Method này gọi `_apiTestOrderAlgorithm.BuildProposalOrder(endpoints)`
- Sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderAlgorithm.cs`, tìm `BuildProposalOrder(...)`
- Trong `BuildProposalOrder(...)` có dòng gọi `FindSemanticTokenEdges(deduplicatedEndpoints)`
- Trong method `FindSemanticTokenEdges(...)` có dòng gọi `_semanticTokenMatcher.FindMatches(consumerParams, resourceTokens, MinSemanticMatchScore)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Algorithms/SemanticTokenMatcher.cs`

Muốn nhảy thẳng tới thuật toán thì mở `SemanticTokenMatcher.cs` và Ctrl+F method `FindMatches`.

Vai trò:

- so khớp token như `userId` với `/users`
- bắt dependency ngữ nghĩa khi tên không khớp tuyệt đối

### `DependencyAwareTopologicalSorter`

Được gọi ở bước chốt order.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Commands/ProposeApiTestOrderCommand.cs`, tìm `BuildProposalOrderAsync(...)`
- Sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs`, method `BuildProposalOrderAsync(...)`
- Method này gọi `_apiTestOrderAlgorithm.BuildProposalOrder(endpoints)`
- Sang `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderAlgorithm.cs`, tìm `BuildProposalOrder(...)`
- Ở cuối phần build edge có dòng gọi trực tiếp `_topologicalSorter.Sort(sortableOps, allEdges)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Algorithms/DependencyAwareTopologicalSorter.cs`

Muốn nhảy thẳng tới thuật toán thì mở `DependencyAwareTopologicalSorter.cs` và Ctrl+F method `Sort`.

Vai trò:

- topological sort
- ưu tiên auth trước
- ưu tiên producer có fan-out cao trước
- đảm bảo thứ tự ổn định

### `ObservationConfirmationPromptBuilder`

Được gọi trước khi gửi dữ liệu sang LLM/n8n.

Đường đi tới code:

- Happy-path: mở `ClassifiedAds.Modules.TestGeneration/Services/HappyPathTestCaseGenerator.cs`, tìm `GenerateAsync(...)`, trong method có dòng `var prompts = _promptBuilder.BuildForSequence(promptContexts)`
- Unified callback: mở `ClassifiedAds.Modules.TestGeneration/Services/TestGenerationPayloadBuilder.cs`, tìm `BuildPayloadAsync(...)`, trong method có dòng gọi `_promptBuilder.BuildForSequence(promptContexts)`
- Boundary/negative có LLM: mở `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`, tìm `SuggestScenariosAsync(...)`, trong method có dòng gọi `_promptBuilder.BuildForSequence(promptContexts)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Algorithms/ObservationConfirmationPromptBuilder.cs`

Muốn nhảy thẳng tới thuật toán thì mở `ObservationConfirmationPromptBuilder.cs` và Ctrl+F method `BuildForSequence`.

Vai trò:

- build prompt theo kiểu quan sát rồi xác nhận
- giảm hallucination
- ép LLM bám spec hơn

### `GeneratedTestCaseDependencyEnricher`

Được gọi sau khi generator tạo test case.

Đường đi tới code:

- Happy-path: mở `ClassifiedAds.Modules.TestGeneration/Commands/GenerateHappyPathTestCasesCommand.cs`, tìm `HandleAsync(...)`, trong method có dòng `GeneratedTestCaseDependencyEnricher.Enrich(generationResult.TestCases, approvedOrder)`
- Boundary/negative: mở `ClassifiedAds.Modules.TestGeneration/Commands/GenerateBoundaryNegativeTestCasesCommand.cs`, tìm `HandleAsync(...)`, trong method có dòng `GeneratedTestCaseDependencyEnricher.Enrich(generationResult.TestCases, approvedOrder, existingHappyPathCases, existingHappyPathVariables)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Services/GeneratedTestCaseDependencyEnricher.cs`

Muốn nhảy thẳng tới thuật toán thì mở `GeneratedTestCaseDependencyEnricher.cs` và Ctrl+F method `Enrich`.

Vai trò:

- vá dependency link còn thiếu
- thêm route param placeholder như `{{userId}}`
- thêm variable extraction ở producer nếu cần

### `PathParameterTemplateService`

Được gọi trong boundary/negative flow.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`, tìm `GenerateAsync(...)`
- Trong vòng lặp xử lý path param có dòng gọi `_pathMutationService.GenerateMutations(pathParam.Name, pathParam.DataType, pathParam.Format, pathParam.DefaultValue)`
- `_pathMutationService` ở đây đi qua gateway, nên mở tiếp `ClassifiedAds.Modules.ApiDocumentation/Services/PathParameterMutationGatewayService.cs`
- Trong method `GenerateMutations(...)` của gateway có dòng `_pathParamService.GenerateMutations(parameterName, dataType, format, defaultValue)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.ApiDocumentation/Services/PathParameterTemplateService.cs`

Muốn nhảy thẳng tới thuật toán thì mở `PathParameterTemplateService.cs` và Ctrl+F method `GenerateMutations`.

Nghĩa là caller trực tiếp trong flow generate là `BoundaryNegativeTestCaseGenerator`, còn caller thực sự chạm tới thuật toán gốc là `PathParameterMutationGatewayService` rồi sang `PathParameterTemplateService`.

Vai trò:

- sinh mutation cho path param:
  - empty
  - wrong type
  - invalid uuid
  - non-existent resource

### `BodyMutationEngine`

Được gọi trong boundary/negative flow.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`, tìm `GenerateAsync(...)`
- Trong phần xử lý body có dòng `_bodyMutationEngine.GenerateMutations(bodyContext)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Services/BodyMutationEngine.cs`

Muốn nhảy thẳng tới thuật toán thì mở `BodyMutationEngine.cs` và Ctrl+F method `GenerateMutations`.

Vai trò:

- sinh mutation cho request body:
  - missing required field
  - type mismatch
  - overflow
  - malformed JSON
  - invalid enum

### `LlmScenarioSuggester`

Được gọi trong boundary/negative flow nếu bật LLM suggestions.

Đường đi tới code:

- Mở `ClassifiedAds.Modules.TestGeneration/Services/BoundaryNegativeTestCaseGenerator.cs`, tìm `GenerateAsync(...)`
- Trong phần LLM suggestions có dòng `_llmSuggester.SuggestScenariosAsync(llmContext, cancellationToken)`
- Triển khai thuật toán gốc nằm trong file `ClassifiedAds.Modules.TestGeneration/Services/LlmScenarioSuggester.cs`

Muốn nhảy thẳng tới thuật toán thì mở `LlmScenarioSuggester.cs` và Ctrl+F method `SuggestScenariosAsync`.

Vai trò:

- sinh thêm scenario nghiệp vụ mà rule-based mutation không đủ tốt để bao phủ

---

## Ý tưởng cốt lõi của hệ thống

Hệ thống không để LLM quyết định toàn bộ flow.

Thay vào đó:

1. Backend tính dependency trước.
2. Backend chốt thứ tự endpoint trước.
3. User approve order trước.
4. LLM chỉ tham gia ở phần sinh nội dung test.
5. Sau khi LLM trả về vẫn còn một lớp hậu xử lý để sửa chain thực thi.

Vì vậy luồng thực tế là:

`Swagger -> Parse -> Dependency Analysis -> Order Proposal -> Approve -> Generate -> Enrich -> Persist -> Ready`

---

## Kết luận 1 câu

File Swagger chỉ là đầu vào; để ra được test case chạy được, hệ thống phải đi qua 4 tầng bắt buộc: **parse spec, tính dependency, approve order, rồi mới generate và hậu xử lý test case**.
