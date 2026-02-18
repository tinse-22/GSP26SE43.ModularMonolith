# ApiDocumentation Workflow Guide

Tai lieu nay mo ta workflow thuc te dang co trong `ClassifiedAds.Modules.ApiDocumentation`, de ban biet nen lam gi tiep theo voi du an.

## 1) Module nay giai quyet bai toan gi?

`ApiDocumentation` quan ly vong doi tai lieu API theo 3 cap:

1. `Project` (don vi chinh, thuoc ve 1 user).
2. `Specification` (OpenAPI/Postman/Manual/cURL) thuoc project.
3. `Endpoint` thuoc specification.

Tat ca API deu:

- Yeu cau dang nhap (`[Authorize]`).
- Co permission rieng theo hanh dong.
- Dung rate limit policy mac dinh.

## 2) Kien truc luong tong quan

```text
Client
  -> Controllers (Projects, Specifications, Endpoints)
  -> Dispatcher (CQRS)
  -> Commands / Queries
  -> Repositories + ApiDocumentationDbContext (schema: apidoc)
  -> Domain events (Create/Update/Delete)
  -> AuditLog + OutboxMessages
  -> PublishEventWorker -> MessageBus publishers
```

Code chinh:

- `ClassifiedAds.Modules.ApiDocumentation/Controllers`
- `ClassifiedAds.Modules.ApiDocumentation/Commands`
- `ClassifiedAds.Modules.ApiDocumentation/Queries`
- `ClassifiedAds.Modules.ApiDocumentation/EventHandlers`
- `ClassifiedAds.Modules.ApiDocumentation/HostedServices`

## 3) Workflow chi tiet theo nghiep vu

### A. Project Management

API:

- `GET /api/projects`
- `GET /api/projects/{id}`
- `POST /api/projects`
- `PUT /api/projects/{id}`
- `PUT /api/projects/{id}/archive`
- `PUT /api/projects/{id}/unarchive`
- `DELETE /api/projects/{id}`
- `GET /api/projects/{id}/auditlogs`

Luot chinh:

1. Tao project (`POST`) se validate name/baseUrl va check trung ten theo owner.
2. Tao moi project se consume quota `MaxProjects` qua `ISubscriptionLimitGatewayService`.
3. Archive project se clear `ActiveSpecId` va tat active cua tat ca spec dang active.
4. Delete project hien tai la soft-delete theo logic:
   - set `Status = Archived`,
   - clear active spec,
   - phat domain event deleted.
5. Audit logs hien co chi duoc expose truc tiep o project endpoint `/{id}/auditlogs`.

### B. Specification Management

API:

- `GET /api/projects/{projectId}/specifications`
- `GET /api/projects/{projectId}/specifications/{specId}`
- `GET /api/projects/{projectId}/specifications/upload-methods`
- `POST /api/projects/{projectId}/specifications/upload`
- `POST /api/projects/{projectId}/specifications/manual`
- `POST /api/projects/{projectId}/specifications/curl-import`
- `PUT /api/projects/{projectId}/specifications/{specId}/activate`
- `PUT /api/projects/{projectId}/specifications/{specId}/deactivate`
- `DELETE /api/projects/{projectId}/specifications/{specId}`

#### B1. Upload file spec (`upload`)

1. Chi ho tro `UploadMethod = StorageGatewayContract`.
2. File bat buoc, toi da 10MB, extension chi nhan `.json/.yaml/.yml`.
3. `SourceType` chi cho `OpenAPI` hoac `Postman`.
4. He thong validate format noi dung theo source type.
5. Consume quota `MaxStorageMB` truoc khi upload file qua `IStorageFileGatewayService`.
6. Tao `ApiSpecification` voi:
   - `ParseStatus = Pending`
   - `OriginalFileId = file id tu Storage`
7. Neu `AutoActivate = true` thi deactivate spec cu va gan spec moi thanh active.

Luu y quan trong:

- Trong module nay chua co parser thuc thi parse sau khi upload; spec upload vao se o trang thai `Pending` cho toi khi co flow parse cap nhat.

#### B2. Tao spec manual (`manual`)

1. Bat buoc `Name`, bat buoc it nhat 1 endpoint.
2. Validate `HttpMethod`, `Path`, length...
3. Consume quota `MaxEndpointsPerProject` theo so endpoint gui len.
4. Tao specification voi:
   - `SourceType = Manual`
   - `ParseStatus = Success`
   - `ParsedAt = UtcNow`
5. Tao endpoint + parameters + responses trong cung transaction.
6. Co the `AutoActivate`.

#### B3. Import tu cURL (`curl-import`)

1. Bat buoc `Name` + `CurlCommand`.
2. Parse cURL (method/url/query/header/body).
3. Consume quota `MaxEndpointsPerProject` voi increment = 1.
4. Tao 1 specification (`SourceType = cURL`, `ParseStatus = Success`) + 1 endpoint.
5. Tu dong map:
   - path params tu `{id}`,
   - query params,
   - headers (bo qua 1 so header pho bien),
   - body parameter neu co data.
6. Co the `AutoActivate`.

#### B4. Activate / Deactivate / Delete spec

1. Activate:
   - dam bao chi 1 active spec/project.
   - spec active cu (neu khac) bi tat.
2. Deactivate:
   - chi cho phep khi spec dang la active spec hien tai.
3. Delete:
   - neu dang active thi clear `Project.ActiveSpecId`,
   - sau do xoa ban ghi spec (children cascade theo FK).

### C. Endpoint Management

API:

- `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
- `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
- `POST /api/projects/{projectId}/specifications/{specId}/endpoints`
- `PUT /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
- `DELETE /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`

Luot chinh:

1. Create endpoint:
   - validate method/path,
   - consume quota `MaxEndpointsPerProject` (+1),
   - tao endpoint + children (parameters/responses) trong transaction.
2. Update endpoint:
   - cap nhat field endpoint,
   - xoa toan bo parameter/response cu,
   - tao lai children tu payload moi.
3. Delete endpoint:
   - xoa endpoint, children cascade theo DB.

Luu y:

- `SecurityRequirements` duoc doc trong query detail, nhung flow create/update endpoint hien tai chua nhan payload cho security requirements.

## 4) Audit log + Outbox + Background worker

### A. Domain events dang co handler

1. `Project`: create/update/archive/delete -> co audit log + outbox.
2. `Specification`: upload/activate/deactivate/delete -> co audit log + outbox.

### B. Endpoint events

- Command co dispatch `EntityCreated/Updated/Deleted<ApiEndpoint>`.
- Hien tai module chua co event handler cho `ApiEndpoint`, nen khong tao audit/outbox rieng cho endpoint tu flow nay.

### C. Publish outbox

1. `PublishEventWorker` chay nen.
2. Moi vong lay toi da 50 outbox message chua publish.
3. Gui message bus, thanh cong thi danh dau `Published = true`.
4. Neu khong co publisher phu hop se log loi va event duoc retry.

## 5) Permission va limit quan trong

Permission matrix chinh:

- Project: `GetProjects`, `AddProject`, `UpdateProject`, `ArchiveProject`, `DeleteProject`
- Specification: `GetSpecifications`, `AddSpecification`, `ActivateSpecification`, `DeleteSpecification`
- Endpoint: `GetEndpoints`, `AddEndpoint`, `UpdateEndpoint`, `DeleteEndpoint`

Rate limit (`ApiDocumentation.DefaultPolicy`):

- User da dang nhap: 200 requests/phut theo user name.
- Anonymous key theo host: 100 requests/phut (thuc te API nay yeu cau authorize).

## 6) Ban nen lam gi tiep theo cho du an

### Step 1 - Chot luong su dung chinh cho team

1. Neu team co OpenAPI/Postman san: di theo luong `upload`.
2. Neu team khong co file spec chuan: di theo `manual` hoac `curl-import`.
3. Quy uoc 1 project chi co 1 active spec tai moi thoi diem.

### Step 2 - Bo sung parser async cho upload (uu tien cao)

Ly do: spec upload hien tao `ParseStatus = Pending` nhung chua co job parse trong module.

Can lam:

1. Tao consumer/job nhan su kien `SPEC_UPLOADED`.
2. Lay file tu Storage theo `OriginalFileId`.
3. Parse ra endpoints/security.
4. Cap nhat `ParseStatus = Success/Failed`, `ParsedAt`, `ParseErrors`.
5. Dam bao idempotent neu message bi deliver lap.

### Step 3 - Chot chinh sach quota

1. Hien tai module consume quota khi tao moi (project/spec endpoint).
2. Xac dinh ro co can "refund usage" khi xoa resource hay khong.
3. Neu can refund, bo sung command/flow giam usage trong Subscription module.

### Step 4 - Hoan thien observability + test

1. Viet integration test cho:
   - upload/manual/curl-import,
   - activate/deactivate,
   - archive/delete project.
2. Test boundary quota (max projects, max endpoints, max storage).
3. Test outbox worker khi:
   - publisher day du,
   - thieu publisher,
   - retry sau loi tam thoi.

## 7) Checklist thao tac nhanh (de dung ngay)

1. Tao project (`POST /api/projects`).
2. Tao spec bang 1 trong 3 cach (`upload` | `manual` | `curl-import`).
3. Neu can, activate spec (`PUT .../activate`).
4. Quan ly endpoints (`POST/PUT/DELETE .../endpoints`).
5. Theo doi thay doi project qua `GET /api/projects/{id}/auditlogs`.
6. Kiem tra outbox worker dang chay de event duoc publish.

