# 📋 User API Reference — Tổng hợp tất cả API liên quan đến User

> **Cập nhật:** 07/02/2026  
> **Module:** `ClassifiedAds.Modules.Identity`  
> **Tổng số endpoints:** **31 API**

---

## 📊 Tổng quan

| Nhóm | Số lượng | Controller |
|-------|----------|------------|
| 🔐 Authentication & Self-Service | 13 | `AuthController` |
| 👤 Admin User Management | 13 | `UsersController` |
| 🛡️ Role Management | 5 | `RolesController` |
| **Tổng cộng** | **31** | |

---

## 🔐 1. AuthController — Authentication & Self-Service

**File:** `ClassifiedAds.Modules.Identity/Controllers/AuthController.cs`  
**Base Route:** `api/auth`  
**Rate Limiting:** Default + Auth/Password-specific policies

| # | Method | Route | Auth | Mô tả |
|---|--------|-------|------|--------|
| 1 | `POST` | `api/auth/register` | 🔓 Anonymous | Đăng ký tài khoản mới |
| 2 | `POST` | `api/auth/login` | 🔓 Anonymous | Đăng nhập bằng email & password |
| 3 | `POST` | `api/auth/refresh-token` | 🔓 Anonymous | Làm mới access token (token rotation) |
| 4 | `POST` | `api/auth/logout` | 🔒 Authorized | Đăng xuất & thu hồi refresh token |
| 5 | `GET` | `api/auth/me` | 🔒 Authorized | Lấy thông tin user đang đăng nhập |
| 6 | `POST` | `api/auth/forgot-password` | 🔓 Anonymous | Yêu cầu email reset password |
| 7 | `POST` | `api/auth/reset-password` | 🔓 Anonymous | Reset password bằng token từ email |
| 8 | `POST` | `api/auth/change-password` | 🔒 Authorized | Đổi password cho user đang đăng nhập |
| 9 | `POST` | `api/auth/confirm-email` | 🔓 Anonymous | Xác nhận email bằng token |
| 10 | `POST` | `api/auth/resend-confirmation-email` | 🔓 Anonymous | Gửi lại email xác nhận |
| 11 | `GET` | `api/auth/me/profile` | 🔒 Authorized | Lấy profile của user hiện tại |
| 12 | `PUT` | `api/auth/me/profile` | 🔒 Authorized | Cập nhật profile của user hiện tại |
| 13 | `POST` | `api/auth/me/avatar` | 🔒 Authorized | Upload avatar (max 2MB, JPEG/PNG/GIF/WebP) |

### Chi tiết Request/Response

#### 1. POST `api/auth/register`
```
Request:  { Email*, Password*, ConfirmPassword* }
Response: 201 → { UserId, Email, Message, EmailConfirmationRequired }
```

#### 2. POST `api/auth/login`
```
Request:  { Email*, Password*, RememberMe }
Response: 200 → { AccessToken, RefreshToken, TokenType, ExpiresIn, User }
```

#### 3. POST `api/auth/refresh-token`
```
Request:  { RefreshToken* }
Response: 200 → { AccessToken, RefreshToken, TokenType, ExpiresIn }
```

#### 4. POST `api/auth/logout`
```
Request:  (none)
Response: 200 → { Message }
```

#### 5. GET `api/auth/me`
```
Response: 200 → { Id, UserName, Email, EmailConfirmed, PhoneNumber, 
                   PhoneNumberConfirmed, TwoFactorEnabled, Roles }
```

#### 6. POST `api/auth/forgot-password`
```
Request:  { Email* }
Response: 200 → { Message } (luôn trả success để tránh user enumeration)
```

#### 7. POST `api/auth/reset-password`
```
Request:  { Email*, Token*, NewPassword*, ConfirmPassword* }
Response: 200 → { Message }
```

#### 8. POST `api/auth/change-password`
```
Request:  { CurrentPassword*, NewPassword*, ConfirmPassword* }
Response: 200 → { Message }
```

#### 9. POST `api/auth/confirm-email`
```
Request:  { Email*, Token* }
Response: 200 → { Message }
```

#### 10. POST `api/auth/resend-confirmation-email`
```
Request:  { Email* }
Response: 200 → { Message } (luôn trả success)
```

#### 11. GET `api/auth/me/profile`
```
Response: 200 → { UserId, Email, UserName, DisplayName, AvatarUrl, 
                   Timezone, PhoneNumber, EmailConfirmed, PhoneNumberConfirmed }
```

#### 12. PUT `api/auth/me/profile`
```
Request:  { DisplayName, Timezone, PhoneNumber }
Response: 200 → { Message }
```

#### 13. POST `api/auth/me/avatar`
```
Request:  multipart/form-data (file, max 2MB)
Response: 200 → { AvatarUrl, Message }
```

---

## 👤 2. UsersController — Admin User Management

**File:** `ClassifiedAds.Modules.Identity/Controllers/UsersController.cs`  
**Base Route:** `api/users`  
**Auth:** Tất cả đều yêu cầu `[Authorize]` + Permission policy cụ thể

| # | Method | Route | Permission | Mô tả |
|---|--------|-------|------------|--------|
| 14 | `GET` | `api/users` | UsersView | Danh sách user (phân trang, tìm kiếm, filter) |
| 15 | `GET` | `api/users/{id}` | UsersView | Lấy thông tin 1 user theo ID |
| 16 | `POST` | `api/users` | UsersCreate | Tạo user mới (admin) |
| 17 | `PUT` | `api/users/{id}` | UsersEdit | Cập nhật thông tin user |
| 18 | `PUT` | `api/users/{id}/password` | UsersEdit | Admin đặt lại password cho user |
| 19 | `DELETE` | `api/users/{id}` | UsersDelete | Xóa user |
| 20 | `POST` | `api/users/{id}/password-reset-email` | UsersEdit | Admin gửi email reset password cho user |
| 21 | `POST` | `api/users/{id}/email-confirmation` | UsersEdit | Admin gửi email xác nhận cho user |
| 22 | `GET` | `api/users/{id}/roles` | UsersView | Lấy danh sách role của user |
| 23 | `POST` | `api/users/{id}/roles` | UsersEdit | Gán role cho user |
| 24 | `DELETE` | `api/users/{id}/roles/{roleId}` | UsersEdit | Xóa role khỏi user |
| 25 | `POST` | `api/users/{id}/lock` | UsersEdit | Khóa/ban tài khoản user |
| 26 | `POST` | `api/users/{id}/unlock` | UsersEdit | Mở khóa tài khoản user |

### Chi tiết Request/Response

#### 14. GET `api/users`
```
Query Params: Page, PageSize, Search, SortBy, SortDirection, Status
Response: 200 → { TotalItems, Items[], Page, PageSize, TotalPages, 
                   HasPreviousPage, HasNextPage }
```

#### 15. GET `api/users/{id}`
```
Path: id (Guid)
Response: 200 → UserDto | 404 Not Found
```

#### 16. POST `api/users`
```
Request:  { UserName, Email*, Password*, PhoneNumber, RoleName (default: "User") }
Response: 201 → UserDto
```

#### 17. PUT `api/users/{id}`
```
Request:  { UserName, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, 
            TwoFactorEnabled, LockoutEnabled, LockoutEnd, AccessFailedCount }
Response: 200 → UserDto | 404
```

#### 18. PUT `api/users/{id}/password`
```
Request:  { Password* }
Response: 200 | 400
```

#### 19. DELETE `api/users/{id}`
```
Path: id (Guid)
Response: 200 | 404
```

#### 20. POST `api/users/{id}/password-reset-email`
```
Path: id (Guid)
Response: 200 → { Message } | 404
```

#### 21. POST `api/users/{id}/email-confirmation`
```
Path: id (Guid)
Response: 200 → { Message } | 404
```

#### 22. GET `api/users/{id}/roles`
```
Path: id (Guid)
Response: 200 → RoleDto[] | 404
```

#### 23. POST `api/users/{id}/roles`
```
Request:  { RoleId* (Guid) }
Response: 200 → { Message } | 400 | 404
```

#### 24. DELETE `api/users/{id}/roles/{roleId}`
```
Path: id (Guid), roleId (Guid)
Response: 200 → { Message } | 400 | 404
```

#### 25. POST `api/users/{id}/lock`
```
Request:  { Days? (default 30), Permanent (default false), Reason? }
Response: 200 → { Message } | 400 | 404
```

#### 26. POST `api/users/{id}/unlock`
```
Path: id (Guid)
Response: 200 → { Message } | 404
```

---

## 🛡️ 3. RolesController — Role Management

**File:** `ClassifiedAds.Modules.Identity/Controllers/RolesController.cs`  
**Base Route:** `api/roles`  
**Auth:** Tất cả đều yêu cầu `[Authorize]` + Permission policy cụ thể

| # | Method | Route | Permission | Mô tả |
|---|--------|-------|------------|--------|
| 27 | `GET` | `api/roles` | RolesView | Danh sách tất cả roles |
| 28 | `GET` | `api/roles/{id}` | RolesView | Lấy thông tin 1 role theo ID |
| 29 | `POST` | `api/roles` | RolesCreate | Tạo role mới |
| 30 | `PUT` | `api/roles/{id}` | RolesEdit | Cập nhật role |
| 31 | `DELETE` | `api/roles/{id}` | RolesDelete | Xóa role |

### Chi tiết Request/Response

#### 27. GET `api/roles`
```
Response: 200 → RoleDto[]
```

#### 28. GET `api/roles/{id}`
```
Path: id (Guid)
Response: 200 → { Id, Name, NormalizedName, ConcurrencyStamp } | 404
```

#### 29. POST `api/roles`
```
Request:  { Name* }
Response: 201 → RoleDto
```

#### 30. PUT `api/roles/{id}`
```
Request:  { Name* }
Response: 200 → RoleDto | 404
```

#### 31. DELETE `api/roles/{id}`
```
Path: id (Guid)
Response: 200 | 404
```

---

## 📦 DTOs / Models Summary

### Auth DTOs (`ClassifiedAds.Modules.Identity/Models/Auth/`)

| Model | Sử dụng tại | Fields |
|-------|-------------|--------|
| `LoginRequestDto` | POST login | Email*, Password*, RememberMe |
| `LoginResponseDto` | Login/RefreshToken response | AccessToken, RefreshToken, TokenType, ExpiresIn, User |
| `UserInfoDto` | me/login response | Id, UserName, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, Roles |
| `RegisterRequestDto` | POST register | Email*, Password*, ConfirmPassword* |
| `RegisterResponseDto` | Register response | UserId, Email, Message, EmailConfirmationRequired |
| `EmailRequestDto` | forgot-password / resend-confirmation | Email* |
| `ResetPasswordRequestDto` | POST reset-password | Email*, Token*, NewPassword*, ConfirmPassword* |
| `ChangePasswordRequestDto` | POST change-password | CurrentPassword*, NewPassword*, ConfirmPassword* |
| `RefreshTokenRequestDto` | POST refresh-token | RefreshToken* |
| `ConfirmEmailRequestDto` | POST confirm-email | Email*, Token* |
| `UserProfileDto` | Profile response | UserId, Email, UserName, DisplayName, AvatarUrl, Timezone, PhoneNumber, EmailConfirmed, PhoneNumberConfirmed |
| `UpdateProfileRequestDto` | PUT me/profile | DisplayName, Timezone, PhoneNumber |
| `AvatarResponseDto` | Avatar upload response | AvatarUrl, Message |

### User Admin DTOs (`ClassifiedAds.Modules.Identity/Models/`)

| Model | Sử dụng tại | Fields |
|-------|-------------|--------|
| `UserDto` | Admin user CRUD | Id, UserName, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, LockoutEnd, AccessFailedCount, … |
| `CreateUserRequestDto` | POST users (admin create) | UserName, Email*, Password*, PhoneNumber, RoleName (default: "User") |
| `AssignRoleRequestDto` | POST users/{id}/roles | RoleId* (Guid) |
| `LockUserRequestDto` | POST users/{id}/lock | Days? (default 30), Permanent (default false), Reason? |
| `RoleDto` | Role CRUD | Id, Name, NormalizedName, ConcurrencyStamp |

---

## 🔒 Bảo mật & Tính năng nổi bật

| Tính năng | Mô tả |
|-----------|--------|
| **JWT Authentication** | Access Token + Refresh Token với token rotation |
| **Rate Limiting** | Giới hạn request cho auth & password endpoints |
| **Permission-based Authorization** | Phân quyền chi tiết theo từng action (View/Create/Edit/Delete) |
| **Anti-enumeration** | Forgot-password & resend-confirmation luôn trả 200 |
| **Account Locking** | Hỗ trợ lock tạm thời (theo ngày) hoặc vĩnh viễn |
| **Email Confirmation** | Bắt buộc xác nhận email khi đăng ký |
| **Avatar Upload** | Hỗ trợ upload ảnh đại diện (JPEG/PNG/GIF/WebP, max 2MB) |
| **Profile Management** | User tự quản lý profile (DisplayName, Timezone, Phone) |

---

> **Ghi chú:** Tất cả API User đều nằm trong module `ClassifiedAds.Modules.Identity`. Không có controller user nào nằm ngoài module này.
