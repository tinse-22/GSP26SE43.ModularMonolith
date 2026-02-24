# TestGeneration Controllers API Guide

Cap nhat lan cuoi: 2026-02-22

Tai lieu nay giai thich y nghia API va logic xu ly cua 2 controller:

- `ClassifiedAds.Modules.TestGeneration/Controllers/TestSuitesController.cs`
- `ClassifiedAds.Modules.TestGeneration/Controllers/TestOrderController.cs`

## 1) Tong quan kien truc

Ca 2 controller deu theo mau CQRS:

- Controller khong chua business logic nang, chi map HTTP -> Command/Query.
- Xu ly nghiep vu nam trong `Commands/*`, `Queries/*`, `Services/*`.
- Moi endpoint write deu lay `CurrentUserId` tu `ICurrentUser`.
- Toan bo endpoint deu co `[Authorize]` + permission policy theo action.

## 2) TestSuitesController - quan ly scope test suite

Route goc:

- `api/projects/{projectId}/test-suites`

Y nghia:

- Dinh nghia pham vi endpoint se duoc dung cho test suite.
- Day la gate cau hinh truoc khi sinh de xuat thu tu test order (FE-05A).

### 2.1) GET `/api/projects/{projectId}/test-suites`

Permission:

- `Permission:GetTestSuites`

Luong xu ly:

1. Dispatch `GetTestSuiteScopesQuery { ProjectId }`.
2. Query lay danh sach suite theo `ProjectId`.
3. Bo qua suite da `Archived`.
4. Sap xep theo `CreatedDateTime` giam dan.
5. Map entity -> `TestSuiteScopeModel`.

Ket qua:

- `200 OK` + list `TestSuiteScopeModel`.

### 2.2) GET `/api/projects/{projectId}/test-suites/{suiteId}`

Permission:

- `Permission:GetTestSuites`

Luong xu ly:

1. Dispatch `GetTestSuiteScopeQuery { ProjectId, SuiteId }`.
2. Tim suite theo dung `suiteId + projectId`.
3. Neu khong co -> `404 NotFound`.
4. Tra ve `TestSuiteScopeModel`.

Ket qua:

- `200 OK` neu tim thay.
- `404 NotFound` neu khong ton tai.

### 2.3) POST `/api/projects/{projectId}/test-suites`

Permission:

- `Permission:AddTestSuite`

Body chinh (`CreateTestSuiteScopeRequest`):

- `name` (required)
- `apiSpecId` (required)
- `selectedEndpointIds` (required, min 1)
- `description`, `generationType`

Luong xu ly (`AddUpdateTestSuiteScopeCommand` - create branch):

1. Validate `ProjectId`, `CurrentUserId`, `ApiSpecId`, `Name`.
2. Normalize endpoint ids qua `TestSuiteScopeService`:
   - bo `Guid.Empty`
   - distinct
   - sort tang dan
3. Bat buoc con it nhat 1 endpoint sau normalize.
4. Goi `IApiEndpointMetadataService.GetEndpointMetadataAsync(...)` de xac nhan tat ca endpoint thuoc dung specification.
5. Neu co endpoint ngoai spec -> `400 ValidationException`.
6. Tao `TestSuite` moi:
   - `Status = Draft`
   - `ApprovalStatus = NotApplicable`
   - gan `CreatedById`, `SelectedEndpointIds`, ...
7. Save DB, map ve `TestSuiteScopeModel`.

Ket qua:

- `201 Created`
- `Location: /api/projects/{projectId}/test-suites/{id}`

### 2.4) PUT `/api/projects/{projectId}/test-suites/{suiteId}`

Permission:

- `Permission:UpdateTestSuite`

Body chinh (`UpdateTestSuiteScopeRequest`):

- Nhu create + `rowVersion` (required)

Luong xu ly (`AddUpdateTestSuiteScopeCommand` - update branch):

1. Validate nhu create.
2. Tim suite theo `suiteId + projectId`, khong co -> `404`.
3. Chi owner duoc sua: `suite.CreatedById == CurrentUserId`.
4. Suite da `Archived` thi cam update.
5. Bat buoc `rowVersion`, parse base64 -> byte[] de optimistic concurrency.
6. Cap nhat scope fields (`Name`, `Description`, `ApiSpecId`, `GenerationType`, `SelectedEndpointIds`).
7. Save; neu conflict row version -> `409 CONCURRENCY_CONFLICT`.

Ket qua:

- `200 OK` + `TestSuiteScopeModel` moi.

### 2.5) DELETE `/api/projects/{projectId}/test-suites/{suiteId}?rowVersion=...`

Permission:

- `Permission:DeleteTestSuite`

Y nghia:

- Soft delete (archive), khong xoa hard record.

Luong xu ly (`ArchiveTestSuiteScopeCommand`):

1. Tim suite theo `suiteId + projectId`.
2. Chi owner duoc archive.
3. Bat buoc `rowVersion`, parse base64.
4. Set `Status = Archived`.
5. Save; neu conflict -> `409 CONCURRENCY_CONFLICT`.

Ket qua:

- `204 NoContent`.

## 3) TestOrderController - de xuat/review/approve thu tu test API

Route goc:

- `api/test-suites/{suiteId}`

Y nghia:

- Quan ly vong doi proposal thu tu endpoint de lam gate truoc khi sinh test cases (FE-05A).

### 3.1) POST `/api/test-suites/{suiteId}/order-proposals`

Permission:

- `Permission:ProposeTestOrder`

Body chinh (`ProposeApiTestOrderRequest`):

- `specificationId` (required)
- `selectedEndpointIds` (co the rong)
- `source`, `llmModel`, `reasoningNote`

Luong xu ly (`ProposeApiTestOrderCommand`):

1. Validate `TestSuiteId`, `CurrentUserId`.
2. Tim suite, check owner.
3. Validate `specificationId` phai khop `suite.ApiSpecId`.
4. Neu request khong gui endpoint, fallback dung `suite.SelectedEndpointIds` da luu o FE-04-01.
5. Goi `IApiTestOrderService.BuildProposalOrderAsync(...)` tao thu tu de xuat.
6. Mark cac proposal cu trang thai `Pending/Approved/ModifiedAndApproved` thanh `Superseded`.
7. Tao proposal moi voi `ProposalNumber` tang dan, `Status = Pending`, luu `ProposedOrder`.
8. Cap nhat suite:
   - `ApprovalStatus = PendingReview`
   - xoa `ApprovedById`, `ApprovedAt`
9. Save.

Thuat toan order (trong `ApiTestOrderService`):

- Uu tien endpoint auth-related truoc.
- Sau do endpoint it dependency truoc.
- Sau do theo HTTP method weight: `POST -> PUT -> PATCH -> GET -> DELETE -> OPTIONS -> HEAD`.
- Tie-break theo `Path` va `EndpointId` de deterministic.

Ket qua:

- `201 Created` + `ApiTestOrderProposalModel`.

### 3.2) GET `/api/test-suites/{suiteId}/order-proposals/latest`

Permission:

- `Permission:GetTestOrderProposal`

Luong xu ly:

1. Check suite ton tai + ownership.
2. Lay proposal co `ProposalNumber` lon nhat.
3. Neu khong co -> `404`.
4. Map JSON order -> model.

Ket qua:

- `200 OK` + latest proposal.

### 3.3) PUT `/api/test-suites/{suiteId}/order-proposals/{proposalId}/reorder`

Permission:

- `Permission:ReorderTestOrder`

Body chinh (`ReorderApiTestOrderRequest`):

- `rowVersion` (required)
- `orderedEndpointIds` (required)
- `reviewNotes` (optional)

Luong xu ly (`ReorderApiTestOrderCommand`):

1. Check suite ton tai + ownership.
2. Check proposal ton tai trong suite.
3. Chi reorder duoc proposal `Pending`.
4. Parse `rowVersion` de concurrency control.
5. Validate tap `orderedEndpointIds` phai dung bang tap endpoint cua `ProposedOrder`:
   - cung so luong
   - khong duplicate
   - khong endpoint ngoai pham vi
   - khong thieu endpoint
6. Tao `UserModifiedOrder` theo thu tu moi, giu nguyen metadata (method/path/reason/dependencies).
7. Save; conflict -> `409 CONCURRENCY_CONFLICT`.

Ket qua:

- `200 OK` + proposal da cap nhat.

### 3.4) POST `/api/test-suites/{suiteId}/order-proposals/{proposalId}/approve`

Permission:

- `Permission:ApproveTestOrder`

Body chinh (`ApproveApiTestOrderRequest`):

- `rowVersion` (required)
- `reviewNotes` (optional)

Luong xu ly (`ApproveApiTestOrderCommand`):

1. Check suite ton tai + ownership.
2. Tim proposal theo `proposalId + suiteId`.
3. Idempotent fast-path:
   - Neu proposal da `Approved/ModifiedAndApproved/Applied` va co `AppliedOrder` -> tra luon, khong update.
4. Nguoc lai, proposal phai o `Pending`.
5. Parse `rowVersion`.
6. Chon final order:
   - Neu co `UserModifiedOrder` thi dung cai nay.
   - Neu khong thi dung `ProposedOrder`.
7. Final order khong duoc rong.
8. Cap nhat proposal:
   - `Status = Approved` hoac `ModifiedAndApproved`
   - set `ReviewedById`, `ReviewedAt`, `ReviewNotes`
   - set `AppliedOrder`, `AppliedAt`
9. Cap nhat suite:
   - `ApprovalStatus = Approved` hoac `ModifiedAndApproved`
   - set `ApprovedById`, `ApprovedAt`
10. Update proposal + suite trong cung transaction.
11. Conflict -> `409 CONCURRENCY_CONFLICT`.

Ket qua:

- `200 OK` + proposal da approve.

### 3.5) POST `/api/test-suites/{suiteId}/order-proposals/{proposalId}/reject`

Permission:

- `Permission:ApproveTestOrder`

Body chinh (`RejectApiTestOrderRequest`):

- `rowVersion` (required)
- `reviewNotes` (required)

Luong xu ly (`RejectApiTestOrderCommand`):

1. Check `reviewNotes` bat buoc.
2. Check suite ton tai + ownership.
3. Check proposal ton tai va dang `Pending`.
4. Parse `rowVersion`.
5. Cap nhat proposal `Status = Rejected`, set review info.
6. Cap nhat suite:
   - `ApprovalStatus = Rejected`
   - clear `ApprovedById`, `ApprovedAt`
7. Save trong 1 transaction.
8. Conflict -> `409 CONCURRENCY_CONFLICT`.

Ket qua:

- `200 OK` + proposal da reject.

### 3.6) GET `/api/test-suites/{suiteId}/order-gate-status`

Permission:

- `Permission:GetTestOrderProposal`

Y nghia:

- API check gate pass/fail truoc cac buoc tiep theo (vi du generate test case).

Luong xu ly (`GetApiTestOrderGateStatusQuery` -> `ApiTestOrderGateService`):

1. Check suite ton tai + ownership.
2. Tim active proposal theo dieu kien:
   - `Status` thuoc `{Approved, ModifiedAndApproved, Applied}`
   - `AppliedOrder` khong rong
   - uu tien proposal moi nhat (`ProposalNumber` lon nhat)
3. Neu khong tim thay -> gate fail:
   - `IsGatePassed = false`
   - `ReasonCode = ORDER_CONFIRMATION_REQUIRED`
4. Neu tim thay, parse `AppliedOrder`:
   - Neu co item -> gate pass
   - Neu rong -> gate fail cung reason code tren.

Ket qua:

- `200 OK` + `ApiTestOrderGateStatusModel`.

## 4) Quy tac chung can nho

1. Owner-only write:
   - Nhieu command bat buoc `suite.CreatedById == CurrentUserId`.
2. Optimistic concurrency:
   - update/delete/reorder/approve/reject deu can `RowVersion` base64.
3. Soft delete:
   - Test suite delete la archive.
4. Supersede proposal cu:
   - Moi lan propose se supersede cac de xuat active cu.
5. Gate dependency:
   - Chua co approved/applied order hop le thi gate khong pass.

## 5) Flow su dung API de xuat

1. Tao test suite scope (`POST /api/projects/{projectId}/test-suites`).
2. Co the cap nhat scope neu can (`PUT .../test-suites/{suiteId}`).
3. Tao de xuat order (`POST /api/test-suites/{suiteId}/order-proposals`).
4. Lay de xuat moi nhat de hien thi (`GET .../order-proposals/latest`).
5. User reorder neu can (`PUT .../reorder`).
6. User approve hoac reject (`POST .../approve` hoac `POST .../reject`).
7. Kiem tra gate (`GET .../order-gate-status`) truoc buoc sinh test tiep theo.
