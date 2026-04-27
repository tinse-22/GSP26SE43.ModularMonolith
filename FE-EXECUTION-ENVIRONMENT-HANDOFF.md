# FE Handoff — Execution Environment API

> **Tài liệu này dành cho Frontend.** Mô tả chính xác contract của tất cả 5 endpoint CRUD cho `ExecutionEnvironment`, bao gồm **bug DELETE** thường gặp khi tích hợp.

---

## 1. Base URL

```
/api/projects/{projectId}/execution-environments
```

> `projectId` — GUID của project hiện tại (lấy từ context của app).

---

## 2. Authentication

Tất cả endpoint đều yêu cầu JWT Bearer token:

```http
Authorization: Bearer <access_token>
```

---

## 3. Endpoints

### 3.1 GET All — Lấy danh sách environments

```http
GET /api/projects/{projectId}/execution-environments
Authorization: Bearer <token>
```

**Response 200:**

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "projectId": "a1b2c3d4-...",
    "name": "Staging",
    "baseUrl": "https://staging.example.com",
    "variables": { "ENV": "staging", "API_KEY": "xxx" },
    "headers": { "X-Custom-Header": "value" },
    "authConfig": {
      "authType": "BearerToken",
      "token": "***MASKED***"
    },
    "isDefault": false,
    "createdDateTime": "2026-04-01T10:00:00Z",
    "updatedDateTime": null,
    "rowVersion": "AAAAAAAAB+M="
  }
]
```

> **Lưu ý**: `authConfig.token`, `password`, `clientSecret`, `apiKeyValue` đều bị **masked** (`***MASKED***`) khi trả về. Đây là hành vi bảo mật có chủ ý, không phải lỗi.

---

### 3.2 GET By ID — Lấy 1 environment

```http
GET /api/projects/{projectId}/execution-environments/{environmentId}
Authorization: Bearer <token>
```

**Response 200:** Giống cấu trúc object trong mục 3.1.

**Response 404:**

```json
{
  "type": "NotFoundException",
  "message": "Không tìm thấy execution environment với mã '...'."
}
```

---

### 3.3 POST — Tạo mới environment

```http
POST /api/projects/{projectId}/execution-environments
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "name": "Production",
  "baseUrl": "https://prod.example.com",
  "variables": { "ENV": "prod" },
  "headers": { "X-Trace": "fe-app" },
  "authConfig": {
    "authType": "BearerToken",
    "token": "my-secret-token"
  },
  "isDefault": false
}
```

| Field       | Type                | Required | Constraint     |
|-------------|---------------------|----------|----------------|
| `name`      | string              | ✅        | max 100 chars  |
| `baseUrl`   | string              | ✅        | max 500 chars, URL hợp lệ |
| `variables` | object (key:string) | ❌        |                |
| `headers`   | object (key:string) | ❌        |                |
| `authConfig`| object              | ❌        | xem mục 5      |
| `isDefault` | boolean             | ❌        | default: false |

**Response 201:**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "rowVersion": "AAAAAAAAB+M=",
  ...
}
```

> Lưu `rowVersion` từ response 201 để dùng cho PUT và DELETE sau này.

---

### 3.4 PUT — Cập nhật environment

```http
PUT /api/projects/{projectId}/execution-environments/{environmentId}
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "rowVersion": "AAAAAAAAB+M=",
  "name": "Production Updated",
  "baseUrl": "https://prod-v2.example.com",
  "variables": {},
  "headers": {},
  "authConfig": {
    "authType": "None"
  },
  "isDefault": true
}
```

> `rowVersion` trong **request body** cho PUT — **không cần** URL encode vì nằm trong JSON body.

**Response 200:** Object `ExecutionEnvironmentModel` đã cập nhật (bao gồm `rowVersion` mới).

**Response 409 — Conflict (optimistic concurrency):**

```json
{
  "type": "ConflictException",
  "code": "CONCURRENCY_CONFLICT",
  "message": "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại."
}
```

---

### 3.5 DELETE — Xóa environment

```http
DELETE /api/projects/{projectId}/execution-environments/{environmentId}?rowVersion={urlEncodedRowVersion}
Authorization: Bearer <token>
```

**Response 204 No Content** — xóa thành công.

---

## ⚠️ 4. BUG HAY GẶP VỚI DELETE — rowVersion phải được URL encode

### Vấn đề

`rowVersion` là chuỗi Base64 chuẩn, ví dụ: `AAAAAAAAB+M=`

Chuỗi Base64 **thường chứa** các ký tự đặc biệt trong URL:

| Ký tự Base64 | Ý nghĩa trong URL query string | Kết quả nếu không encode |
|--------------|-------------------------------|--------------------------|
| `+`          | Được decode thành **dấu cách** | `AAAAAAAAB M=` → `FormatException` |
| `/`          | Dấu phân cách path             | Có thể break URL routing  |
| `=`          | Dấu `key=value`                | Có thể bị cắt bỏ         |

Nếu FE gọi:
```
DELETE /api/projects/.../execution-environments/...?rowVersion=AAAAAAAAB+M=
```

Backend nhận được `rowVersion = "AAAAAAAAB M="` (dấu `+` bị đổi thành space).

Kết quả: **400 Bad Request** với message `"RowVersion không hợp lệ."`

### Fix — Luôn dùng `encodeURIComponent()` trước khi append vào URL

```typescript
// ✅ ĐÚNG
const deleteEnvironment = async (
  projectId: string,
  environmentId: string,
  rowVersion: string   // lấy từ response GET hoặc POST/PUT
) => {
  const encodedRowVersion = encodeURIComponent(rowVersion);
  const url = `/api/projects/${projectId}/execution-environments/${environmentId}?rowVersion=${encodedRowVersion}`;

  const res = await fetch(url, {
    method: 'DELETE',
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (res.status === 204) {
    // xóa thành công
    return;
  }

  if (res.status === 409) {
    const body = await res.json();
    throw new Error(`Conflict: ${body.message}`);
  }

  if (res.status === 404) {
    throw new Error('Environment không tồn tại.');
  }

  throw new Error(`DELETE thất bại: ${res.status}`);
};
```

```typescript
// ❌ SAI — không encode rowVersion
const url = `/api/projects/${projectId}/execution-environments/${environmentId}?rowVersion=${rowVersion}`;
```

---

## 5. AuthConfig — Cấu trúc chi tiết

### `authType: "None"` — Không có auth

```json
{ "authType": "None" }
```

### `authType: "BearerToken"`

```json
{
  "authType": "BearerToken",
  "token": "your-bearer-token",
  "headerName": "Authorization"   // optional, default: "Authorization"
}
```

### `authType: "Basic"`

```json
{
  "authType": "Basic",
  "username": "user",
  "password": "pass"
}
```

### `authType: "ApiKey"`

```json
{
  "authType": "ApiKey",
  "apiKeyName": "X-API-Key",
  "apiKeyValue": "secret",
  "apiKeyLocation": "Header"   // "Header" | "Query"
}
```

### `authType: "OAuth2ClientCredentials"`

```json
{
  "authType": "OAuth2ClientCredentials",
  "tokenUrl": "https://auth.example.com/token",
  "clientId": "my-client-id",
  "clientSecret": "my-client-secret",
  "scopes": ["read", "write"]
}
```

> **Bảo mật**: Các trường secret (`token`, `password`, `clientSecret`, `apiKeyValue`) bị mask khi GET. FE không cần hiển thị lại giá trị thực, chỉ cần gửi lại khi update (nếu muốn thay đổi).

---

## 6. Luồng CRUD chuẩn (Full Flow)

```
1. GET /environments         → Hiển thị danh sách, lưu rowVersion của từng item
2. POST /environments        → Tạo mới → nhận rowVersion từ response 201
3. GET /environments/{id}    → Lấy chi tiết → lưu rowVersion mới nhất
4. PUT /environments/{id}    → Gửi rowVersion trong JSON body → nhận rowVersion mới
5. DELETE /environments/{id}?rowVersion=<encodeURIComponent(rowVersion)>  → 204
```

> **Nguyên tắc Optimistic Concurrency**: Trước khi PUT hoặc DELETE, nên GET lại để có `rowVersion` mới nhất. Nếu có `409 Conflict`, thông báo người dùng reload và thử lại.

---

## 7. Bảng tóm tắt Error Codes

| HTTP Status | Meaning | Cách xử lý FE |
|-------------|---------|---------------|
| 200 | OK | Đọc response body |
| 201 | Created | Đọc `id` và `rowVersion` từ body |
| 204 | No Content (DELETE OK) | Xóa khỏi UI |
| 400 | Validation error | Hiển thị `message` từ body |
| 401 | Unauthorized | Redirect về login |
| 403 | Forbidden (wrong project owner) | Hiển thị "Bạn không có quyền" |
| 404 | Not found | Hiển thị "Không tìm thấy" |
| 409 | Concurrency conflict | Hiển thị "Dữ liệu đã bị thay đổi, vui lòng tải lại" |

---

## 8. Ví dụ TypeScript đầy đủ

```typescript
const API_BASE = '/api';

interface ExecutionEnvironmentModel {
  id: string;
  projectId: string;
  name: string;
  baseUrl: string;
  variables: Record<string, string>;
  headers: Record<string, string>;
  authConfig: ExecutionAuthConfigModel;
  isDefault: boolean;
  createdDateTime: string;
  updatedDateTime: string | null;
  rowVersion: string;   // Base64 — phải URL encode khi dùng trong DELETE query param
}

interface ExecutionAuthConfigModel {
  authType: 'None' | 'BearerToken' | 'Basic' | 'ApiKey' | 'OAuth2ClientCredentials';
  headerName?: string;
  token?: string;
  username?: string;
  password?: string;
  apiKeyName?: string;
  apiKeyValue?: string;
  apiKeyLocation?: 'Header' | 'Query';
  tokenUrl?: string;
  clientId?: string;
  clientSecret?: string;
  scopes?: string[];
}

class ExecutionEnvironmentApi {
  constructor(
    private projectId: string,
    private getToken: () => string
  ) {}

  private get baseUrl() {
    return `${API_BASE}/projects/${this.projectId}/execution-environments`;
  }

  private headers() {
    return {
      Authorization: `Bearer ${this.getToken()}`,
      'Content-Type': 'application/json',
    };
  }

  async getAll(): Promise<ExecutionEnvironmentModel[]> {
    const res = await fetch(this.baseUrl, { headers: this.headers() });
    if (!res.ok) throw new Error(`GET all failed: ${res.status}`);
    return res.json();
  }

  async getById(environmentId: string): Promise<ExecutionEnvironmentModel> {
    const res = await fetch(`${this.baseUrl}/${environmentId}`, { headers: this.headers() });
    if (res.status === 404) throw new Error('Environment không tồn tại.');
    if (!res.ok) throw new Error(`GET failed: ${res.status}`);
    return res.json();
  }

  async create(payload: Omit<ExecutionEnvironmentModel, 'id' | 'projectId' | 'createdDateTime' | 'updatedDateTime' | 'rowVersion'>): Promise<ExecutionEnvironmentModel> {
    const res = await fetch(this.baseUrl, {
      method: 'POST',
      headers: this.headers(),
      body: JSON.stringify(payload),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || `POST failed: ${res.status}`);
    }
    return res.json();
  }

  async update(environmentId: string, rowVersion: string, payload: Partial<ExecutionEnvironmentModel>): Promise<ExecutionEnvironmentModel> {
    const res = await fetch(`${this.baseUrl}/${environmentId}`, {
      method: 'PUT',
      headers: this.headers(),
      body: JSON.stringify({ ...payload, rowVersion }),  // rowVersion trong body — KHÔNG cần encodeURIComponent
    });
    if (res.status === 409) {
      const err = await res.json();
      throw new Error(`Conflict: ${err.message}`);
    }
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || `PUT failed: ${res.status}`);
    }
    return res.json();
  }

  async delete(environmentId: string, rowVersion: string): Promise<void> {
    // ✅ QUAN TRỌNG: phải encodeURIComponent vì rowVersion là Base64 có thể chứa +, /, =
    const encodedRowVersion = encodeURIComponent(rowVersion);
    const url = `${this.baseUrl}/${environmentId}?rowVersion=${encodedRowVersion}`;

    const res = await fetch(url, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${this.getToken()}`,
        // Không cần Content-Type cho DELETE
      },
    });

    if (res.status === 204) return;   // thành công

    if (res.status === 404) throw new Error('Environment không tồn tại.');
    if (res.status === 409) {
      const err = await res.json();
      throw new Error(`Conflict: ${err.message}`);
    }
    if (res.status === 400) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || 'Validation error');
    }
    throw new Error(`DELETE failed: ${res.status}`);
  }
}
```

---

## 9. Checklist tích hợp FE

- [ ] Dùng `encodeURIComponent(rowVersion)` khi gọi DELETE
- [ ] Lưu `rowVersion` từ response của POST / PUT / GET để dùng cho lần gọi tiếp theo
- [ ] Xử lý `409 Conflict` — thông báo reload và thử lại
- [ ] Không hardcode `rowVersion` — luôn lấy từ response API mới nhất
- [ ] Không cần gửi secret fields trong GET/display — backend đã mask
- [ ] Khi update authConfig, gửi đầy đủ lại toàn bộ authConfig object (backend sẽ overwrite)

---

*Last updated: 2026-04-27 | Backend branch: `feature/FE-17-optimize-fix-20260410`*
