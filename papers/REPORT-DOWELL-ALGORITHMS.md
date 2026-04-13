# Báo Cáo Chi Tiết Thuật Toán Hệ Thống TestGeneration

Báo cáo này được thiết kế để hỗ trợ việc giải trình và chấm điểm đồ án. Nội dung tập trung vào 4 thuật toán cốt lõi, cơ sở khoa học, vị trí sử dụng trong mã nguồn và cách thức hoạt động trong quy trình thực tế.

---

## I. Tổng Quan Kiến Trúc Thuật Toán

Hệ thống kế thừa các kỹ thuật tiên tiến từ 3 bài báo khoa học (KAT, SPDG, COmbine) để giải quyết bài toán: **"Làm sao để tự động tạo ra một kịch bản kiểm thử API có thứ tự logic và nội dung chính xác?"**

Hệ thống chia làm 2 giai đoạn chính sử dụng thuật toán:
1.  **Giai đoạn Phân tích & Sắp xếp (Static Analysis):** Sử dụng KAT và SPDG để hiểu cấu trúc API.
2.  **Giai đoạn Sinh kịch bản (AI Reasoning):** Sử dụng COmbine để điều khiển AI tạo nội dung.

---

## II. Chi Tiết 4 Thuật Toán Cốt Lõi

### 1. DependencyAwareTopologicalSorter (Sắp xếp cấu trúc phụ thuộc)
*   **Cơ sở khoa học:** KAT paper (Section 4.3).
*   **Vị trí trong Code:** [DependencyAwareTopologicalSorter.cs](file:///d:/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Algorithms/DependencyAwareTopologicalSorter.cs)
*   **Nơi sử dụng:** Được gọi tại `ApiTestOrderAlgorithm.cs` (dòng 111) để ra quyết định cuối cùng về thứ tự API.
*   **Giải thích cho Thầy Cô:**
    *   **Vấn đề:** Sắp xếp Topo thông thường sẽ bị kẹt nếu có vòng lặp hoặc không biết chọn cái nào trước nếu nhiều cái bằng nhau.
    *   **Giải pháp:** Thuật toán sử dụng **Kahn's Algorithm** nhưng cải tiến thêm **Fan-out Ranking**. Những API nào "cung cấp dữ liệu cho nhiều phía" (Fan-out cao) sẽ được đẩy lên trước. Ngoài ra, nó ưu tiên các API Auth và các phương thức `POST` để đảm bảo hệ thống có dữ liệu mồi trước khi thực hiện `GET` hoặc `DELETE`.

### 2. SchemaRelationshipAnalyzer (Phân tích Schema)
*   **Cơ sở khoa học:** KAT paper (Section 4.2).
*   **Vị trí trong Code:** [SchemaRelationshipAnalyzer.cs](file:///d:/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Algorithms/SchemaRelationshipAnalyzer.cs)
*   **Nơi sử dụng:** Được gọi tại `ApiTestOrderAlgorithm.cs` (dòng 92 và 183) để xây dựng đồ thị phụ thuộc giữa các dữ liệu.
*   **Giải thích cho Thầy Cô:**
    *   **Cơ chế:** Thuật toán quét các tham chiếu `$ref` trong tệp OpenAPI. Nếu Schema A chứa Schema B, nó tạo một cạnh phụ thuộc.
    *   **Điểm đặc biệt:** Sử dụng **Fuzzy Name Matching**. Nó tự động rút gọn tên (ví dụ: `CreateUserRequest` -> `User`) để tìm ra các Schema liên quan đến nhau dù không có tham chiếu trực tiếp. Nó cũng dùng thuật toán **Warshall** để tìm phụ thuộc bắc cầu (A->B->C thì A->C).

### 3. SemanticTokenMatcher (Khớp mã ngữ nghĩa)
*   **Cơ sở khoa học:** SPDG paper (Section 3.2).
*   **Vị trí trong Code:** [SemanticTokenMatcher.cs](file:///d:/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Algorithms/SemanticTokenMatcher.cs)
*   **Nơi sử dụng:** Được gọi tại `ApiTestOrderAlgorithm.cs` (dòng 96 và 267) để khớp các trường dữ liệu "giống ý nghĩa nhưng khác tên".
*   **Giải thích cho Thầy Cô:**
    *   **Cơ chế:** Khi so sánh hai trường dữ liệu (ví dụ `userId` và `owner_id`), nó không chỉ so khớp chuỗi. Nó thực hiện các bước:
        1. **Singularization:** Đưa về số ít (`users` -> `user`).
        2. **Abbreviation Expansion:** Giải mã viết tắt (`auth` -> `authentication`).
        3. **Stemming:** Lấy gốc từ (`created` -> `creat`).
    *   **Kết quả:** Hệ thống nhận diện được các mối liên hệ ngữ nghĩa mà các công cụ quét mã thông thường sẽ bỏ lỡ.

### 4. ObservationConfirmationPromptBuilder (Hệ thống Prompt 2 giai đoạn)
*   **Cơ sở khoa học:** COmbine/RBCTest paper (Section 3).
*   **Vị trí trong Code:** [ObservationConfirmationPromptBuilder.cs](file:///d:/GSP26SE43.ModularMonolith/ClassifiedAds.Modules.TestGeneration/Algorithms/ObservationConfirmationPromptBuilder.cs)
*   **Nơi sử dụng:** Được gọi tại `HappyPathTestCaseGenerator.cs` (dòng 84) trước khi gửi yêu cầu cho LLM (AI).
*   **Giải thích cho Thầy Cô:**
    *   **Vấn đề:** AI thường bị "ảo tưởng" (hallucination), tạo ra các kịch bản test không có trong tài liệu Spec.
    *   **Giải pháp:** Áp dụng mô hình **Observation (Quan sát) - Confirmation (Xác nhận)**. 
        *   Bước 1: Bắt AI liệt kê mọi ràng buộc nó thấy. 
        *   Bước 2: Bắt AI tìm **Evidence (Bằng chứng)** cụ thể từ Spec cho từng ràng buộc đó. 
    *   **Lợi ích:** Đảm bảo test case sinh ra luôn "bám sát" tài liệu kỹ thuật 100%.

---

## III. Quy trình Thực tế (Pipeline)

Nếu Thầy Cô hỏi dữ liệu đi như thế nào, hãy trình bày flow này:

1.  **Input:** File OpenAPI của hệ thống Modular Monolith.
2.  **Step 1:** `ApiTestOrderAlgorithm` chạy 3 thuật toán đầu tiên (Schema, Semantic, Topological) để đề xuất một thứ tự chạy test logic nhất.
3.  **Step 2 (Human-in-the-loop):** Người dùng duyệt hoặc điều chỉnh thứ tự này từ giao diện Frontend.
4.  **Step 3:** `HappyPathTestCaseGenerator` lấy thứ tự đã duyệt, dùng thuật toán `ObservationConfirmation` để tạo Prompt "siêu chi tiết" gửi cho AI.
5.  **Output:** Bộ TestCase hoàn chỉnh với đầy đủ các mối liên kết dữ liệu (ví dụ: lấy ID vừa tạo ở bước 1 dùng cho bước 2).

---

> [!TIP]
> **Điểm nhấn để lấy điểm cao:** Hãy nhấn mạnh rằng hệ thống không chỉ gọi AI một cách mù quáng, mà sử dụng các thuật toán **Phân tích Đồ thị (Graph Analysis)** và **Xử lý Ngôn ngữ tự nhiên (NLP)** để "tiền xử lý" dữ liệu, giúp AI làm việc chính xác và hiệu quả nhất.
