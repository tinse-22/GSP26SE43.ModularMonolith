# Phân tích Nguồn gốc Nghiên cứu Khoa học của Các Thuật toán Lõi trong Test Generation

Tài liệu này trình bày chi tiết về 4 thuật toán và design pattern phân tích dữ liệu lõi được kiến trúc và sử dụng trong module `ClassifiedAds.Modules.TestGeneration\Algorithms`. Các thuật toán này không sử dụng các phương pháp heuristics thông thường mà kế thừa toàn bộ lý thuyết từ các bài báo cáo khoa học (Research Papers) nổi bật trong ngành Software Engineering (SE) về Application Programming Interface (API) Testing và khả năng lập luận của Trí tuệ nhân tạo (Large Language Models - LLMs).

---

## Nhóm 1: Nhóm thuật toán sắp xếp cấu trúc Test Suite (Workflow Sorting & Dependency)

Mục tiêu chung của nhóm này là xác định trật tự gọi các API sao cho đúng logic ngữ cảnh nghiệp vụ (VD: `POST /users` phải được chạy xong để lấy dữ liệu ID trước khi gọi `GET /users/{id}`).

### 1. `DependencyAwareTopologicalSorter` (Sắp xếp Topo có nhận biết phụ thuộc)

*   **Mục đích trong dự án:** Phân rã và sắp xếp đồ thị các API Call để trích xuất ra một luồng Test Suite có tỷ lệ HTTP 200 (Thành công) cao nhất, chạy theo đúng thứ tự logic tạo lập dữ liệu. Nó cho phép các "luật cứng" (hard-rules) đè lên đồ thị như: API Authentication (VD: Login/Token) luôn phải chạy đầu tiên để thiết lập phiên làm việc, chống lại lỗi Un-Authorized hoặc "chưa có dữ liệu nền".
*   **Cách hoạt động nền tảng:** Thuật toán là một phiên bản tùy biến từ thuật toán Kahn (Kahn's Algorithm for Topological Sorting). Nó đánh trọng số các đỉnh (các API/Endpoints) trên DAG (Directed Acyclic Graph) bằng **In-degree** (số lượng API bắt buộc phải cung cấp dữ liệu cho nó chạy) và ưu tiên sắp xếp break-tie thông qua **Fan-out** (số quyết định chi phối: có bao nhiêu API khác đang chờ lấy dữ liệu từ nó).
*   **Dựa trên Background:** Báo cáo **KAT (Katalon API Testing methodologies / Knowledge-Aware REST API Testing)**.
*   **Báo cáo KAT nói lên điều gì về khía cạnh này?**
    *   Các phương pháp API Testing tự động hoặc Random API Fuzzing thường xuyên chạy API một cách vô tri, không nhận biết trạng thái bối cảnh, hay gặp rủi ro văng lỗi Invalid Request 400 hoặc 404 Not Found do gọi sai trình tự (VD: gọi API lấy chi tiết Item trong khi hệ thống chưa hề có API Create Item nào được bóp cò).
    *   Bài báo nghiên cứu định nghĩa một khái niệm chuẩn mực là **Operation Dependency Graph (ODG - Đồ thị phụ thuộc hoạt động)** được chắt lọc từ đặc tả OpenAPI (Swagger).
    *   Báo cáo chứng minh sức mạnh của mô hình mạng: khi đồ thị hóa được sự phụ thuộc luồng tham số (parameter dependency sequence), hệ thống có thể duyệt qua kiến trúc đồ thị (Graph Traversal) để sinh ra được dữ liệu trải mượt cho tập Test. Việc ứng dụng sắp xếp Topo trong project chính là hiện thực hóa thuật toán ODG Routing này nhằm tìm ra trình tự hợp lý nhất cho mọi Pipeline CI/CD tự động mà không cần can thiệp tay.

### 2. `SchemaRelationshipAnalyzer` (Thuật toán phân tích quan hệ cấu trúc dữ liệu Entity ngầm)

*   **Mục đích trong dự án:** Tự động thám thính và ráp nối để nhận diện hai endpoint có mối quan hệ phụ thuộc lẫn nhau một cách ngầm định dưới lớp Data Cấu trúc Entity (Schema-Schema Dependency) – cho phép hệ thống Test Generation ngầm gán map dữ liệu một cách cực nhạy thông qua kiểu trả về.
*   **Cách hoạt động nền tảng:** Thuật toán tiến hành quét "từ gốc tới ngọn" (recursive traversal) vào các thành phần schema components (các block schemas trong file JSON/Swagger) để bóc tách liên kết tham chiếu `$ref` nội bộ. Sau khi lấy được đồ thị tham chiếu, thuật toán vận dụng Ma trận để tính **Transitive Closure (Bao đóng bắt cầu** theo thuật toán Warshall cổ điển) nhằm truy vết vòng gián tiếp: Input (Request Body) của API A có quan hệ thế nào với Output (Response) gốc của API B, kể cả khi chúng chôn giấu qua 3-4 lớp wrapper.
*   **Dựa trên Background:** Các kỹ thuật khai phá sâu (Deep Mining) trong kiến trúc **KAT**.
*   **Báo cáo KAT nói lên điều gì về khía cạnh này?**
    *   Sự phụ thuộc trên URL (ví dụ `/users/{id}` và `/users` chung Parameter Type) là kiểu tường minh, dễ phân tích. Nhưng **sự phụ thuộc ngầm ở mức cấu trúc (Data Schema Object)** mới là chìa khóa để build Input mượt mà.
    *   Khẳng định: Các REST API hiện đại thường giao tiếp thông qua Object JSON phân tầng sâu (Nested JSON). Có nhiều class cha con kế thừa trong C#/Java. Bằng cách phân tích cấu trúc đối tượng rễ (Root Schema) rồi móc nối liên kết qua quan hệ `$ref` tham chiếu (tính bắt cầu A->B->C), QA automation có thể suy ngược ra thuộc tính `userId` của endpoint C chính là tham chiếu bảng của schema gốc `User` thuộc endpoint A tạo ra. Bước khai quật này là tiền đề bắt buộc để tính toán Graph Edge ở kỹ thuật số 1.

---

## Nhóm 2: Nhóm thuật toán so khớp ngữ nghĩa lỏng lẻo (Fuzzy Semantic Inter-Linking)

### 3. `SemanticTokenMatcher` (Thuật toán so khớp Token liên kết ngữ nghĩa)

*   **Mục đích trong dự án:** Hoàn thiện điểm mù chết người của API Code: Khi dữ liệu OpenAPI/Swagger bị định nghĩa không chính xác, đặt biến bừa bãi không tuân thủ quy chuẩn. Thuật toán phân tích chuỗi này sử dụng Heuristics thông minh để phát hiện ra "Fuzzy Dependency" (VD: API Endpoint A nhả ra Response property là `user_idx`, API Endppoint B yêu cầu truyền Parameter là `userId`, làm sao máy hiểu 2 cái này là 1 để đẩy data qua lại?).
*   **Cách hoạt động nền tảng:** Đây là kỹ thuật NLP (Natural Language Processing). Module chuyển đổi text, tính toán **Similarity Score (Điểm tương đồng Cosine/Semantic)** giữa tên biến tham số dự định truyền vào. Thuật toán vận hành thông qua các Parser token hóa: Cắt gốc hình thái ngữ pháp tiếng Anh (Stemming: mice -> mouse, leaves -> leaf), và đặc biệt cơ sở dữ liệu map từ viết tắt chuẩn mực của developer thế giới (cat = category, usr / u = user, repo = repository) nhằm "nội suy" liên kết data.
*   **Dựa trên Background:** Báo cáo **SPDG (Semantic Property Dependency Graph - Đồ thị phụ thuộc thuộc tính ngữ nghĩa)**.
*   **Báo cáo SPDG nói lên điều gì?**
    *   Cũng xoáy vào điểm yếu Black-Box API testing: Viết code Swagger spec theo định dạng cứng nhắc là không tưởng ở các dự án Outsource nhiều trình độ coder khác nhau (inconsistent naming conventions - Đặt tên không chuẩn). 
    *   SPDG đề xuất một nhánh mới: Tích hợp công nghệ **Phân tích ngữ nghĩa tự nhiên (Semantic string matching and knowledge graphs)** vào không gian API Specs.
    *   Luận điểm chính: Bằng cách đo đạc "khoảng cách ngữ nghĩa" (Scoring Threshold Method) của các thuộc tính API (ví dụ id, uid, reqId, document_id) đối chiếu chéo tên path `/users` -> parameter `uId`. Khả năng tự động dò tìm và liên kết Output sang Input tăng mạnh và tạo ra các bộ Parameter Map gần như tương tác tự nhiên, hoàn toàn bypass được khiếm khuyết đặt sai tên Data Transfer Object (DTO) của coder.

---

## Nhóm 3: Nhóm định hình mẫu lập luận Trí Tuệ Nhân Tạo (AI Chain Logic / GenAI Orchestration)

### 4. `ObservationConfirmationPromptBuilder` (Mẫu Prompt Quan sát - Xác nhận dành cho LLMs)

*   **Mục đích trong dự án:** Xây dựng một hàng rào Prompt Engineering vách sắt tường đồng để "giam lỏng" trí tưởng tượng LLMs, ép AI (như GPT-4) phải sinh ra các Test Case Assertions (Biểu thức kiểm tra ràng buộc kết quả API) một cách **tuyệt đối chính xác và bám sát Specs dự án**, chặn đứng 100% hiện tượng "Sinh huyễn hoặc / Ảo giác (Hallucination)" - sự tự phát minh ra API params/rules sai lệch không hề có trong Swagger.
*   **Cách hoạt động nền tảng:** Hệ thống gò ép luồng suy nghĩ Chain-of-Thought nâng cao theo Prompt chia 2 phân đoạn (tương ứng với Zero-Shot Context injection):
    *   **Phase 1 (Observation - Giai đoạn Quan sát):** Thuật toán yêu cầu LLM đóng vai Data Parser khô khan, đọc vét qua toàn bộ file API Spec và trả về một danh sách thô kệch các "Ràng buộc dữ liệu ngầm" (Ví dụ: Biến này Type String? Format Email? Ràng buộc MaxLength = 50? Enum 3 giá trị cố định? Có Regex đặc biệt?). KHÔNG SUY ĐOÁN.
    *   **Phase 2 (Confirmation - Giai đoạn Chứng minh/Xác nhận):** Trọng tâm của Prompt. LLM nhận lệnh phải trích dẫn lại (Quote Evidence text) đúng một chuỗi ký tự nào đó từ mảng JSON/Spec Definition để đưa ra bằng chứng xác minh Ràng buộc ở Phase 1 là **Tồn tại Hợp Lệ trong Code Thực Tế**. Bất kỳ test case / rule assertion nào không kiếm được Quote từ Spec sẽ bị chém bỏ ngay lập tức tại vòng Filter.
*   **Dựa trên Background:** Báo cáo thuật toán kiến trúc mãng **RBCTest (A framework for leveraging LLMs in API validation testing)** kết hợp mẫu thiết kế Prompt lừng danh **OC Scheme (Observation-Confirmation Prompting Scheme)**.
*   **Báo cáo RBCTest / OS Scheme nói lên điều gì?**
    *   Báo cáo chỉ trích thẳng thừng cách xài AI sơ khai "Hi LLM, give me test cases for this OpenAPI endpoint", nó chỉ tổ sinh ra các Test Script "Rác", phi thực tế, báo Lỗi Fail Validation liên tục do LLM bịa ra quy luật (Oracle Problem).
    *   **RBCTest** là khung làm việc vận dụng sức mạnh LLM để đóng vai trò khai phá "Luật ngầm định" (API Oracles) thông qua phương pháp Tĩnh (Static approach – suy gẫm và phân tích Text Specification) giúp tiết kiệm tài nguyên thay vì code chạy Dynamic Analysis cực đoan.
    *   Phát hiện được công nhận nhất tại hội thảo là tính hữu dụng vượt bậc của **OC Scheme (Observation-Confirmation Scheme)**. Công trình này chứng minh trên số liệu thực tế đồ sộ: Việc cấu trúc não model AI suy luận theo thứ tự: (1) Trích xuất luật theo ngụ ý tự nhận diện (Contextualize) -> (2) Định danh bằng chứng cụ thể phải có trong văn bản nền (Confirmation Presence with textual Quote) — đã kích thích độ chuẩn xác của Bộ Rule Validate trong Test Suite nhảy vọt đụng ngưỡng Precision **86.4% đến 93.6%**. Cách khống chế trí thông minh gen-AI này được đánh giá cao vì nó khoá chặt mọi sai số xác suất và loại bỏ hoàn toàn khả năng "chém gió" vốn có của LLM.

---
**TỔNG KẾT**
Nhờ ứng dụng nhuần nhuyễn bộ ba thuật toán gốc toán học đồ thị / khai phá chuỗi (Toán học Cổ Điển Số 1,2,3) để tính toán "khung xương" vững vàng cho Workflow Test Suite. Sau đó dùng kết quả khung xương ấy làm đầu vào ép chặt cho kỹ thuật điều kiển LLM (Gen-AI Prompting Số 4 RBCTest/OC) để ép độ chính xác khi sinh Test. Kiến trúc của Module TestGeneration trở thành một Engine tự động hóa chuẩn mức Software Engineering hàn lâm cực kỳ bài bản và chuyên nghiệp.
