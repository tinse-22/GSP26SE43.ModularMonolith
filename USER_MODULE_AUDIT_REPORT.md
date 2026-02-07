# User Module Audit Report

> **Audit Date:** February 6, 2026  
> **Auditor:** AI Software Auditor  
> **Project:** ClassifiedAds.ModularMonolith  
> **Module:** ClassifiedAds.Modules.Identity

---

## Executive Summary

Hệ thống User Management của dự án đã được triển khai khá đầy đủ với các chức năng authentication và authorization cơ bản. Tuy nhiên, còn một số API quan trọng chưa được implement và có một vài vấn đề security cần được khắc phục.

**Overall Score: 7.5/10**

---

## 1. Existing APIs

### 1.1 Authentication APIs (`AuthController`)

| Method | Endpoint | Description | Input | Output | Auth Required |
|--------|----------|-------------|-------|--------|---------------|
| POST | `/api/auth/login` | Login with email/password | `LoginModel` (email, password) | `LoginResponseModel` (accessToken, refreshToken, user) | ❌ No |
| POST | `/api/auth/refresh-token` | Refresh access token | `RefreshTokenModel` (refreshToken) | `LoginResponseModel` | ❌ No |
| POST | `/api/auth/logout` | Logout & revoke refresh token | - | Success message | ✅ Yes |
| GET | `/api/auth/me` | Get current user info | - | `UserInfoModel` | ✅ Yes |
| POST | `/api/auth/forgot-password` | Request password reset email | `ForgotPasswordModel` (email) | Success message | ❌ No |
| POST | `/api/auth/reset-password` | Reset password with token | `ResetPasswordModel` (email, token, newPassword) | Success message | ❌ No |
| POST | `/api/auth/change-password` | Change password (authenticated) | `ChangePasswordModel` (currentPassword, newPassword) | Success message | ✅ Yes |
| POST | `/api/auth/confirm-email` | Confirm email address | `ConfirmEmailModel` (email, token) | Success message | ❌ No |
| POST | `/api/auth/resend-confirmation-email` | Resend email confirmation | `ForgotPasswordModel` (email) | Success message | ❌ No |

### 1.2 User Management APIs (`UsersController`)

| Method | Endpoint | Description | Input | Output | Auth Required | Permission |
|--------|----------|-------------|-------|--------|---------------|------------|
| GET | `/api/users` | Get all users | - | `List<UserModel>` | ✅ Yes | `GetUsers` |
| GET | `/api/users/{id}` | Get user by ID | `id` (Guid) | `UserModel` | ✅ Yes | `GetUser` |
| POST | `/api/users` | Create new user | `CreateUserModel` | `UserModel` + role info | ✅ Yes | `AddUser` |
| PUT | `/api/users/{id}` | Update user | `id`, `UserModel` | `UserModel` | ✅ Yes | `UpdateUser` |
| DELETE | `/api/users/{id}` | Delete user | `id` (Guid) | - | ✅ Yes | `DeleteUser` |
| PUT | `/api/users/{id}/password` | Set user password (admin) | `id`, `SetPasswordModel` | - | ✅ Yes | `SetPassword` |
| POST | `/api/users/{id}/passwordresetemail` | Send password reset email | `id` (Guid) | - | ✅ Yes | `SendResetPasswordEmail` |
| POST | `/api/users/{id}/emailaddressconfirmation` | Send email confirmation | `id` (Guid) | - | ✅ Yes | `SendConfirmationEmailAddressEmail` |

### 1.3 Role Management APIs (`RolesController`)

| Method | Endpoint | Description | Input | Output | Auth Required | Permission |
|--------|----------|-------------|-------|--------|---------------|------------|
| GET | `/api/roles` | Get all roles | - | `List<RoleModel>` | ✅ Yes | `GetRoles` |
| GET | `/api/roles/{id}` | Get role by ID | `id` (Guid) | `RoleModel` | ✅ Yes | `GetRole` |
| POST | `/api/roles` | Create new role | `RoleModel` | `RoleModel` | ✅ Yes | `AddRole` |
| PUT | `/api/roles/{id}` | Update role | `id`, `RoleModel` | `RoleModel` | ✅ Yes | `UpdateRole` |
| DELETE | `/api/roles/{id}` | Delete role | `id` (Guid) | - | ✅ Yes | `DeleteRole` |

---

## 2. Missing APIs (Recommended)

### 2.1 Authentication & Account

| API Needed | Endpoint | Description | Priority | Reason |
|------------|----------|-------------|----------|--------|
| ❌ Self Registration | `POST /api/auth/register` | Public user registration | 🔴 **HIGH** | Thiếu API cho phép user tự đăng ký tài khoản. Hiện tại chỉ admin mới có thể tạo user qua `/api/users`. |
| ⚠️ Enable/Disable 2FA | `POST /api/auth/2fa/enable` | Enable Two-Factor Auth | 🟡 MEDIUM | Có field `TwoFactorEnabled` trong User entity nhưng chưa có API để enable/disable. |
| ⚠️ 2FA Verification | `POST /api/auth/2fa/verify` | Verify 2FA code | 🟡 MEDIUM | Cần thiết nếu enable 2FA feature. |
| ⚠️ 2FA Recovery Codes | `POST /api/auth/2fa/recovery-codes` | Generate recovery codes | 🟡 MEDIUM | Backup method khi mất 2FA device. |

### 2.2 User Profile

| API Needed | Endpoint | Description | Priority | Reason |
|------------|----------|-------------|----------|--------|
| ❌ Update Profile | `PUT /api/auth/me/profile` | Update own profile | 🔴 **HIGH** | Có `UserProfile` entity với `DisplayName`, `AvatarUrl`, `Timezone` nhưng chưa có API để cập nhật. |
| ❌ Upload Avatar | `POST /api/auth/me/avatar` | Upload profile picture | 🟡 MEDIUM | Có field `AvatarUrl` trong `UserProfile` nhưng chưa có upload API. |
| ⚠️ Update Email | `POST /api/auth/me/email` | Change email address | 🟡 MEDIUM | Cần verify email mới trước khi đổi. |
| ⚠️ Update Phone | `POST /api/auth/me/phone` | Change phone number | 🟢 LOW | Optional feature. |

### 2.3 Admin User Management

| API Needed | Endpoint | Description | Priority | Reason |
|------------|----------|-------------|----------|--------|
| ❌ List Users with Pagination | `GET /api/users?page=1&pageSize=10` | Paginated user list | 🔴 **HIGH** | Hiện tại GET `/api/users` trả về tất cả users, không có pagination. |
| ❌ Filter/Search Users | `GET /api/users?email=&role=&status=` | Filter users by criteria | 🔴 **HIGH** | Thiếu chức năng search/filter users. |
| ❌ Assign Role to User | `POST /api/users/{id}/roles` | Assign role | 🔴 **HIGH** | Chỉ assign role khi tạo user, không thể đổi role sau đó qua API. |
| ❌ Remove Role from User | `DELETE /api/users/{id}/roles/{roleId}` | Remove role | 🟡 MEDIUM | Cần thiết cho quản lý user. |
| ⚠️ Ban/Deactivate User | `POST /api/users/{id}/ban` | Ban user | 🟡 MEDIUM | Có `LockoutEnabled` và `LockoutEnd` nhưng chưa có API riêng. |
| ⚠️ Activate User | `POST /api/users/{id}/activate` | Reactivate user | 🟡 MEDIUM | Unban user. |
| ⚠️ Get User Activity Logs | `GET /api/users/{id}/activity` | User audit trail | 🟢 LOW | Xem lịch sử hoạt động của user. |

### 2.4 Security & Session

| API Needed | Endpoint | Description | Priority | Reason |
|------------|----------|-------------|----------|--------|
| ⚠️ Get Active Sessions | `GET /api/auth/sessions` | List active sessions | 🟡 MEDIUM | Xem các thiết bị đang đăng nhập. |
| ⚠️ Revoke Session | `DELETE /api/auth/sessions/{id}` | Revoke specific session | 🟡 MEDIUM | Đăng xuất từ xa. |
| ⚠️ Revoke All Sessions | `POST /api/auth/sessions/revoke-all` | Logout everywhere | 🟡 MEDIUM | Security feature quan trọng. |

---

## 3. Issues Found

### 3.1 Security Issues 🔴 CRITICAL

| Issue ID | Severity | Description | Location | Recommendation |
|----------|----------|-------------|----------|----------------|
| **SEC-001** | 🔴 Critical | **Hardcoded JWT Secret Key** | [JwtTokenService.cs#L156-L160](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L156-L160) | Secret key đang hardcoded: `"ClassifiedAds-Super-Secret-Key-For-JWT-Token-Generation-2026!@#$%"`. Vi phạm rule SEC-001. **Phải di chuyển vào configuration/secrets.** |
| **SEC-002** | 🔴 Critical | **No Rate Limiting on Auth Endpoints** | [AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs) | `AuthController` không có `[EnableRateLimiting]` attribute. Vi phạm rule SEC-103: "Authentication endpoints MUST have stricter rate limits." Dễ bị brute-force attack. |
| **SEC-003** | 🔴 Critical | **No Rate Limiting on UsersController** | [UsersController.cs](ClassifiedAds.Modules.Identity/Controllers/UsersController.cs) | `UsersController` không có `[EnableRateLimiting]`. Vi phạm rule SEC-100, SEC-101. |
| **SEC-004** | 🟡 Medium | **Refresh Token Scan All Users** | [JwtTokenService.cs#L52-L94](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L52-L94) | `ValidateRefreshTokenAsync` scan qua tất cả users để tìm refresh token. Performance issue và potential DOS vulnerability. |

### 3.2 API Design Issues 🟡 MEDIUM

| Issue ID | Severity | Description | Location | Recommendation |
|----------|----------|-------------|----------|----------------|
| **API-001** | 🟡 Medium | **Inconsistent REST Naming** | `/api/users/{id}/passwordresetemail` | Không theo RESTful convention. Nên đổi thành `POST /api/users/{id}/password-reset-email` hoặc `POST /api/users/{id}/actions/send-password-reset`. |
| **API-002** | 🟡 Medium | **Inconsistent REST Naming** | `/api/users/{id}/emailaddressconfirmation` | Nên đổi thành `POST /api/users/{id}/email-confirmation` hoặc `POST /api/users/{id}/actions/send-email-confirmation`. |
| **API-003** | 🟡 Medium | **No Pagination Support** | `GET /api/users` | Trả về tất cả users không phù hợp cho production với số lượng lớn users. |
| **API-004** | 🟡 Medium | **Missing OpenAPI Documentation** | Multiple endpoints | Một số endpoints thiếu `[ProducesResponseType]` attributes đầy đủ. |

### 3.3 Missing Features 🟢 LOW

| Issue ID | Severity | Description | Recommendation |
|----------|----------|-------------|----------------|
| **FEAT-001** | 🟢 Low | **No Self-Registration API** | Thêm `POST /api/auth/register` cho phép user tự đăng ký. |
| **FEAT-002** | 🟢 Low | **2FA Not Fully Implemented** | Có entity support nhưng thiếu APIs. |
| **FEAT-003** | 🟢 Low | **UserProfile APIs Missing** | Có `UserProfile` entity nhưng không có controller/APIs. |

---

## 4. Compliance Check

### 4.1 Architecture Rules Compliance

| Rule | Status | Notes |
|------|--------|-------|
| ARCH-001: Module self-contained | ✅ Pass | Identity module có riêng DbContext, Entities, Controllers |
| ARCH-020: Thin controllers | ✅ Pass | Controllers delegate to UserManager/Dispatcher |
| ARCH-022: Authorize with permissions | ✅ Pass | Sử dụng `[Authorize(Permissions.X)]` |
| ARCH-024: ProducesResponseType | ⚠️ Partial | Một số endpoints thiếu attributes |
| ARCH-025: Rate limiting | ❌ Fail | Identity controllers không có rate limiting |

### 4.2 Security Rules Compliance

| Rule | Status | Notes |
|------|--------|-------|
| SEC-001: No hardcoded secrets | ❌ **FAIL** | JWT secret key hardcoded |
| SEC-010: All endpoints protected | ✅ Pass | Có `[Authorize]` attribute |
| SEC-014: Token expiration | ✅ Pass | Access: 60 min, Refresh: 7 days |
| SEC-020: Policy-based authorization | ✅ Pass | Sử dụng Permissions constants |
| SEC-030: Input validation | ✅ Pass | DataAnnotations trong models |
| SEC-100: Rate limiting enabled | ❌ **FAIL** | Không có rate limiting |
| SEC-103: Auth endpoints rate limited | ❌ **FAIL** | Login endpoint không có rate limit |

---

## 5. Suggestions & Improvements

### 5.1 Immediate Actions (Must Fix) 🔴

1. **Fix Hardcoded JWT Secret** (SEC-001)
   ```csharp
   // Move to configuration
   private string GetSecretKey()
   {
       return _options.Jwt?.SecretKey 
           ?? throw new InvalidOperationException("JWT Secret Key not configured");
   }
   ```

2. **Add Rate Limiting to Auth Endpoints** (SEC-100, SEC-103)
   ```csharp
   // AuthController.cs
   [EnableRateLimiting("AuthPolicy")]  // Stricter policy: 5 requests/minute
   [HttpPost("login")]
   public async Task<ActionResult<LoginResponseModel>> Login(...)
   ```

3. **Add Rate Limiting to Identity Controllers**
   ```csharp
   [EnableRateLimiting(RateLimiterPolicyNames.DefaultPolicy)]
   [Authorize]
   [Route("api/[controller]")]
   public class UsersController : ControllerBase
   ```

### 5.2 Short-term Improvements 🟡

1. **Add Self-Registration API**
   ```
   POST /api/auth/register
   Body: { email, password, confirmPassword }
   ```

2. **Add User Pagination**
   ```
   GET /api/users?page=1&pageSize=20&search=&role=
   ```

3. **Add Role Assignment API**
   ```
   POST /api/users/{id}/roles
   Body: { roleId: "guid" }
   ```

4. **Add User Profile API**
   ```
   GET /api/auth/me/profile
   PUT /api/auth/me/profile
   Body: { displayName, timezone }
   ```

5. **Fix Refresh Token Validation**
   - Lưu refresh token với user ID để query trực tiếp thay vì scan all users

### 5.3 Long-term Enhancements 🟢

1. **Implement Full 2FA Support**
   - Enable/disable 2FA
   - TOTP setup
   - Recovery codes

2. **Session Management**
   - Track active sessions
   - Remote logout capability

3. **User Activity Audit**
   - Log important user actions
   - Integration with AuditLog module

4. **OAuth2/Social Login**
   - Google, Facebook, Microsoft login
   - Link social accounts

---

## 6. API Coverage Checklist

### Authentication & Account
- [x] Login
- [x] Logout
- [x] Refresh Token
- [ ] ❌ **Register (Self-signup)** - MISSING
- [x] Verify Email
- [x] Resend Verification Email
- [x] Forgot Password
- [x] Reset Password
- [x] Change Password

### User Profile
- [x] Get Current User (/me)
- [ ] ❌ **Update Profile** - MISSING
- [ ] ❌ **Upload Avatar** - MISSING
- [ ] ⚠️ Update Email - NOT IMPLEMENTED
- [ ] ⚠️ Update Phone - NOT IMPLEMENTED

### Admin User Management
- [x] List Users (no pagination)
- [ ] ❌ **List Users with Pagination** - MISSING
- [ ] ❌ **Filter/Search Users** - MISSING
- [x] Get User by ID
- [x] Create User
- [x] Update User
- [x] Delete User
- [ ] ❌ **Assign Role** - MISSING (chỉ khi tạo user)
- [ ] ⚠️ Ban/Deactivate User - PARTIAL
- [ ] ⚠️ Update User Status - PARTIAL

### Role Management
- [x] List Roles
- [x] Get Role by ID
- [x] Create Role
- [x] Update Role
- [x] Delete Role

### Security Features
- [ ] ❌ **Rate Limiting on Auth** - MISSING
- [ ] ⚠️ 2FA Setup/Verify - NOT IMPLEMENTED
- [ ] ⚠️ Session Management - NOT IMPLEMENTED
- [x] Account Lockout (on failed logins)
- [x] Audit Logs (separate module)

---

## 7. Final Score

| Category | Score | Weight | Weighted Score |
|----------|-------|--------|----------------|
| API Coverage | 7/10 | 25% | 1.75 |
| RESTful Naming | 7/10 | 10% | 0.70 |
| Authentication | 8/10 | 20% | 1.60 |
| Authorization (RBAC) | 8/10 | 15% | 1.20 |
| Security | 5/10 | 20% | 1.00 |
| Documentation | 7/10 | 10% | 0.70 |

### **Final Score: 6.95/10 → 7/10**

---

## 8. Priority Action Items

### P0 - Critical (Fix Immediately)
1. 🔴 **Remove hardcoded JWT secret key** - Security vulnerability
2. 🔴 **Add rate limiting to AuthController** - Brute-force protection

### P1 - High (This Sprint)
3. 🔴 Add rate limiting to UsersController
4. 🔴 Add self-registration API (`POST /api/auth/register`)
5. 🔴 Add pagination to user listing
6. 🔴 Add role assignment API

### P2 - Medium (Next Sprint)
7. 🟡 Add user profile update APIs
8. 🟡 Implement 2FA APIs
9. 🟡 Fix refresh token validation performance
10. 🟡 Standardize endpoint naming convention

### P3 - Low (Backlog)
11. 🟢 Session management APIs
12. 🟢 Avatar upload
13. 🟢 User activity logs

---

## Appendix: Reference Documents

- [Architecture Overview](docs-architecture/02-architecture-overview.md)
- [Authentication & Authorization](docs-architecture/08-authentication-authorization.md)
- [Security Rules](rules/security.md)
- [Architecture Rules](rules/architecture.md)

---

*Report generated by AI Software Auditor - February 6, 2026*
