# FE-18 — Project Delete: Quota Release & Cascade Behavior

> **Phạm vi áp dụng:** Bản vá hành vi `DELETE /api/projects/{id}`.  
> Không có endpoint mới. Chỉ hành vi bên trong thay đổi.  
> FE **không cần sửa request/response format**, nhưng cần hiểu behavior mới để hiển thị đúng trạng thái.

---

## 1. Tổng quan thay đổi

| Vấn đề cũ | Giải pháp mới |
|---|---|
| Xóa project (soft-delete) không trả lại quota → user bị block tạo project mới dù không còn project nào | `DeleteProjectCommand` giờ gọi `ReleaseUsageAsync` sau khi xóa → `UsageTracking.ProjectCount` giảm 1 |
| TestSuites con vẫn ở trạng thái Active sau khi project bị xóa | `DeleteProjectCommand` gọi `TestSuiteProjectService.ArchiveByProjectIdAsync` → tất cả TestSuites của project bị archive |

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

---

## 10. Các điểm dễ nhầm

1. `DELETE /api/projects/{id}` là **soft-delete** — project vẫn còn trong DB với `status = "Archived"`, không bị xóa vật lý.
2. Response body của `DELETE` là **empty** (không có JSON), chỉ `200 OK`.
3. Quota được trả về **best-effort** — nếu có WARN log, cần kiểm tra lại `GET /api/subscriptions/my-usage` thủ công.
4. TestSuites con sẽ tự động bị archive — FE không cần gọi API riêng để archive chúng.
5. TestCases bên trong TestSuite bị xóa vật lý (DB cascade) — **không thể khôi phục**.
