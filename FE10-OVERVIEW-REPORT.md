# Bao cao Overview FE-10: Test Reporting And Export

**Ngay kiem tra:** 16/03/2026  
**Muc tieu:** Danh gia muc do san sang cua codebase cho FE-10 va chot huong implement phu hop nhat voi Modular Monolith hien tai.

---

## 1. Danh gia chung (Trang thai: NEN TANG RAT TOT)

Codebase hien tai co nen tang rat phu hop de implement FE-10 ma khong can pha vo kien truc:

- `ClassifiedAds.Modules.TestReporting` da ton tai, da duoc wire vao `WebAPI` va `Migrator`.
- Schema `testreporting` da co migration khoi tao cho `TestReports` va `CoverageMetrics`.
- `Storage` da co contract gateway de upload/download file.
- `TestExecution` da co deterministic run summary + cache-backed detailed results.
- `Infrastructure` da co san HTML/PDF/CSV tooling (`RazorLight`, `DinkToPdf`, `CsvHelper`) va host da dang ky san.

Noi ngan gon: FE-10 khong con la bai toan "thieu nen tang", ma la bai toan "thieu workflow va service layer".

---

## 2. Nhung diem codebase da san sang rat dung huong

1. **Module va schema da co san**
   - `TestReporting` da co `TestReport`, `CoverageMetric`, `DbContext`, `ServiceCollectionExtensions`, va migration initial.
   - Dieu nay cho phep FE-10 v1 reuse 100% persistence shape hien tai ma khong can mo rong database.

2. **Host wiring da dung pattern**
   - `ClassifiedAds.WebAPI/Program.cs` da dang ky `AddTestReportingModule()` trong ca MVC chain va service chain.
   - `ClassifiedAds.Migrator` da dang ky va migrate `TestReporting`.

3. **Execution data da du kha dung**
   - `TestRun` summary o PostgreSQL.
   - `TestRunResultModel` va `TestCaseRunResultModel` da co day du thong tin cho report chi tiet.
   - FE09 da chung minh pattern gateway doc `TestExecution` qua contract la dung va hop codebase.

4. **Export infrastructure da co san**
   - `IStorageFileGatewayService` da du de upload/download file.
   - `IRazorLightEngine` va `IConverter` da duoc dang ky san trong host.
   - `CsvHelper` da nam trong dependency graph cua repo.

---

## 3. Cac khoang trong can implement cho FE-10

Nhung phan hien tai van con thieu hoan toan:

1. **Khong co cross-module report read gateway**
   - `TestReporting` chua co cach hop le de doc detailed run context tu `TestExecution`.

2. **Khong co CQRS/API surface**
   - Chua co `TestReportsController`.
   - Chua co command/query cho generate, list, get metadata, download.

3. **Khong co report runtime**
   - Chua co generator.
   - Chua co coverage calculator.
   - Chua co export sanitizer.
   - Chua co renderer theo format.

4. **Khong co file generation flow**
   - Chua upload file qua `Storage` gateway.
   - Chua persist metadata report sau khi render.

5. **Khong co test slice cho TestReporting**
   - Hien chua co namespace test rieng cho `ClassifiedAds.UnitTests.TestReporting`.

---

## 4. Huong implement de xuat (Khuyen nghi cho FE-10 v1)

Huong phu hop nhat voi codebase hien tai la:

- FE-10 v1 nen la **synchronous on-demand API**.
- Module chinh van la `ClassifiedAds.Modules.TestReporting`.
- Doc run data qua contract moi `ITestRunReportReadGatewayService` trong `ClassifiedAds.Contracts/TestExecution`.
- Tinh coverage theo **suite-scoped endpoints**, khong tinh theo full spec analytics o v1.
- Render:
  - `JSON` bang `System.Text.Json`
  - `CSV` bang `CsvHelper`
  - `HTML` bang `RazorLight`
  - `PDF` bang HTML -> `DinkToPdf`
- Upload file qua `IStorageFileGatewayService`.
- Reuse `Permission:GetTestRuns` de tranh mo rong auth seed trong v1.
- Khong them migration, khong them worker, khong them queue export.

---

## 5. Ket luan

FE-10 co the duoc implement rat "sach" tren codebase nay neu di theo 3 boundary ro rang:

- `TestExecution` so huu run data
- `TestReporting` so huu report orchestration + metadata
- `Storage` so huu file binary

Bo tai lieu FE-10 da duoc tao theo mau FE-09 de AI Agent co the implement dung huong ma khong phai tu suy doan lai architecture.
