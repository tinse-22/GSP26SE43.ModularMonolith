# Script thuyết trình Workflow 5 và Workflow 7

## Workflow 5 - Explain How to Pass Parameters

**Mở bài**

Ở Workflow 5, mục tiêu chính là giải thích cách dữ liệu được truyền vào từng request trong một test suite. Nói cách khác, sau khi hệ thống đã biết API cần test là gì và test case nào sẽ chạy, workflow này trả lời câu hỏi: mỗi tham số lấy từ đâu, truyền vào đâu, và vì sao nó cần thiết.

**Ý nghĩa của workflow**

Trong API testing, một request thường không chạy độc lập. Ví dụ request tạo user trả về `userId`, sau đó request cập nhật user phải dùng lại `userId` đó. Nếu không biết rõ mỗi tham số đến từ đâu, test case có thể fail không phải vì API sai, mà vì mình truyền sai dữ liệu.

Workflow 5 dùng để làm rõ luồng dữ liệu này. Hệ thống đọc contract API, đọc environment, đọc các response có thể lấy dữ liệu từ request trước, sau đó tạo ra bản mapping giữa parameter và data source.

**Cách workflow chạy**

Đầu tiên, người dùng chọn endpoint hoặc cả test suite cần giải thích tham số. Hệ thống sẽ đọc endpoint đó cần những loại parameter nào, ví dụ path parameter, query string, header, hoặc request body.

Tiếp theo, hệ thống tìm nguồn dữ liệu phù hợp cho từng parameter. Nguồn có thể là biến trong environment, dữ liệu mẫu của test case, hoặc kết quả response từ request trước. Nếu một request sau cần dữ liệu từ request trước, hệ thống sẽ ghi nhận dependency để đảm bảo thứ tự chạy đúng.

Sau đó, workflow xác định vị trí truyền dữ liệu. Cùng một giá trị `id`, nếu đưa sai cho query thay vì path, API vẫn có thể fail. Vì vậy workflow không chỉ nói giá trị là gì, mà còn nói nó phải nằm ở đâu trong request.

Cuối cùng, hệ thống tạo giải thích bằng ngôn ngữ dễ hiểu: lấy giá trị nào, từ đâu, truyền vào đâu, và request nào phụ thuộc vào request nào.

**Output**

Kết quả của Workflow 5 là một parameter mapping rõ ràng. Mapping này gồm nguồn dữ liệu, vị trí truyền, biến cần extract từ response, và thứ tự chạy an toàn. Output này được dùng tiếp ở Workflow 6 để sinh test case tốt hơn và ở Workflow 7 để resolve dữ liệu khi chạy test thật.

**Điểm cần nhấn mạnh khi thuyết trình**

Workflow 5 giúp biến test case từ dạng tĩnh thành một chuỗi test có context. Nó đảm bảo test runner không gọi API với tham số rỗng, sai vị trí, hoặc sai thứ tự. Giá trị lớn nhất của workflow này là làm cho người dùng hiểu được logic truyền tham số, thay vì chỉ nhìn thấy một request bị fail.

**Câu chốt**

Tóm lại, Workflow 5 là bước nối dữ liệu giữa các request. Nếu Workflow 4 giúp hiểu API và nghiệp vụ, Workflow 5 giúp hiểu cách dữ liệu đi qua từng request trong quá trình test.

---

## Workflow 7 - Execute Test Cases, AI Explain Result / Failure

**Mở bài**

Workflow 7 là bước đưa toàn bộ test case đã được sinh và approve vào thực thi trên API thật. Đây là workflow biến những test case trên hệ thống thành kết quả kiểm thử cụ thể: pass, fail, hoặc skip.

**Ý nghĩa của workflow**

Nếu các workflow trước tập trung vào phần chuẩn bị, phân tích tài liệu, sinh test case và giải thích tham số, thì Workflow 7 là bước kiểm chứng thực tế. Hệ thống sẽ chọn environment, resolve biến, gọi API, validate response, lưu kết quả và giải thích lý do nếu test fail.

Workflow này quan trọng vì nó không chỉ nói test có thành công hay không, mà còn giúp người dùng hiểu tại sao nó thành công hoặc thất bại.

**Cách workflow chạy**

Đầu tiên, người dùng chọn test suite đã approve và chọn environment muốn chạy, ví dụ local, staging hoặc môi trường gần production. Hệ thống nạp base URL, auth token, headers, secrets, timeout và các biến cần thiết.

Tiếp theo, hệ thống sắp xếp test case theo dependency đã có từ các workflow trước. Request tạo dữ liệu hoặc lấy token sẽ chạy trước request cần dùng dữ liệu đó.

Trước khi gọi mỗi API, runner resolve tham số request. Các placeholder trong path, query, header hoặc body sẽ được thay bằng giá trị từ environment hoặc giá trị đã extract từ response trước.

Sau đó, hệ thống gửi request đến API thật và ghi lại thông tin thực thi như status code, response body, thời gian chạy, lỗi kết nối nếu có, và các lần retry nếu được cấu hình.

Khi có response, workflow validate kết quả dựa trên expected status, schema, assertion và business expectation của test case. Nếu response đúng với expectation, case được đánh dấu pass. Nếu không đúng, case được đánh dấu fail. Nếu case phụ thuộc vào một case trước đã fail, nó có thể được đánh dấu skip.

Cuối cùng, hệ thống tạo report. Report có trạng thái từng test case, request/response log, assertion result, extracted variables và giải thích lỗi bằng AI cho các case fail.

**Output**

Output của Workflow 7 là test run report. Báo cáo này cho biết tổng số case pass, fail, skip, chi tiết từng request/response, biến nào đã được extract, assertion nào sai, và lý do có khả năng gây fail.

**Điểm cần nhấn mạnh khi thuyết trình**

Workflow 7 không chỉ là nút "Run test". Nó là một execution engine có context. Nó hiểu environment, hiểu dependency, hiểu parameter mapping, hiểu expected result, và có thể giải thích failure để người dùng biết nên kiểm tra API, test data, auth, environment hay logic assertion.

**Câu chốt**

Tóm lại, Workflow 7 là bước đóng vòng lặp kiểm thử. Sau khi hệ thống đã phân tích tài liệu, sinh test case và giải thích cách truyền tham số, Workflow 7 chạy test trên API thật và biến kết quả thành báo cáo có thể hành động được.

---

## Câu nối chuyển tiếp giữa Workflow 5 và Workflow 7

Sau Workflow 5, hệ thống đã biết mỗi request cần dữ liệu gì và dữ liệu đó nằm ở đâu. Đến Workflow 7, những mapping này được sử dụng thật trong lúc execute. Vì vậy Workflow 5 là phần giải thích và chuẩn bị dữ liệu, còn Workflow 7 là phần áp dụng mapping đó để chạy test và đánh giá kết quả.
