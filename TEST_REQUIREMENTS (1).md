# Tài Liệu Kiểm Thử API - Test Requirements Document

> **Dự án:** API Đồ Án Tốt Nghiệp  
> **Base URL:** `http://localhost:5000/api`  
> **Swagger UI:** `http://localhost:5000/api-docs`  
> **Phiên bản tài liệu:** 1.0  
> **Ngày tạo:** 26/04/2026

---

## Mục Lục

1. [Tổng Quan Hệ Thống](#1-tổng-quan-hệ-thống)
2. [Hướng Dẫn Thiết Lập Môi Trường Test](#2-hướng-dẫn-thiết-lập-môi-trường-test)
3. [Quy Ước Chung & Format Response](#3-quy-ước-chung--format-response)
4. [TC-SYS: Kiểm Thử Health Check](#4-tc-sys-kiểm-thử-health-check)
5. [TC-AUTH: Kiểm Thử Xác Thực](#5-tc-auth-kiểm-thử-xác-thực)
6. [TC-CAT: Kiểm Thử Danh Mục (Categories)](#6-tc-cat-kiểm-thử-danh-mục-categories)
7. [TC-PROD: Kiểm Thử Sản Phẩm (Products)](#7-tc-prod-kiểm-thử-sản-phẩm-products)
8. [TC-SEC: Kiểm Thử Bảo Mật](#8-tc-sec-kiểm-thử-bảo-mật)
9. [Dữ Liệu Test Mẫu (Test Data)](#9-dữ-liệu-test-mẫu-test-data)
10. [Checklist Tổng Hợp](#10-checklist-tổng-hợp)

---

## 1. Tổng Quan Hệ Thống

### Danh Sách Endpoint

| Nhóm       | Method | Endpoint              | Mô tả                  | Xác thực            |
| ---------- | ------ | --------------------- | ---------------------- | ------------------- |
| System     | GET    | `/api/health`         | Kiểm tra server        | Không               |
| Auth       | POST   | `/api/auth/register`  | Đăng ký tài khoản      | Không               |
| Auth       | POST   | `/api/auth/login`     | Đăng nhập              | Không               |
| Categories | GET    | `/api/categories`     | Lấy danh sách danh mục | Không               |
| Categories | POST   | `/api/categories`     | Tạo danh mục mới       | **Có (Bearer JWT)** |
| Categories | PUT    | `/api/categories/:id` | Cập nhật danh mục      | **Có (Bearer JWT)** |
| Categories | DELETE | `/api/categories/:id` | Xóa danh mục           | **Có (Bearer JWT)** |
| Products   | GET    | `/api/products`       | Lấy danh sách sản phẩm | Không               |
| Products   | POST   | `/api/products`       | Tạo sản phẩm mới       | **Có (Bearer JWT)** |
| Products   | PUT    | `/api/products/:id`   | Cập nhật sản phẩm      | **Có (Bearer JWT)** |
| Products   | DELETE | `/api/products/:id`   | Xóa sản phẩm           | **Có (Bearer JWT)** |

### Ràng Buộc Dữ Liệu (Validation Rules)

| Entity   | Field         | Quy tắc                                                            |
| -------- | ------------- | ------------------------------------------------------------------ |
| User     | `email`       | Định dạng email hợp lệ, duy nhất trong hệ thống, tự động lowercase |
| User     | `password`    | Tối thiểu 6 ký tự                                                  |
| Category | `name`        | Tối thiểu 1 ký tự, duy nhất trong hệ thống                         |
| Category | `description` | Tuỳ chọn (optional), chuỗi ký tự                                   |
| Product  | `name`        | Tối thiểu 1 ký tự                                                  |
| Product  | `price`       | Số không âm (>= 0)                                                 |
| Product  | `stock`       | Số nguyên không âm (>= 0)                                          |
| Product  | `categoryId`  | MongoDB ObjectId hợp lệ, phải tồn tại trong DB                     |

---

## 2. Hướng Dẫn Thiết Lập Môi Trường Test

### Cài đặt

```
1. Clone project và cài dependencies: npm install
2. Tạo file .env với các biến:
   - MONGODB_URI=mongodb://localhost:27017/graduation_project
   - JWT_SECRET=your_secret_key
   - JWT_EXPIRES_IN=1d
   - PORT=5000
3. Khởi động server: npm run dev
4. Xác nhận server chạy: GET http://localhost:5000/api/health
```

### Công Cụ Gợi Ý

- **Postman** hoặc **Insomnia** để gửi HTTP request
- **Swagger UI** tại `http://localhost:5000/api-docs` để xem API docs
- **MongoDB Compass** để kiểm tra dữ liệu trong DB

### Lấy Token Để Test Các API Có Auth

```
1. Gọi POST /api/auth/login với tài khoản hợp lệ
2. Lấy giá trị "token" từ response body
3. Với mỗi request cần auth, thêm header:
   Authorization: Bearer <token_vừa_lấy>
```

---

## 3. Quy Ước Chung & Format Response

### Response Thành Công

```json
{
  "success": true,
  "message": "Mô tả kết quả",
  "data": { ... }
}
```

### Response Lỗi

```json
{
  "success": false,
  "message": "Mô tả lỗi"
}
```

### Response Lỗi Validation (Zod)

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": {
    "fieldErrors": { "email": ["Invalid email"] },
    "formErrors": []
  }
}
```

### HTTP Status Code Sử Dụng

| Code | Ý nghĩa                            |
| ---- | ---------------------------------- |
| 200  | Thành công                         |
| 201  | Tạo mới thành công                 |
| 400  | Dữ liệu đầu vào không hợp lệ       |
| 401  | Chưa xác thực / token không hợp lệ |
| 404  | Không tìm thấy tài nguyên          |
| 409  | Xung đột dữ liệu (đã tồn tại)      |
| 500  | Lỗi server                         |

---

## 4. TC-SYS: Kiểm Thử Health Check

### TC-SYS-001: Kiểm tra server đang hoạt động

|               |                                            |
| ------------- | ------------------------------------------ |
| **Endpoint**  | `GET /api/health`                          |
| **Mô tả**     | Xác nhận server đang hoạt động bình thường |
| **Điều kiện** | Server đang chạy                           |

**Request:**

```
GET http://localhost:5000/api/health
```

**Expected Response (200 OK):**

```json
{
  "status": "success",
  "message": "Server is healthy",
  "timestamp": "2026-04-26T00:00:00.000Z"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] `status` = `"success"`
- [ ] `message` = `"Server is healthy"`
- [ ] `timestamp` có định dạng ISO 8601

---

### TC-SYS-002: Gọi route không tồn tại

|              |                                     |
| ------------ | ----------------------------------- |
| **Endpoint** | `GET /api/nonexistent`              |
| **Mô tả**    | Gọi đến route không được định nghĩa |

**Request:**

```
GET http://localhost:5000/api/nonexistent
```

**Expected Response (404 Not Found):**

```json
{
  "success": false,
  "message": "Route not found"
}
```

---

## 5. TC-AUTH: Kiểm Thử Xác Thực

### 5.1 Đăng Ký (Register)

**Endpoint:** `POST /api/auth/register`  
**Headers:** `Content-Type: application/json`

---

#### TC-AUTH-REG-001: Đăng ký thành công

|               |                                      |
| ------------- | ------------------------------------ |
| **Loại test** | Happy Path                           |
| **Mô tả**     | Đăng ký với email và password hợp lệ |

**Request Body:**

```json
{
  "email": "tester01@example.com",
  "password": "123456"
}
```

**Expected Response (201 Created):**

```json
{
  "success": true,
  "message": "User registered successfully",
  "data": {
    "id": "<mongodb_objectid>",
    "email": "tester01@example.com",
    "createdAt": "<timestamp>"
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 201
- [ ] `success` = `true`
- [ ] `data.email` trùng với email đã đăng ký (lowercase)
- [ ] `data.id` là MongoDB ObjectId hợp lệ
- [ ] `data` **KHÔNG** chứa trường `password`
- [ ] `data.createdAt` có giá trị timestamp

---

#### TC-AUTH-REG-002: Đăng ký với email đã tồn tại

|               |                                                    |
| ------------- | -------------------------------------------------- |
| **Loại test** | Negative - Duplicate                               |
| **Điều kiện** | Email `tester01@example.com` đã được đăng ký trước |

**Request Body:**

```json
{
  "email": "tester01@example.com",
  "password": "abcdef"
}
```

**Expected Response (409 Conflict):**

```json
{
  "success": false,
  "message": "Email already exists"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 409
- [ ] `success` = `false`
- [ ] Message đúng: `"Email already exists"`

---

#### TC-AUTH-REG-003: Email không hợp lệ

|               |                       |
| ------------- | --------------------- |
| **Loại test** | Negative - Validation |

**Request Body:**

```json
{
  "email": "not-an-email",
  "password": "123456"
}
```

**Expected Response (400 Bad Request):**

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": {
    "fieldErrors": {
      "email": ["Invalid email"]
    },
    "formErrors": []
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 400
- [ ] `errors.fieldErrors.email` có chứa thông báo lỗi

---

#### TC-AUTH-REG-004: Password quá ngắn (dưới 6 ký tự)

**Request Body:**

```json
{
  "email": "newuser@example.com",
  "password": "123"
}
```

**Expected Response (400 Bad Request):**

```json
{
  "success": false,
  "message": "Validation failed",
  "errors": {
    "fieldErrors": {
      "password": ["String must contain at least 6 character(s)"]
    },
    "formErrors": []
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 400
- [ ] `errors.fieldErrors.password` có chứa thông báo lỗi

---

#### TC-AUTH-REG-005: Thiếu trường bắt buộc

**Test 5a - Thiếu email:**

```json
{
  "password": "123456"
}
```

**Test 5b - Thiếu password:**

```json
{
  "email": "newuser@example.com"
}
```

**Test 5c - Body rỗng:**

```json
{}
```

**Expected:** Tất cả trả về 400, `message` = `"Validation failed"`, `errors.fieldErrors` chứa thông báo trường bị thiếu.

---

#### TC-AUTH-REG-006: Email dạng chữ hoa (case-insensitive)

|           |                                              |
| --------- | -------------------------------------------- |
| **Mô tả** | Hệ thống tự động chuyển email sang lowercase |

**Request Body:**

```json
{
  "email": "UPPER@EXAMPLE.COM",
  "password": "123456"
}
```

**Expected Response (201):**  
`data.email` = `"upper@example.com"` (đã lowercase)

---

#### TC-AUTH-REG-007: Dữ liệu biên (Boundary)

| Test                         | Email                 | Password   | Expected        |
| ---------------------------- | --------------------- | ---------- | --------------- |
| Password đúng 6 ký tự        | `boundary01@test.com` | `123456`   | 201 Created     |
| Password 5 ký tự             | `boundary02@test.com` | `12345`    | 400 Bad Request |
| Password rất dài (100 ký tự) | `boundary03@test.com` | `a` \* 100 | 201 Created     |

---

### 5.2 Đăng Nhập (Login)

**Endpoint:** `POST /api/auth/login`  
**Headers:** `Content-Type: application/json`

---

#### TC-AUTH-LOG-001: Đăng nhập thành công

|               |                                                  |
| ------------- | ------------------------------------------------ |
| **Loại test** | Happy Path                                       |
| **Điều kiện** | Tài khoản `tester01@example.com` đã được đăng ký |

**Request Body:**

```json
{
  "email": "tester01@example.com",
  "password": "123456"
}
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "<jwt_token_string>"
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] `success` = `true`
- [ ] `data.token` là chuỗi JWT (format: `xxxxx.yyyyy.zzzzz`)
- [ ] Token có thể decode và chứa `userId`, `email`

---

#### TC-AUTH-LOG-002: Sai password

**Request Body:**

```json
{
  "email": "tester01@example.com",
  "password": "wrongpassword"
}
```

**Expected Response (401 Unauthorized):**

```json
{
  "success": false,
  "message": "Invalid email or password"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 401
- [ ] Thông báo lỗi **không tiết lộ** email đúng/sai hay password đúng/sai (tránh enumeration)

---

#### TC-AUTH-LOG-003: Email không tồn tại

**Request Body:**

```json
{
  "email": "notexist@example.com",
  "password": "123456"
}
```

**Expected Response (401 Unauthorized):**

```json
{
  "success": false,
  "message": "Invalid email or password"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 401
- [ ] Message giống hệt TC-AUTH-LOG-002 (không phân biệt lỗi email hay password)

---

#### TC-AUTH-LOG-004: Validation lỗi khi login

| Test             | Input                                 | Expected               |
| ---------------- | ------------------------------------- | ---------------------- |
| Email sai format | `{"email":"abc","password":"123456"}` | 400, validation failed |
| Thiếu password   | `{"email":"test@test.com"}`           | 400, validation failed |
| Body rỗng        | `{}`                                  | 400, validation failed |

---

## 6. TC-CAT: Kiểm Thử Danh Mục (Categories)

### 6.1 Lấy Danh Sách Danh Mục

**Endpoint:** `GET /api/categories`

---

#### TC-CAT-GET-001: Lấy danh sách khi có dữ liệu

|               |                             |
| ------------- | --------------------------- |
| **Điều kiện** | DB đã có ít nhất 1 category |

**Request:**

```
GET http://localhost:5000/api/categories
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Categories fetched successfully",
  "data": [
    {
      "_id": "<objectid>",
      "name": "Electronics",
      "description": "Electronic devices",
      "createdAt": "<timestamp>",
      "updatedAt": "<timestamp>"
    }
  ]
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] `data` là array
- [ ] Mỗi item có `_id`, `name`, `createdAt`, `updatedAt`
- [ ] Danh sách sắp xếp theo `createdAt` giảm dần (mới nhất lên đầu)
- [ ] `description` xuất hiện nếu đã nhập, không xuất hiện nếu không nhập

---

#### TC-CAT-GET-002: Lấy danh sách khi DB rỗng

|               |                         |
| ------------- | ----------------------- |
| **Điều kiện** | DB chưa có category nào |

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Categories fetched successfully",
  "data": []
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200 (không phải 404)
- [ ] `data` là array rỗng `[]`

---

#### TC-CAT-GET-003: Không cần token

**Điểm kiểm tra:**

- [ ] Gọi không có header `Authorization` vẫn trả về 200

---

### 6.2 Tạo Danh Mục

**Endpoint:** `POST /api/categories`  
**Headers:** `Authorization: Bearer <token>`, `Content-Type: application/json`

---

#### TC-CAT-CRT-001: Tạo thành công có đầy đủ thông tin

**Request Body:**

```json
{
  "name": "Electronics",
  "description": "Các thiết bị điện tử"
}
```

**Expected Response (201 Created):**

```json
{
  "success": true,
  "message": "Category created successfully",
  "data": {
    "_id": "<objectid>",
    "name": "Electronics",
    "description": "Các thiết bị điện tử",
    "createdAt": "<timestamp>",
    "updatedAt": "<timestamp>"
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 201
- [ ] `data._id` là MongoDB ObjectId hợp lệ
- [ ] `data.name` đúng với input
- [ ] `data.description` đúng với input
- [ ] Kiểm tra trong DB: document được tạo

---

#### TC-CAT-CRT-002: Tạo thành công không có description

**Request Body:**

```json
{
  "name": "Fashion"
}
```

**Expected Response (201):**  
`data` không có trường `description` hoặc `description` = `undefined`.

---

#### TC-CAT-CRT-003: Tên category đã tồn tại

|               |                                     |
| ------------- | ----------------------------------- |
| **Điều kiện** | Category `"Electronics"` đã tồn tại |

**Request Body:**

```json
{
  "name": "Electronics",
  "description": "Mô tả khác"
}
```

**Expected Response (409 Conflict):**

```json
{
  "success": false,
  "message": "Category name already exists"
}
```

---

#### TC-CAT-CRT-004: Không có token (Unauthorized)

**Request:** Gọi POST không có header `Authorization`

**Expected Response (401):**

```json
{
  "success": false,
  "message": "Unauthorized: Bearer token is required"
}
```

---

#### TC-CAT-CRT-005: Token không hợp lệ

**Request Header:**

```
Authorization: Bearer invalid_token_string
```

**Expected Response (401):**

```json
{
  "success": false,
  "message": "Unauthorized: Invalid or expired token"
}
```

---

#### TC-CAT-CRT-006: Validation lỗi

| Test                       | Input                    | Expected                              |
| -------------------------- | ------------------------ | ------------------------------------- |
| `name` rỗng                | `{"name":""}`            | 400, validation failed                |
| Thiếu `name`               | `{"description":"test"}` | 400, validation failed                |
| Body rỗng                  | `{}`                     | 400, validation failed                |
| `name` chỉ có khoảng trắng | `{"name":"   "}`         | 201 hoặc 400 (kiểm tra trim behavior) |

---

### 6.3 Cập Nhật Danh Mục

**Endpoint:** `PUT /api/categories/:id`  
**Headers:** `Authorization: Bearer <token>`, `Content-Type: application/json`

---

#### TC-CAT-UPD-001: Cập nhật thành công

|               |                               |
| ------------- | ----------------------------- |
| **Điều kiện** | `categoryId` tồn tại trong DB |

**Request:**

```
PUT http://localhost:5000/api/categories/64abc123def456789012abcd
```

**Request Body:**

```json
{
  "name": "Electronics Updated",
  "description": "Mô tả mới"
}
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Category updated successfully",
  "data": {
    "_id": "64abc123def456789012abcd",
    "name": "Electronics Updated",
    "description": "Mô tả mới",
    "createdAt": "<original_timestamp>",
    "updatedAt": "<new_timestamp>"
  }
}
```

**Điểm kiểm tra:**

- [ ] `data.name` đã được cập nhật
- [ ] `data.updatedAt` > `data.createdAt`
- [ ] `data._id` không thay đổi

---

#### TC-CAT-UPD-002: ID không tồn tại trong DB

**Request:** PUT với một ObjectId hợp lệ nhưng không có trong DB  
(ví dụ: `000000000000000000000000`)

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Category not found"
}
```

---

#### TC-CAT-UPD-003: ID không phải ObjectId hợp lệ

**Request:**

```
PUT http://localhost:5000/api/categories/not-a-valid-id
```

**Expected Response (400):**

```json
{
  "success": false,
  "message": "Invalid category id"
}
```

---

#### TC-CAT-UPD-004: Đổi tên thành tên đã tồn tại (của category khác)

|               |                                                              |
| ------------- | ------------------------------------------------------------ |
| **Điều kiện** | Category `"Fashion"` đã tồn tại, đang cập nhật category khác |

**Request Body:**

```json
{
  "name": "Fashion"
}
```

**Expected Response (409):**

```json
{
  "success": false,
  "message": "Category name already exists"
}
```

---

#### TC-CAT-UPD-005: Cập nhật với tên giống như hiện tại (no-op)

|           |                                                          |
| --------- | -------------------------------------------------------- |
| **Mô tả** | Gửi chính xác tên hiện tại của category (không thay đổi) |

**Expected Response (200):**  
Trả về 200, data giống như cũ. Không bị lỗi 409.

---

#### TC-CAT-UPD-006: Không có token / token không hợp lệ

**Expected:** 401 (giống TC-CAT-CRT-004 và TC-CAT-CRT-005)

---

### 6.4 Xóa Danh Mục

**Endpoint:** `DELETE /api/categories/:id`  
**Headers:** `Authorization: Bearer <token>`

---

#### TC-CAT-DEL-001: Xóa thành công

**Request:**

```
DELETE http://localhost:5000/api/categories/<valid_existing_id>
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Category deleted successfully"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] Response **không** có trường `data`
- [ ] Kiểm tra DB: document đã bị xóa

---

#### TC-CAT-DEL-002: Xóa category không tồn tại

**Request:** DELETE với ObjectId hợp lệ nhưng không có trong DB

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Category not found"
}
```

---

#### TC-CAT-DEL-003: Xóa với ID không hợp lệ

**Request:**

```
DELETE http://localhost:5000/api/categories/invalid-id
```

**Expected Response (400):**

```json
{
  "success": false,
  "message": "Invalid category id"
}
```

---

#### TC-CAT-DEL-004: Không có token / token không hợp lệ

**Expected:** 401

---

## 7. TC-PROD: Kiểm Thử Sản Phẩm (Products)

### 7.1 Lấy Danh Sách Sản Phẩm

**Endpoint:** `GET /api/products`

---

#### TC-PROD-GET-001: Lấy danh sách thành công

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Products fetched successfully",
  "data": [
    {
      "_id": "<objectid>",
      "name": "iPhone 15",
      "price": 25000000,
      "stock": 50,
      "categoryId": {
        "_id": "<cat_objectid>",
        "name": "Electronics",
        "description": "Thiết bị điện tử"
      },
      "createdAt": "<timestamp>",
      "updatedAt": "<timestamp>"
    }
  ]
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] `data` là array
- [ ] `categoryId` được **populate** (là object, không phải string ID)
- [ ] `categoryId` chứa `_id`, `name`, `description`
- [ ] Danh sách sắp xếp theo `createdAt` giảm dần

---

#### TC-PROD-GET-002: Danh sách rỗng

**Expected:** 200, `data: []`

---

#### TC-PROD-GET-003: Không cần token

**Điểm kiểm tra:**

- [ ] Gọi không có `Authorization` header vẫn trả về 200

---

### 7.2 Tạo Sản Phẩm

**Endpoint:** `POST /api/products`  
**Headers:** `Authorization: Bearer <token>`, `Content-Type: application/json`

---

#### TC-PROD-CRT-001: Tạo thành công

|               |                               |
| ------------- | ----------------------------- |
| **Điều kiện** | `categoryId` tồn tại trong DB |

**Request Body:**

```json
{
  "name": "iPhone 15 Pro Max",
  "price": 33990000,
  "stock": 100,
  "categoryId": "64abc123def456789012abcd"
}
```

**Expected Response (201 Created):**

```json
{
  "success": true,
  "message": "Product created successfully",
  "data": {
    "_id": "<objectid>",
    "name": "iPhone 15 Pro Max",
    "price": 33990000,
    "stock": 100,
    "categoryId": "64abc123def456789012abcd",
    "createdAt": "<timestamp>",
    "updatedAt": "<timestamp>"
  }
}
```

**Điểm kiểm tra:**

- [ ] Status code = 201
- [ ] Tất cả fields đúng với input
- [ ] Kiểm tra DB: document được tạo

---

#### TC-PROD-CRT-002: categoryId không tồn tại trong DB

**Request Body:**

```json
{
  "name": "Test Product",
  "price": 100000,
  "stock": 10,
  "categoryId": "000000000000000000000000"
}
```

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Category not found"
}
```

---

#### TC-PROD-CRT-003: categoryId không phải ObjectId hợp lệ

**Request Body:**

```json
{
  "name": "Test Product",
  "price": 100000,
  "stock": 10,
  "categoryId": "not-valid-id"
}
```

**Expected Response (400):**

```json
{
  "success": false,
  "message": "Invalid categoryId"
}
```

---

#### TC-PROD-CRT-004: Validation lỗi - price âm

**Request Body:**

```json
{
  "name": "Test",
  "price": -1,
  "stock": 10,
  "categoryId": "64abc123def456789012abcd"
}
```

**Expected Response (400):** `message: "Validation failed"`, lỗi tại field `price`

---

#### TC-PROD-CRT-005: Validation lỗi - stock âm

**Request Body:**

```json
{
  "name": "Test",
  "price": 100,
  "stock": -5,
  "categoryId": "64abc123def456789012abcd"
}
```

**Expected Response (400):** `message: "Validation failed"`, lỗi tại field `stock`

---

#### TC-PROD-CRT-006: Validation lỗi - stock là số thập phân

**Request Body:**

```json
{
  "name": "Test",
  "price": 100,
  "stock": 1.5,
  "categoryId": "64abc123def456789012abcd"
}
```

**Expected Response (400):** `stock` phải là số nguyên, lỗi validation

---

#### TC-PROD-CRT-007: Giá trị biên cho price và stock

| Test                 | price  | stock    | Expected    |
| -------------------- | ------ | -------- | ----------- |
| Giá = 0 (miễn phí)   | `0`    | `0`      | 201 Created |
| Giá = 0.01           | `0.01` | `1`      | 201 Created |
| Stock = 0 (hết hàng) | `100`  | `0`      | 201 Created |
| Stock rất lớn        | `100`  | `999999` | 201 Created |

---

#### TC-PROD-CRT-008: Thiếu trường bắt buộc

| Thiếu trường       | Expected               |
| ------------------ | ---------------------- |
| Thiếu `name`       | 400, validation failed |
| Thiếu `price`      | 400, validation failed |
| Thiếu `stock`      | 400, validation failed |
| Thiếu `categoryId` | 400, validation failed |

---

#### TC-PROD-CRT-009: Không có token / token không hợp lệ

**Expected:** 401

---

### 7.3 Cập Nhật Sản Phẩm

**Endpoint:** `PUT /api/products/:id`  
**Headers:** `Authorization: Bearer <token>`, `Content-Type: application/json`

---

#### TC-PROD-UPD-001: Cập nhật thành công

**Request Body:**

```json
{
  "name": "iPhone 15 Pro Max 512GB",
  "price": 35990000,
  "stock": 80,
  "categoryId": "64abc123def456789012abcd"
}
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Product updated successfully",
  "data": {
    "_id": "<product_id>",
    "name": "iPhone 15 Pro Max 512GB",
    "price": 35990000,
    "stock": 80,
    "categoryId": {
      "_id": "64abc123def456789012abcd",
      "name": "Electronics",
      "description": "..."
    },
    "createdAt": "<original>",
    "updatedAt": "<new>"
  }
}
```

**Điểm kiểm tra:**

- [ ] `categoryId` trong response được **populate** (là object)
- [ ] `updatedAt` > `createdAt`

---

#### TC-PROD-UPD-002: Product ID không tồn tại

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Product not found"
}
```

---

#### TC-PROD-UPD-003: Product ID không phải ObjectId hợp lệ

**Request:**

```
PUT http://localhost:5000/api/products/invalid-id
```

**Expected Response (400):**

```json
{
  "success": false,
  "message": "Invalid product id"
}
```

---

#### TC-PROD-UPD-004: categoryId mới không tồn tại

|           |                                              |
| --------- | -------------------------------------------- |
| **Mô tả** | Cập nhật product sang category không tồn tại |

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Category not found"
}
```

---

#### TC-PROD-UPD-005: Không có token / token không hợp lệ

**Expected:** 401

---

### 7.4 Xóa Sản Phẩm

**Endpoint:** `DELETE /api/products/:id`  
**Headers:** `Authorization: Bearer <token>`

---

#### TC-PROD-DEL-001: Xóa thành công

**Expected Response (200 OK):**

```json
{
  "success": true,
  "message": "Product deleted successfully"
}
```

**Điểm kiểm tra:**

- [ ] Status code = 200
- [ ] Response không có trường `data`
- [ ] Kiểm tra DB: document đã bị xóa

---

#### TC-PROD-DEL-002: Product không tồn tại

**Expected Response (404):**

```json
{
  "success": false,
  "message": "Product not found"
}
```

---

#### TC-PROD-DEL-003: Product ID không hợp lệ

**Expected Response (400):**

```json
{
  "success": false,
  "message": "Invalid product id"
}
```

---

#### TC-PROD-DEL-004: Không có token / token không hợp lệ

**Expected:** 401

---

## 8. TC-SEC: Kiểm Thử Bảo Mật

---

#### TC-SEC-001: Password không lưu dạng plain text

|           |                                                              |
| --------- | ------------------------------------------------------------ |
| **Mô tả** | Sau khi đăng ký, kiểm tra DB xem password có được hash không |

**Cách kiểm tra:**  
Dùng MongoDB Compass, tìm user vừa tạo, xác nhận trường `password` có dạng `$2a$10$...` (bcrypt hash), **không phải** plain text.

---

#### TC-SEC-002: Response đăng ký không trả về password

**Điểm kiểm tra:**

- [ ] Response của `POST /api/auth/register` không có trường `password`

---

#### TC-SEC-003: Token hết hạn

|           |                                                               |
| --------- | ------------------------------------------------------------- |
| **Mô tả** | Sử dụng token đã hết hạn (nếu `JWT_EXPIRES_IN` được set ngắn) |

**Expected Response (401):**

```json
{
  "success": false,
  "message": "Unauthorized: Invalid or expired token"
}
```

---

#### TC-SEC-004: Giả mạo token

**Request Header:**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.fake.signature
```

**Expected Response (401):**

```json
{
  "success": false,
  "message": "Unauthorized: Invalid or expired token"
}
```

---

#### TC-SEC-005: Sai format Authorization header

| Test            | Header                         | Expected                   |
| --------------- | ------------------------------ | -------------------------- |
| Thiếu "Bearer " | `Authorization: <token>`       | 401, Bearer token required |
| Dùng "Basic "   | `Authorization: Basic <token>` | 401, Bearer token required |
| Header rỗng     | `Authorization: `              | 401, Bearer token required |

---

#### TC-SEC-006: Tấn công brute force login (quan sát)

|             |                                                         |
| ----------- | ------------------------------------------------------- |
| **Mô tả**   | Gọi `POST /api/auth/login` sai password liên tục 10 lần |
| **Kỳ vọng** | Server vẫn trả về 401 (không crash, không lock)         |

> **Ghi chú:** API hiện tại chưa có rate limiting. Đây là điểm cần cải thiện.

---

## 9. Dữ Liệu Test Mẫu (Test Data)

### 9.1 Tài Khoản Test

| Vai trò          | Email              | Password    | Ghi chú                   |
| ---------------- | ------------------ | ----------- | ------------------------- |
| Admin/User chính | `admin@test.com`   | `Admin123`  | Tạo trước khi test        |
| User phụ         | `user2@test.com`   | `User2456`  | Dùng test duplicate email |
| User bị xóa      | `deleted@test.com` | `Delete789` | Đăng ký rồi test sau      |

### 9.2 Danh Mục Test

```json
[
  {
    "name": "Điện tử",
    "description": "Thiết bị điện tử, điện thoại, laptop"
  },
  {
    "name": "Thời trang",
    "description": "Quần áo, giày dép, phụ kiện"
  },
  {
    "name": "Thực phẩm",
    "description": "Đồ ăn, thức uống các loại"
  },
  {
    "name": "Sách",
    "description": "Sách giáo khoa, tiểu thuyết, kỹ năng"
  },
  {
    "name": "No Description Category"
  }
]
```

### 9.3 Sản Phẩm Test

```json
[
  {
    "name": "iPhone 15 Pro",
    "price": 27990000,
    "stock": 50,
    "categoryId": "<id_danh_muc_dien_tu>"
  },
  {
    "name": "Samsung Galaxy S24",
    "price": 22990000,
    "stock": 30,
    "categoryId": "<id_danh_muc_dien_tu>"
  },
  {
    "name": "Áo Thun Nam",
    "price": 199000,
    "stock": 200,
    "categoryId": "<id_danh_muc_thoi_trang>"
  },
  {
    "name": "Sản phẩm miễn phí",
    "price": 0,
    "stock": 999,
    "categoryId": "<id_bat_ky>"
  },
  {
    "name": "Sản phẩm hết hàng",
    "price": 500000,
    "stock": 0,
    "categoryId": "<id_bat_ky>"
  }
]
```

### 9.4 Dữ Liệu Biên (Edge Cases)

| Loại                       | Giá trị                           |
| -------------------------- | --------------------------------- |
| Tên rất dài (255 ký tự)    | `"A".repeat(255)`                 |
| Ký tự đặc biệt trong tên   | `"Test <>&\"'"`                   |
| Giá bằng 0                 | `0`                               |
| Stock bằng 0               | `0`                               |
| MongoDB ObjectId giả       | `"000000000000000000000000"`      |
| String không phải ObjectId | `"abc"`, `"12345"`, `"not-an-id"` |

---

## 10. Checklist Tổng Hợp

### Auth

- [ ] TC-AUTH-REG-001: Đăng ký thành công
- [ ] TC-AUTH-REG-002: Email trùng → 409
- [ ] TC-AUTH-REG-003: Email sai format → 400
- [ ] TC-AUTH-REG-004: Password < 6 ký tự → 400
- [ ] TC-AUTH-REG-005: Thiếu field → 400
- [ ] TC-AUTH-REG-006: Email uppercase → lowercase
- [ ] TC-AUTH-REG-007: Boundary password
- [ ] TC-AUTH-LOG-001: Đăng nhập thành công → token
- [ ] TC-AUTH-LOG-002: Sai password → 401
- [ ] TC-AUTH-LOG-003: Email không tồn tại → 401
- [ ] TC-AUTH-LOG-004: Validation lỗi → 400

### Categories

- [ ] TC-CAT-GET-001: Lấy danh sách có data
- [ ] TC-CAT-GET-002: Danh sách rỗng
- [ ] TC-CAT-GET-003: Không cần token
- [ ] TC-CAT-CRT-001: Tạo có description → 201
- [ ] TC-CAT-CRT-002: Tạo không có description → 201
- [ ] TC-CAT-CRT-003: Tên trùng → 409
- [ ] TC-CAT-CRT-004: Không có token → 401
- [ ] TC-CAT-CRT-005: Token không hợp lệ → 401
- [ ] TC-CAT-CRT-006: Validation lỗi → 400
- [ ] TC-CAT-UPD-001: Cập nhật thành công → 200
- [ ] TC-CAT-UPD-002: ID không tồn tại → 404
- [ ] TC-CAT-UPD-003: ID không hợp lệ → 400
- [ ] TC-CAT-UPD-004: Tên trùng category khác → 409
- [ ] TC-CAT-UPD-005: Tên giống hiện tại → 200
- [ ] TC-CAT-UPD-006: Không có token → 401
- [ ] TC-CAT-DEL-001: Xóa thành công → 200
- [ ] TC-CAT-DEL-002: ID không tồn tại → 404
- [ ] TC-CAT-DEL-003: ID không hợp lệ → 400
- [ ] TC-CAT-DEL-004: Không có token → 401

### Products

- [ ] TC-PROD-GET-001: Lấy danh sách với populate
- [ ] TC-PROD-GET-002: Danh sách rỗng
- [ ] TC-PROD-GET-003: Không cần token
- [ ] TC-PROD-CRT-001: Tạo thành công → 201
- [ ] TC-PROD-CRT-002: categoryId không tồn tại → 404
- [ ] TC-PROD-CRT-003: categoryId không hợp lệ → 400
- [ ] TC-PROD-CRT-004: price âm → 400
- [ ] TC-PROD-CRT-005: stock âm → 400
- [ ] TC-PROD-CRT-006: stock thập phân → 400
- [ ] TC-PROD-CRT-007: Boundary price=0, stock=0 → 201
- [ ] TC-PROD-CRT-008: Thiếu field → 400
- [ ] TC-PROD-CRT-009: Không có token → 401
- [ ] TC-PROD-UPD-001: Cập nhật thành công → 200
- [ ] TC-PROD-UPD-002: Product ID không tồn tại → 404
- [ ] TC-PROD-UPD-003: Product ID không hợp lệ → 400
- [ ] TC-PROD-UPD-004: categoryId mới không tồn tại → 404
- [ ] TC-PROD-UPD-005: Không có token → 401
- [ ] TC-PROD-DEL-001: Xóa thành công → 200
- [ ] TC-PROD-DEL-002: Product không tồn tại → 404
- [ ] TC-PROD-DEL-003: Product ID không hợp lệ → 400
- [ ] TC-PROD-DEL-004: Không có token → 401

### Security

- [ ] TC-SEC-001: Password được hash trong DB
- [ ] TC-SEC-002: Response không trả về password
- [ ] TC-SEC-003: Token hết hạn → 401
- [ ] TC-SEC-004: Token giả mạo → 401
- [ ] TC-SEC-005: Sai format Authorization → 401
- [ ] TC-SEC-006: Brute force không crash server

---

_Tài liệu này được tạo tự động dựa trên phân tích source code. Cập nhật khi có thay đổi API._
