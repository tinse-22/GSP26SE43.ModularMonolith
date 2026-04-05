# User vs Admin Permission Report

Date: 2026-04-05
Scope: docs only, no code changes in this step

## Goal

Muc tieu san pham can chot lai la:

- ung dung nay la workspace ca nhan, khong phai mo hinh company / organization / role hierarchy phuc tap
- `User` mac dinh phai dung duoc gan nhu toan bo tinh nang hang ngay tren du lieu cua chinh ho
- `Admin` chi giu cac nhom thao tac nhay cam: quan tri user/role, cau hinh he thong, audit log toan he thong, billing admin, payment debug/reconciliation, webhook/integration hardening

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

## A. Endpoints nen de `User` mac dinh

### Auth self-service

Khong can permission rieng, chi can authenticated user:

- `POST /api/auth/logout`
- `GET /api/auth/me`
- `POST /api/auth/change-password`
- `GET /api/auth/me/profile`
- `PUT /api/auth/me/profile`
- `POST /api/auth/me/avatar`
- cac flow anonymous hop le:
  - register
  - login
  - refresh-token
  - forgot-password
  - reset-password
  - confirm-email
  - resend-confirmation-email

### ApiDocumentation

Nen mo toan bo cho `User` mac dinh:

- `Permission:GetProjects`
- `Permission:AddProject`
- `Permission:UpdateProject`
- `Permission:DeleteProject`
- `Permission:ArchiveProject`
- `Permission:GetSpecifications`
- `Permission:AddSpecification`
- `Permission:UpdateSpecification`
- `Permission:DeleteSpecification`
- `Permission:ActivateSpecification`
- `Permission:GetEndpoints`
- `Permission:AddEndpoint`
- `Permission:UpdateEndpoint`
- `Permission:DeleteEndpoint`

Ly do:

- module nay da thiet ke theo `OwnerId`
- phu hop mo hinh workspace ca nhan

### TestExecution / TestReporting / FailureExplanation

Nen mo cho `User` mac dinh:

- `Permission:StartTestRun`
- `Permission:GetTestRuns`

Ly do:

- day la flow su dung cot loi cua san pham
- `TestRuns`, `Reports`, `FailureExplanations` da truyen `CurrentUserId`

### TestGeneration

Muc tieu san pham nen la `User` duoc dung toan bo:

- `Permission:GetTestSuites`
- `Permission:AddTestSuite`
- `Permission:UpdateTestSuite`
- `Permission:DeleteTestSuite`
- `Permission:ProposeTestOrder`
- `Permission:GetTestOrderProposal`
- `Permission:ReorderTestOrder`
- `Permission:ApproveTestOrder`
- `Permission:GenerateTestCases`
- `Permission:GetTestCases`
- `Permission:GenerateBoundaryNegativeTestCases`
- `Permission:AddTestCase`
- `Permission:UpdateTestCase`
- `Permission:DeleteTestCase`

Nhung rollout nen tach 2 pha:

- Pha 1: mo write/generation flow da co `CurrentUserId`
- Pha 2: mo read flow sau khi audit them owner filter cho suite/test-case queries

### Subscription self-service

Nen mo cho `User` mac dinh:

- `Permission:GetPlans`
- `Permission:GetSubscription`
- `Permission:GetCurrentSubscription`
- `Permission:CancelSubscription`
- `Permission:GetSubscriptionHistory`
- `Permission:GetPaymentTransactions`
- `Permission:GetUsageTracking` cho du lieu cua chinh user
- `Permission:CreateSubscriptionPayment`
- `Permission:GetPaymentIntent`
- `Permission:CreatePayOsCheckout`

Ghi chu quan trong:

- `CreateSubscriptionPayment` va `CreatePayOsCheckout` la user-facing payment flow, khong nen cho vao `Admin-only`, neu khong user se khong tu mua goi duoc
- webhook / return URL cua PayOS la integration endpoint, khong xep vao role `User` hay `Admin`

## B. Endpoints nen giu `Admin-only`

### Identity admin

Giu `Admin-only`:

- tat ca permission trong `ClassifiedAds.Modules.Identity/Authorization/Permissions.cs`
- quan ly role
- quan ly user
- assign/remove role
- lock/unlock user
- set password cho user khac
- gui reset password / resend confirmation thay mat user khac

### System configuration

Giu `Admin-only`:

- `Permission:GetConfigurationEntries`
- `Permission:GetConfigurationEntry`
- `Permission:AddConfigurationEntry`
- `Permission:UpdateConfigurationEntry`
- `Permission:DeleteConfigurationEntry`
- `ExportAsExcel`
- `ImportExcel`

Ly do:

- day la system-wide settings
- co kha nang chua secret / encrypted config

### Global audit log

Giu `Admin-only`:

- `Permission:GetAuditLogs`

Ly do:

- audit log tong hop co the lo metadata cua toan bo he thong

### Subscription admin / billing admin

Nen giu `Admin-only` hoac internal-only:

- `Permission:AddPlan`
- `Permission:UpdatePlan`
- `Permission:DeletePlan`
- `Permission:GetPlanAuditLogs`
- `Permission:AddSubscription`
- `Permission:UpdateSubscription`
- `Permission:AddPaymentTransaction`
- `Permission:UpdateUsageTracking`
- `Permission:SyncPayment`

Ly do:

- `AddSubscription` / `UpdateSubscription` hien co the tao hoac nang cap paid subscription truc tiep ma khong di qua payment intent
- `AddPaymentTransaction` la ghi nhan giao dich thu cong
- `UpdateUsageTracking` anh huong billing counter
- `SyncPayment` la manual reconciliation / debug voi PayOS

### Payment debug / operational endpoints

Nen `Admin-only` hoac doi ten sang internal endpoint:

- `POST /api/payments/debug/check-payment/{intentId}`
- `POST /api/payments/debug/sync-payment/{intentId}`

Ghi chu:

- `GetPaymentIntent` da du cho user tu xem trang thai intent cua ho
- debug endpoints khong nen la public user-facing contract

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
| Auth self-service | Authenticated user |
| Project / Spec / Endpoint | User |
| Test generation / execution / reports | User, nhung audit read ownership truoc khi rollout full |
| File management | Chua mo rong cho toi khi fix owner enforcement |
| User/Role management | Admin |
| System configuration | Admin |
| Global audit logs | Admin |
| Plan management | Admin |
| Subscription direct create/update | Admin/Internal |
| Payment reconciliation / debug | Admin |
| PayOS webhook / AI callback | Integration auth, khong theo role |

## Recommended Implementation Order

1. Chot target role model:
   - `User` = workspace ca nhan day du
   - `Admin` = system/security/billing admin
2. Tao bang mapping permission -> role lam single source of truth cho toan repo.
3. Mo role `User` cho `ApiDocumentation` va nhom test flow an toan truoc.
4. Dua `AuthController.Register` va `UsersController.Post` ve model dung:
   - self-register mac dinh chi nhan `User`
   - create-user khong auto them `Admin` neu khong chu dong chon
5. Giu `Identity`, `Configuration`, `AuditLog`, `Plan management`, `billing admin` cho `Admin`.
6. Fix cac security gaps truoc khi mo rong them:
   - owner filter cho `Storage`
   - `FileEntryAuthorizationHandler`
   - read ownership trong `TestGeneration` / `ExecutionEnvironments`
   - permission-specific authorize cho `Configuration Export/Import`
7. Sau khi role mapping on dinh, bo workaround "grant Admin cho user moi".

## Final Recommendation

Neu follow dung business direction "app ca nhan, khong role hierarchy phuc tap", thi target hop ly nhat la:

- `User` duoc dung phan lon tinh nang cot loi
- `Admin` chi quan ly he thong va billing nhay cam
- khong tiep tuc giai quyet bang cach gan `Admin` cho tat ca user moi

Tuy nhien, de rollout an toan, can tach ro:

- nhom co the mo ngay
- nhom phai fix ownership truoc
- nhom bat buoc giu admin/internal

File nay la baseline de implement o buoc tiep theo, khong nen code truoc khi chot bang mapping nay.
