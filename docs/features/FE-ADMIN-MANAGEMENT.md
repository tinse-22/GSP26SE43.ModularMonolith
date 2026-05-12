# FE Prompt - Admin Management (Admin only)

## Objective
Build FE for admin management only. Exclude user self-service and guest flows. Provide flows, params, and concrete actions so FE can wire all APIs with full payloads.

## Preconditions and access
- All screens require admin session and permission checks.
- All API calls include header: Authorization: Bearer {access_token}.
- 401 -> redirect to login. 403 -> show no-access state.

## Route map (admin only)
- /admin/dashboard
- /admin/users
- /admin/users/:id
- /admin/roles
- /admin/plans
- /admin/system (settings, audit logs, notification templates)

## Global list behavior (apply to all list tables)
- page is 1-based.
- pageSize default 20 (users list max 100).
- size/sort/desc follow swagger for admin dashboard endpoints.
- Keep filters in URL query for shareable links.
- Show empty state when items[] is empty and totalCount == 0.

## 1) User management (UsersController)

### Flow
1. List -> GET /api/users with filters.
2. Open detail -> GET /api/users/{id}.
3. Admin actions from detail: update profile fields, set password, assign/remove roles, lock/unlock, send emails, delete.

### Endpoints and params

#### GET /api/users
Query params:
- page (int, optional). If page and pageSize are omitted together, backend returns all users (legacy).
- pageSize (int, default 20, max 100).
- search (string) searches email, username, phone number.
- role (string) role name filter.
- emailConfirmed (bool).
- isLocked (bool).

#### GET /api/users/{id}
Path params:
- id (uuid)

#### POST /api/users
Body:
- userName (string, optional, max 256)
- email (string, required, email, max 256)
- password (string, required, 6..100)
- phoneNumber (string, optional, max 32)
- roles (string[], optional, defaults to ["User"])
Response note: response shape is { user, roles, emailConfirmationRequired, message } (swagger is stale).

#### PUT /api/users/{id}
Full object update. Missing fields can be nulled by server. Always fetch detail first and send full payload.
Body (send all current fields + changes):
- userName (required)
- email (required)
- emailConfirmed (bool)
- phoneNumber (string)
- phoneNumberConfirmed (bool)
- twoFactorEnabled (bool)
- lockoutEnabled (bool)
- lockoutEnd (date-time or null)
- accessFailedCount (int)

#### PUT /api/users/{id}/password
Body:
- password (required)
- confirmPassword (optional, FE must validate match)
Note: backend generates reset token internally and sends email to user.

#### DELETE /api/users/{id}
Path params:
- id (uuid)

#### POST /api/users/{id}/password-reset-email
No body.

#### POST /api/users/{id}/email-confirmation
No body.

#### GET /api/users/{id}/roles
Path params:
- id (uuid)
Response: RoleDto[]

#### POST /api/users/{id}/roles
Body:
- roleId (uuid, required)

#### DELETE /api/users/{id}/roles/{roleId}
Path params:
- id (uuid)
- roleId (uuid)

#### POST /api/users/{id}/lock
Body:
- days (int, optional, min 1, ignored when permanent is true)
- permanent (bool, required)
- reason (string, optional, max 500)
Response includes message + lockoutEnd.

#### POST /api/users/{id}/unlock
No body. Unlocks and resets accessFailedCount; backend sends email.

### FE behavior notes
- After create/update/delete/lock/unlock/role changes, refetch list and detail.
- Show lockoutEnd, emailConfirmed, and roles in detail view.
- Disable destructive actions behind confirmation dialogs.

## 2) Role management (RolesController)

### Flow
1. List roles -> GET /api/roles.
2. Create role -> POST /api/roles.
3. Edit role -> PUT /api/roles/{id}.
4. Delete role -> DELETE /api/roles/{id}.

### Endpoints and params

#### GET /api/roles
No params.

#### GET /api/roles/{id}
Path params:
- id (uuid)

#### POST /api/roles
Body:
- name (string, required, min 1, max 256)
Server computes normalizedName.

#### PUT /api/roles/{id}
Path params:
- id (uuid)
Body:
- name (string, required, min 1, max 256)

#### DELETE /api/roles/{id}
Path params:
- id (uuid)

## 3) Subscription plan management (PlansController)

### Flow
1. List plans -> GET /api/plans with optional filters.
2. View plan -> GET /api/plans/{id}.
3. Create plan -> POST /api/plans (with limits).
4. Update plan -> PUT /api/plans/{id} (replace limits).
5. Deactivate plan -> DELETE /api/plans/{id} (soft delete).
6. View audit log -> GET /api/plans/{id}/auditlogs.

### Endpoints and params

#### GET /api/plans
Query params:
- isActive (bool?)
- search (string?) by Name or DisplayName

#### POST /api/plans and PUT /api/plans/{id}
Body (CreateUpdatePlanModel):
- name (required, unique, max 50)
- displayName (required, max 100)
- description (optional, max 500)
- priceMonthly (decimal?, >= 0)
- priceYearly (decimal?, >= 0)
- currency (string, 3-letter ISO, default "USD")
- isActive (bool, default true)
- sortOrder (int, >= 0)
- limits (array)

Limit item (PlanLimitModel):
- id (guid?, null for new)
- limitType (string enum name)
- limitValue (int?, required when isUnlimited == false)
- isUnlimited (bool)

Allowed limitType values:
- MaxProjects
- MaxEndpointsPerProject
- MaxTestCasesPerSuite
- MaxTestRunsPerMonth
- MaxConcurrentRuns
- RetentionDays
- MaxLlmCallsPerMonth
- MaxStorageMB

Business rules:
- Each limitType appears at most once per plan.
- If isUnlimited == true, limitValue must be null.

#### DELETE /api/plans/{id}
Soft delete: sets isActive = false. If plan has active subscribers, backend returns 409.

## 4) Admin dashboard analytics (AdminDashboard)

### Flow
- On dashboard load: call summary + default tables.
- For each table: apply filters + pagination and keep query state in URL.

### Endpoints and params

#### GET /api/admin/dashboard/summary
No params.

#### GET /api/admin/dashboard/users
Query params:
- Keyword, Role, MembershipPlan, HasActiveMembership, IncludeDeleted
- CreatedFrom, CreatedTo (date-time)
- MinBalanceCents, MaxBalanceCents
- page, size, sort, desc

#### GET /api/admin/dashboard/users/{userId}
Path params:
- userId (uuid)

#### GET /api/admin/dashboard/revenue
Query params:
- period (string, default "month")
- startDate, endDate (date-time)

#### GET /api/admin/dashboard/transactions
Query params:
- UserId, EventId (uuid)
- Status, Direction, Method (string)
- FromDate, ToDate (date-time)
- Period (string)
- MinAmountCents, MaxAmountCents (int64)
- Provider (string)
- page, size, sort, desc

#### GET /api/admin/dashboard/payments
Query params:
- UserId, EventId, MembershipPlanId (uuid)
- Purpose, Status (string)
- FromDate, ToDate (date-time)
- Period (string)
- MinAmountCents, MaxAmountCents (int64)
- OrderCode (int64)
- page, size, sort, desc

#### GET /api/admin/dashboard/memberships
Query params:
- page, size, sort, desc

#### GET /api/admin/dashboard/communities
Query params:
- page, size, sort, desc, includeDeleted

#### GET /api/admin/dashboard/clubs
Query params:
- page, size, sort, desc, includeDeleted

#### GET /api/admin/dashboard/games
Query params:
- page, size, sort, desc, includeDeleted

#### GET /api/admin/dashboard/roles
Query params:
- page, size, sort, desc

## 5) System admin (placeholders)

Admin-only features from permission report: system settings, global audit logs, notification templates.
API details are not specified in current docs; keep FE shells and wire once endpoints are confirmed.

## Output expectations
- Provide route map, page list, component map, data hooks, and state handling notes.

## Sources
- docs/USECASE_SYSTEM.md
- docs/api-reference/USER_API_REFERENCE.md
- docs/api-reference/identity-fe-requirements.json
- docs/features/FE-14-subscription-billing/FE-14-01/admin-plan-management.md
- docs/architecture/USER-ADMIN-PERMISSION-REPORT.md
- docs/api-reference/webapi-v1.json
