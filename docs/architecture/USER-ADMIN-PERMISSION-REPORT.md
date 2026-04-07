# User vs Admin Permission Report

Date: 2026-04-05
Scope: docs only, no code changes in this step

## Goal

Muc tieu san pham can chot lai la:

- Ung dung nay la workspace ca nhan, khong phai mo hinh company / organization / role hierarchy phuc tap.
- **`User` mac dinh co TOAN BO quyen tren he thong**, chi tru cac quyen dac biet (danh cho Admin) nhu:
  - Xoa / quan tri nguoi dung (Deleted user).
  - Tao cac goi dich vu (Plan/package management).
  - Quan ly thanh toan (Payment managements, billing admin, reconciliation).
  - System Admin (Cau hinh he thong, Global audit log).

## Current State

### 1. Permission layer va ownership layer dang ton tai song song

Codebase hien tai khong chi chan bang `[Authorize(Permissions.X)]`, ma con co them ownership checks trong mot so flow:

- `ApiDocumentation` da filter theo `OwnerId` trong project/spec/endpoint flow.
- `Subscription` da co `EnsureSubscriptionOwnershipAsync(...)` va `ResolveUserId(...)` cho self-service.
- `TestExecution`, `TestReporting`, `LlmAssistant` da truyen `CurrentUserId` xuong command/query.
- Mot phan `TestGeneration` write flow co `CurrentUserId`, nhung mot so read flow chua co owner filter ro rang.

Ket luan: huong mo `User` cho tinh nang ca nhan la hop logic, vi repo da co san mot phan self-scope. Tuy vay, khong phai module nao cung dong deu.

### 2. Hien tai role model dang la workaround, chua phai thiet ke dich

Hai cho dang ep user thanh `Admin`:

- `ClassifiedAds.Modules.Identity/Controllers/UsersController.cs`
  - luong tao user tu admin screen dang auto them role `Admin`
- `ClassifiedAds.Modules.Identity/Controllers/AuthController.cs`
  - working tree hien tai dang auto them `User` + `Admin` khi self-register

Dieu nay giai quyet tam thoi viec "user moi trang quyen", nhung khong phai mo hinh phan quyen mong muon ve lau dai.

### 3. Seed permission cho Admin hien chua day du tren toan he thong

`ClassifiedAds.Modules.Identity/DbConfigurations/RoleClaimConfiguration.cs` hien chi seed permission cua module `Identity` cho role `Admin`.

He qua:

- `Admin` hien dang duoc dung nhu super-role trong tu duy san pham
- nhung seed data lai chua phan anh dung tat ca permission cua cac module khac
- neu lam lai role model, can co mot single source of truth cho role-permission mapping

### 4. Co mot so diem bat thuong / risk truoc khi mo rong `User`

#### Configuration module

`ClassifiedAds.Modules.Configuration/Controllers/ConfigurationEntriesController.cs`

- CRUD da co permission rieng
- nhung `ExportAsExcel` va `ImportExcel` hien chi dung class-level `[Authorize]`
- day la system config, khong nen de bat ky user dang nhap nao goi duoc

#### Storage module

`ClassifiedAds.Modules.Storage/Queries/GetFileEntriesQuery.cs`

- `GetFiles` hien tra ve tat ca file chua xoa, khong filter theo owner

`ClassifiedAds.Modules.Storage/Authorization/FileEntryAuthorizationHandler.cs`

- resource authorization handler dang `context.Succeed(...)` vo dieu kien
- comment trong code con ghi `TODO: check CreatedById`

Ket luan:

- khong nen mo rong `Storage` cho `User` mac dinh truoc khi fix owner enforcement

#### TestGeneration / TestExecution read queries

Mot so read query dang loc theo `projectId` / `suiteId` / `environmentId` nhung chua truyen `CurrentUserId`:

- `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopesQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetTestSuiteScopeQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCasesByTestSuiteQuery.cs`
- `ClassifiedAds.Modules.TestGeneration/Queries/GetTestCaseDetailQuery.cs`
- `ClassifiedAds.Modules.TestExecution/Queries/GetExecutionEnvironmentsQuery.cs`
- `ClassifiedAds.Modules.TestExecution/Queries/GetExecutionEnvironmentQuery.cs`

Neu project/suite id bi doan duoc, nhung endpoint nay co nguy co doc du lieu cheo user.

Ket luan:

- day la nhom "nen mo cho User", nhung phai audit ownership filter truoc khi rollout rong

## Recommended Target Model

## A. Endpoints thuoc quyen `User` mac dinh

Theo nguyen tac "User co toan bo quyen", mac dinh Role `User` se so huu TAT CA cac permission cua cac xu ly nghiep vu/ung dung bao gom:

- ApiDocumentation (Get/Add/Update/Delete Projects, Specs, Endpoints)
- TestExecution, TestReporting, FailureExplanation (Run, View, Explain test)
- TestGeneration (Create/Update/Delete Suites, Cases, Generate from AI)
- Storage (File upload/download)
- Subscription self-service (Get Plans, Get own subscription, Create payment intent, PayOs Checkout)
- Auth self-service (Login, Register, Logout, Profile update)
- Configuration (Get public configs if any)

*Ghi chu:* Mac du co toan quyen su dung chuc nang, he thong phai luon dam bao Resource-level Authorization (kiem tra `OwnerId` hoac `CurrentUserId`) de User chi doc/ghi du lieu cua chinh ho.

## B. Endpoints thuoc quyen `Admin-only` (Cac quyen dac biet)

Nhung quyen sau la "quyền đặc biệt", chi danh rieng cho `Admin` va User khong duoc phep goi:

### 1. Quan tri nguoi dung (User/Role management)
- Xoa nguoi dung (Deleted user)
- Quan ly role, phan quyen (Assign/remove role)
- Khoa / mo khoa tai khoan (Lock/unlock user)
- Dat lai mat khau cho user khac
- Xem tat ca nguoi dung

### 2. Tao cac goi dich vu (Plan management)
- Tao cac goi dich vu (`Permission:AddPlan`)
- Sua, xoa goi dich vu (`Permission:UpdatePlan`, `Permission:DeletePlan`)
- Xem lich su thay doi cac goi (`Permission:GetPlanAuditLogs`)

### 3. Quan ly thanh toan (Payment managements)
- Cap nhat/tao subscription thu cong khong qua payment gateway (`Permission:AddSubscription`, `Permission:UpdateSubscription`)
- Ghi nhan giao dich thu cong (`Permission:AddPaymentTransaction`)
- Dong bo va debug thanh toan (`Permission:SyncPayment`, debug endpoint)
- Chinh sua usage tracking thu cong (`Permission:UpdateUsageTracking`)

### 4. System operation
- Quan ly cau hinh he thong (System configuration: Add/Update/Delete/Import/Export)
- Xem log he thong (Global audit logs: GetAuditLogs)

## C. Endpoints khong xep theo User/Admin thong thuong

Day la integration/system callback endpoints:

- `POST /api/payments/payos/webhook`
- `GET /api/payments/payos/return`
- `POST /api/test-suites/{suiteId}/test-cases/from-ai`

De xuat:

- tiep tuc dung signature / callback API key / integration auth
- khong map vao role `User` hoac `Admin`

## Proposed Permission Matrix

| Nhom | De xuat |
|------|---------|
| **Toan bo cac chuc nang ung dung (Core Features)**<br> (Project, Spec, Test Generation/Execution, Storage, LlmAssistant...) | **User**<br> (Co toan quyen su dung tren du lieu cua ho - can check Resource Ownership) |
| User/Role management (Bao gom Delete User) | Admin |
| Plan management (Tao cac goi, Sua xoa goi) | Admin |
| Payment management / Billing admin | Admin |
| System configuration & Global audit logs | Admin |
| PayOS webhook / AI callback | Integration auth, khong theo role |

## Recommended Implementation Order

1. Chot target role model nhu da xac nhan: `User` co toan bo quyen, chi tru cac quyen dac biet cua `Admin` (xoa user, tao goi, quan ly thanh toan).
2. Tao file / module mapping permission -> role (dieu chinh `RoleClaimConfiguration`) lam single source of truth cho toan repo: tat ca permission ung dung deu nhet vao `User`.
3. Dua `AuthController.Register` va luong API user ve model dung:
   - self-register mac dinh chi nhan `User` (se map tu dong voi toan bo quyen ung dung)
   - Khong auto them `Admin` cho user moi.
4. Giu cac permission lien quan toi `Identity (User/Role)`, `System Configuration`, `AuditLog`, `Plan management`, `Payment managements` mac dinh mapping cho `Admin`.
5. Fix cac security gaps de dam bao "User chi xem data cua User" mac du co the truyen input de goi API:
   - owner filter cho `Storage`
   - read ownership trong `TestGeneration` / `ExecutionEnvironments`
6. Sau khi role mapping on dinh, bo workaround "grant Admin cho user moi dang ky".

## Final Recommendation

Da dieu chinh va xac nhan lai business direction theo yeu cau:

- **`User` co TOAN BO QUYEN tren he thong ung dung**.
- **Chỉ TRỪ cac quyen dac biet** tieu bieu nhu: deleted user (xoa nguoi dung), tao cac goi (quan ly plans/packages), va payment managements (quan tri thanh toan, doi soat).

Khong can chia nho viec add authorization attribute. Co the gan truc tiep tap permission rat lon cho `User` trong bang RoleClaimConfiguration, sau do tap trung fix Data Ownership de hoan tra quyen that su cua "workspace ca nhan" cho nguoi dung binh thuong. File nay la baseline moi nhat de thuc hien viec cap nhat Code.
