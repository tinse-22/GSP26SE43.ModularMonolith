# Báo cáo Overview FE-10: Test Reporting And Export

**Ngày kiểm tra:** 16/03/2026
**Mục tiêu:** Đánh giá mức độ sẵn sàng của codebase cho FE-10 và chốt hướng implement phù hợp nhất với Modular Monolith hiện tại.

---

## 1. Đánh giá chung (Trạng thái: NỀN TẢNG RẤT TỐT)

Codebase hiện tại có nền tảng rất phù hợp để implement FE-10 mà không cần phá vỡ kiến trúc:

- `ClassifiedAds.Modules.TestReporting` đã tồn tại, đã được wire vào `WebAPI` và `Migrator`.
- Schema `testreporting` đã có migration khởi tạo cho `TestReports` và `CoverageMetrics`.
- `Storage` đã có contract gateway để upload/download file.
- `TestExecution` đã có deterministic run summary + cache-backed detailed results.
- `Infrastructure` đã có sẵn HTML/PDF/CSV tooling (`RazorLight`, `DinkToPdf`, `CsvHelper`) và host đã đăng ký sẵn.

Nói ngắn gọn: FE-10 không còn là bài toán "thiếu nền tảng", mà là bài toán "thiếu workflow và service layer".

---

## 2. Những điểm codebase đã sẵn sàng rất đúng hướng

1. **Module và schema đã có sẵn**
   - `TestReporting` đã có `TestReport`, `CoverageMetric`, `DbContext`, `ServiceCollectionExtensions`, và migration initial.
   - Điều này cho phép FE-10 v1 reuse 100% persistence shape hiện tại mà không cần mở rộng database.

2. **Host wiring đã đúng pattern**
   - `ClassifiedAds.WebAPI/Program.cs` đã đăng ký `AddTestReportingModule()` trong cả MVC chain và service chain.
   - `ClassifiedAds.Migrator` đã đăng ký và migrate `TestReporting`.

3. **Execution data đã đủ khả dụng**
   - `TestRun` summary ở PostgreSQL.
   - `TestRunResultModel` và `TestCaseRunResultModel` đã có đầy đủ thông tin cho report chi tiết.
   - FE09 đã chứng minh pattern gateway đọc `TestExecution` qua contract là đúng và hợp codebase.

4. **Export infrastructure đã có sẵn**
   - `IStorageFileGatewayService` đã đủ để upload/download file.
   - `IRazorLightEngine` và `IConverter` đã được đăng ký sẵn trong host.
   - `CsvHelper` đã nằm trong dependency graph của repo.

---

## 3. Các khoảng trống cần implement cho FE-10

Những phần hiện tại vẫn còn thiếu hoàn toàn:

1. **Không có cross-module report read gateway**
   - `TestReporting` chưa có cách hợp lệ để đọc detailed run context từ `TestExecution`.

2. **Không có CQRS/API surface**
   - Chưa có `TestReportsController`.
   - Chưa có command/query cho generate, list, get metadata, download.

3. **Không có report runtime**
   - Chưa có generator.
   - Chưa có coverage calculator.
   - Chưa có export sanitizer.
   - Chưa có renderer theo format.

4. **Không có file generation flow**
   - Chưa upload file qua `Storage` gateway.
   - Chưa persist metadata report sau khi render.

5. **Không có test slice cho TestReporting**
   - Hiện chưa có namespace test riêng cho `ClassifiedAds.UnitTests.TestReporting`.

---

## 4. Hướng implement đề xuất (Khuyến nghị cho FE-10 v1)

Hướng phù hợp nhất với codebase hiện tại là:

- FE-10 v1 nên là **synchronous on-demand API**.
- Module chính vẫn là `ClassifiedAds.Modules.TestReporting`.
- Đọc run data qua contract mới `ITestRunReportReadGatewayService` trong `ClassifiedAds.Contracts/TestExecution`.
- Tính coverage theo **suite-scoped endpoints**, không tính theo full spec analytics ở v1.
- Render:
  - `JSON` bằng `System.Text.Json`
  - `CSV` bằng `CsvHelper`
  - `HTML` bằng `RazorLight`
  - `PDF` bằng HTML -> `DinkToPdf`
- Upload file qua `IStorageFileGatewayService`.
- Reuse `Permission:GetTestRuns` để tránh mở rộng auth seed trong v1.
- Không thêm migration, không thêm worker, không thêm queue export.

---

## 5. Kết luận

FE-10 có thể được implement rất "sạch" trên codebase này nếu đi theo 3 boundary rõ ràng:

- `TestExecution` sở hữu run data
- `TestReporting` sở hữu report orchestration + metadata
- `Storage` sở hữu file binary

Bộ tài liệu FE-10 đã được tạo theo mẫu FE-09 để AI Agent có thể implement đúng hướng mà không phải tự suy đoán lại architecture.
