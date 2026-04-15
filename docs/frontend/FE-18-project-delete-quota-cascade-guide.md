# FE-18 — Project Delete: Quota Release & Cascade Behavior + Spec Soft-Delete & Restore

> **Phạm vi áp dụng:**
> 1. Bản vá hành vi `DELETE /api/projects/{id}` — quota release & TestSuite cascade.
> 2. **Tính năng mới:** Soft-delete & restore cho `ApiSpecification` (API phân tách).
>
> FE cần cập nhật để hỗ trợ luồng xóa / khôi phục specification.

---

## 1. Tổng quan thay đổi

| Vấn đề cũ | Giải pháp mới |
|---|---|
| Xóa project (soft-delete) không trả lại quota → user bị block tạo project mới dù không còn project nào | `DeleteProjectCommand` giờ gọi `ReleaseUsageAsync` sau khi xóa → `UsageTracking.ProjectCount` giảm 1 |
| TestSuites con vẫn ở trạng thái Active sau khi project bị xóa | `DeleteProjectCommand` gọi `TestSuiteProjectService.ArchiveByProjectIdAsync` → tất cả TestSuites của project bị archive |
| **🆕** `DELETE /api/projects/{projectId}/specifications/{specId}` xóa vĩnh viễn record khỏi DB, không thể khôi phục | Chuyển sang **soft-delete**: `IsDeleted = true`, `DeletedAt = now`; record vẫn còn trong DB |
| **🆕** Không có API khôi phục specification đã xóa | Thêm `POST /api/projects/{projectId}/specifications/{specId}/restore` |

---

## 2. Endpoint: `DELETE /api/projects/{id}`

### Request

| Thành phần | Giá trị |
|---|---|
| Method | `DELETE` |
| URL | `/api/projects/{id}` |
| Auth | Bearer JWT (required) |
| Permission | `Permission:DeleteProject` |
| Body | _(không có)_ |

**Path Parameter:**

| Tên | Kiểu | Mô tả |
|---|---|---|
| `id` | `Guid` (UUID) | ID của project cần xóa |

### Response

#### ✅ `200 OK` — Xóa thành công

```json
(empty body)
```

FE nên xử lý: xóa project khỏi local state, cập nhật danh sách, **invalidate cache quota** để lần tạo mới sẽ lấy quota mới.

#### ❌ `404 Not Found` — Project không tồn tại

```json
{
  "message": "Không tìm thấy project với mã '{id}'."
}
```

#### ❌ `400 Bad Request` — Không phải owner

```json
{
  "message": "Bạn không có quyền xóa project này."
}
```

#### ❌ `401 Unauthorized` — Chưa đăng nhập

#### ❌ `429 Too Many Requests` — Rate limit

---

## 3. Luồng xử lý bên trong (không ảnh hưởng contract, chỉ để debug)

```
DELETE /api/projects/{id}
    │
    ├─ [1] Load project từ DB
    │        → 404 nếu không tồn tại
    │
    ├─ [2] Kiểm tra OwnerId == CurrentUserId
    │        → 400 nếu không phải owner
    │
    ├─ [3] Deactivate tất cả ApiSpecifications của project
    │        IsActive = false cho tất cả spec đang active
    │        ActiveSpecId = null
    │
    ├─ [4] Soft-delete project: project.Status = "Archived"
    │
    ├─ [5] SaveChanges + dispatch EntityDeletedEvent<Project>
    │        → Tạo audit log entry "DELETED_PROJECT"
    │        → Outbox event ProjectArchivedOutboxEvent (IsArchived=true)
    │
    ├─ [6] 🆕 ReleaseUsageAsync (best-effort, non-fatal)
    │        UserId = CurrentUserId
    │        LimitType = MaxProjects
    │        IncrementValue = 1 (giảm 1)
    │        → Nếu lỗi: chỉ log WARNING, không fail request
    │
    └─ [7] 🆕 ArchiveByProjectIdAsync (cross-module cascade)
             Archive tất cả TestSuites có ProjectId == id
             và Status != Archived
```

---

## 4. Hành vi quota sau khi xóa

### Trước khi vá:
```
User có plan Free (MaxProjects = 1)
→ Tạo Project A → ProjectCount = 1
→ Xóa Project A → ProjectCount = 1 (!!!) ← Bug: không giảm
→ Tạo Project B → 400 "Bạn đã đạt giới hạn 1 project cho gói Free"
```

### Sau khi vá:
```
User có plan Free (MaxProjects = 1)
→ Tạo Project A → ProjectCount = 1
→ Xóa Project A → ProjectCount = 0 ← Đã fix
→ Tạo Project B → 201 Created ✅
```

### Khuyến nghị FE:
- Sau khi `DELETE /api/projects/{id}` trả `200 OK`, **gọi lại** `GET /api/subscriptions/my-usage` (hoặc endpoint tương đương) để refresh số quota hiển thị trên UI.
- Hoặc dùng optimistic update: giảm `projectCount` hiển thị đi 1 ngay sau khi xóa thành công.

---

## 5. Hành vi cascade TestSuites

Khi project bị xóa:
- Tất cả **TestSuites** thuộc project đó sẽ bị đổi `Status = "Archived"` tự động.
- TestSuites đã `Archived` rồi thì giữ nguyên.
- Các **TestCases**, **LlmSuggestions**, **TestOrderProposals**, **TestSuiteVersions** bên trong TestSuite bị xóa vật lý bởi DB cascade (`ON DELETE CASCADE` từ TestSuite).

### Phía FE:
- Nếu user đang ở trang TestSuites của project và project bị xóa (ví dụ từ tab khác), danh sách TestSuites sẽ trả về rỗng (filter mặc định `Status != Archived`).
- Không cần xử lý gì thêm — backend tự cascade.

---

## 6. Log messages (dành cho debugging)

Khi cần debug, tìm trong server logs theo các pattern sau:

| Level | Pattern | Ý nghĩa |
|---|---|---|
| `INFO` | `Cascade-archived {N} test suite(s) for deleted project. ProjectId={id}` | Cascade thành công, N suites đã được archive |
| `WARN` | `ReleaseUsageAsync failed non-fatally. UserId=... LimitType=MaxProjects` | Quota release thất bại (không blocking — request vẫn 200 OK, nhưng ProjectCount có thể chưa giảm) |
| `INFO` | `DELETED_PROJECT` (audit log) | Audit log entry tạo sau khi delete |
| `INFO` | `EntityDeletedEvent<Project>` dispatched | Domain event thành công |

> **Lưu ý về WARN `ReleaseUsageAsync`:** Nếu log này xuất hiện, user vẫn xóa được project nhưng quota chưa được trả về. User sẽ cần liên hệ support hoặc đợi billing period reset.

---

## 7. Endpoint liên quan: `POST /api/projects` (tạo project)

Không thay đổi contract. Lỗi quota vẫn trả về như cũ:

```http
POST /api/projects
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "My New Project",
  "description": "optional",
  "baseUrl": "https://api.example.com"
}
```

#### ✅ `201 Created` — Tạo thành công

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "My New Project",
  "description": "optional",
  "baseUrl": "https://api.example.com",
  "status": "Active",
  "activeSpecId": null,
  "activeSpecName": null,
  "totalSpecifications": 0,
  "activeSpecSummary": null,
  "createdDateTime": "2025-01-01T00:00:00+00:00",
  "updatedDateTime": null
}
```

#### ❌ `400 Bad Request` — Quota vượt giới hạn (vẫn có thể xảy ra nếu WARN log trên xuất hiện)

```json
{
  "message": "Bạn đã đạt giới hạn 1 project cho gói Free. Vui lòng nâng cấp gói để tạo thêm project."
}
```

**Validation errors `400`:**

| Field | Điều kiện | Message |
|---|---|---|
| `name` | Bắt buộc | `"Tên project là bắt buộc."` |
| `name` | > 200 ký tự | `"Tên project không được vượt quá 200 ký tự."` |
| `description` | > 2000 ký tự | `"Mô tả project không được vượt quá 2000 ký tự."` |
| `baseUrl` | Không phải absolute URL | `"URL cơ sở không hợp lệ. Phải là URL tuyệt đối (ví dụ: https://api.example.com)."` |

---

## 8. Model tham khảo

### `ProjectModel` (dùng trong list `GET /api/projects`)

```typescript
interface ProjectModel {
  id: string;             // UUID
  name: string;
  description: string | null;
  baseUrl: string | null;
  status: "Active" | "Archived";
  activeSpecId: string | null;   // UUID
  activeSpecName: string | null;
  createdDateTime: string;       // ISO 8601
  updatedDateTime: string | null; // ISO 8601
}
```

### `ProjectDetailModel` (dùng trong `POST`, `PUT`, `GET /api/projects/{id}`)

```typescript
interface ProjectDetailModel extends ProjectModel {
  totalSpecifications: number;
  activeSpecSummary: SpecSummaryModel | null;
}

interface SpecSummaryModel {
  id: string;     // UUID
  name: string;
  sourceType: string;
  version: string | null;
  parseStatus: "Pending" | "Success" | "Failed";
  endpointCount: number;
}
```

---

## 9. Checklist FE sau khi backend deploy bản vá

- [ ] Sau `DELETE /api/projects/{id}` → 200 OK: gọi lại quota API để update số dư
- [ ] Nếu dùng optimistic update: giảm `projectUsed` đi 1 ngay lập tức
- [ ] Nếu project list cache theo `status=Active`: invalidate cache sau khi xóa
- [ ] Nếu có trang TestSuites embed trong project: navigate away hoặc clear list khi project bị archive
- [ ] Kiểm tra lại flow "Xóa project → Tạo project mới" — không còn bị block bởi quota nữa
- [ ] **🆕** Khi `DELETE /api/projects/{projectId}/specifications/{specId}` → 204: chuyển spec sang trạng thái "đã xóa" trên UI (giỏ rác / ẩn khỏi danh sách chính)
- [ ] **🆕** Hiển thị nút **Khôi phục** cho các spec trong trash (dùng query `?includeDeleted=true`)
- [ ] **🆕** Khi `POST …/restore` → 200: đưa spec trở lại danh sách chính

---

## 10. Các điểm dễ nhầm

1. `DELETE /api/projects/{id}` là **soft-delete** — project vẫn còn trong DB với `status = "Archived"`, không bị xóa vật lý.
2. Response body của `DELETE` là **empty** (không có JSON), chỉ `200 OK`.
3. Quota được trả về **best-effort** — nếu có WARN log, cần kiểm tra lại `GET /api/subscriptions/my-usage` thủ công.
4. TestSuites con sẽ tự động bị archive — FE không cần gọi API riêng để archive chúng.
5. TestCases bên trong TestSuite bị xóa vật lý (DB cascade) — **không thể khôi phục**.
6. **🆕** `DELETE /api/projects/{projectId}/specifications/{specId}` giờ là **soft-delete** — spec KHÔNG bị xóa vật lý, chỉ ẩn khỏi danh sách mặc định.
7. **🆕** Spec đã xóa vẫn có thể **khôi phục** qua endpoint restore — endpoints & parameters bên trong vẫn giữ nguyên.
8. **🆕** Spec đang là `ActiveSpec` sẽ tự động bị deactivate khi xóa — cần cập nhật UI để reflect trạng thái này.

---

## 11. 🆕 API Soft-Delete & Restore Specification

> Áp dụng cho tất cả loại spec: OpenAPI, Postman, Manual, cURL.

---

### 11.1. `DELETE /api/projects/{projectId}/specifications/{specId}` (cập nhật hành vi)

**Hành vi cũ:** hard-delete — xóa vĩnh viễn khỏi DB.  
**Hành vi mới:** soft-delete — đặt `IsDeleted = true`, spec bị ẩn khỏi danh sách mặc định.

#### Request

| Thành phần | Giá trị |
|---|---|
| Method | `DELETE` |
| URL | `/api/projects/{projectId}/specifications/{specId}` |
| Auth | Bearer JWT (required) |
| Permission | `Permission:DeleteSpecification` |
| Body | _(không có)_ |

#### Response

##### ✅ `204 No Content` — Soft-delete thành công

```
(empty body)
```

##### ❌ `404 Not Found` — Project hoặc spec không tồn tại

```json
{ "message": "Không tìm thấy specification với mã '{specId}'." }
```

##### ❌ `400 Bad Request` — Không phải owner

```json
{ "message": "Bạn không có quyền thao tác project này." }
```

#### Side effect

- Nếu spec đang là `ActiveSpec` của project → `project.ActiveSpecId` bị set `null` tự động.
- `spec.IsActive` bị set `false`.
- Spec bị ẩn trong `GET /api/projects/{projectId}/specifications` (mặc định).

---

### 11.2. `GET /api/projects/{projectId}/specifications` (cập nhật — thêm query param)

Danh sách spec giờ hỗ trợ xem "thùng rác":

| Query Param | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `parseStatus` | `string` | — | Lọc theo trạng thái parse |
| `sourceType` | `string` | — | Lọc theo loại nguồn |
| `includeDeleted` | `bool` | `false` | `true` → trả về **chỉ** các spec đã xóa (trash view); `false` (default) → chỉ spec chưa xóa |

> **Lưu ý:** `includeDeleted=true` trả về **chỉ** các spec đã soft-delete (trash), không phải "tất cả bao gồm đã xóa". Để hiện cả hai nhóm, FE cần gọi 2 request riêng hoặc tự merge.

#### SpecificationModel (cập nhật — thêm 2 trường mới)

```typescript
interface SpecificationModel {
  id: string;             // UUID
  projectId: string;      // UUID
  name: string;
  sourceType: "OpenAPI" | "Postman" | "Manual" | "cURL";
  version: string | null;
  isActive: boolean;
  parseStatus: "Pending" | "Success" | "Failed";
  parsedAt: string | null;        // ISO 8601
  originalFileId: string | null;  // UUID
  createdDateTime: string;        // ISO 8601
  updatedDateTime: string | null; // ISO 8601
  // 🆕 Soft-delete fields
  isDeleted: boolean;
  deletedAt: string | null;       // ISO 8601, null nếu chưa xóa
}
```

---

### 11.3. `POST /api/projects/{projectId}/specifications/{specId}/restore` (**MỚI**)

Khôi phục một specification đã soft-delete về trạng thái bình thường.

#### Request

| Thành phần | Giá trị |
|---|---|
| Method | `POST` |
| URL | `/api/projects/{projectId}/specifications/{specId}/restore` |
| Auth | Bearer JWT (required) |
| Permission | `Permission:RestoreSpecification` |
| Body | _(không có)_ |

**Path Parameters:**

| Tên | Kiểu | Mô tả |
|---|---|---|
| `projectId` | `Guid` (UUID) | ID của project chứa spec |
| `specId` | `Guid` (UUID) | ID của specification cần khôi phục |

#### Response

##### ✅ `200 OK` — Khôi phục thành công

Trả về `SpecificationDetailModel` của spec vừa được khôi phục:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "projectId": "660e8400-e29b-41d4-a716-446655440001",
  "name": "My API v1",
  "sourceType": "OpenAPI",
  "version": "1.0.0",
  "isActive": false,
  "parseStatus": "Success",
  "parsedAt": "2025-01-01T10:00:00+00:00",
  "originalFileId": null,
  "createdDateTime": "2025-01-01T09:00:00+00:00",
  "updatedDateTime": "2025-01-02T08:00:00+00:00",
  "isDeleted": false,
  "deletedAt": null,
  "endpointCount": 12,
  "parseErrors": null,
  "originalFileName": null
}
```

> **Lưu ý:** Sau khi restore, spec có `isActive = false`. Nếu muốn active lại spec, FE gọi thêm `PUT /api/projects/{projectId}/specifications/{specId}/activate`.

##### ❌ `404 Not Found` — Spec không tồn tại hoặc chưa bị xóa

```json
{ "message": "Không tìm thấy specification đã xóa với mã '{specId}'." }
```

##### ❌ `400 Bad Request` — Không phải owner

```json
{ "message": "Bạn không có quyền thao tác project này." }
```

##### ❌ `401 Unauthorized`

##### ❌ `429 Too Many Requests`

---

### 11.4. Luồng UX gợi ý cho FE

```
[Danh sách Specifications - mặc định]
GET /api/projects/{id}/specifications
(includeDeleted mặc định = false)
        │
        ├─ [Xóa spec]
        │   DELETE .../specifications/{specId}
        │   → 204 → ẩn spec khỏi danh sách
        │   → Hiện toast "Đã xóa. Khôi phục?" với nút Undo (trong 5–10 giây)
        │
        └─ [Xem thùng rác]
            GET .../specifications?includeDeleted=true
            → Hiển thị danh sách spec đã xóa với "Xóa lúc: {deletedAt}"
            → Mỗi spec có nút [Khôi phục]
                    │
                    └─ POST .../specifications/{specId}/restore
                       → 200 → chuyển spec về danh sách chính
                       → isActive = false → FE có thể gợi ý [Kích hoạt ngay]
```

---

### 11.5. Permissions liên quan đến Specification

| Permission | Endpoint |
|---|---|
| `Permission:GetSpecifications` | `GET /specifications`, `GET /specifications/{specId}` |
| `Permission:AddSpecification` | `POST /specifications/upload`, `/manual`, `/curl-import` |
| `Permission:DeleteSpecification` | `DELETE /specifications/{specId}` |
| `Permission:RestoreSpecification` | `POST /specifications/{specId}/restore` |
| `Permission:ActivateSpecification` | `PUT /specifications/{specId}/activate`, `/deactivate` |

