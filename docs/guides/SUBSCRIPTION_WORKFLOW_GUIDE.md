# Subscription Workflow Guide

Tai lieu nay mo ta workflow thuc te dang co trong `ClassifiedAds.Modules.Subscription` de ban biet can lam gi tiep theo cho du an.

## 1) Module nay dang giai quyet bai toan gi?

`Subscription` quan ly 5 nhom nghiep vu:

1. Quan ly goi cuoc (`SubscriptionPlan`, `PlanLimit`).
2. Vong doi dang ky cua user (`UserSubscription`, `SubscriptionHistory`).
3. Thanh toan qua PayOS (`PaymentIntent`, `PaymentTransaction`).
4. Kiem soat quota su dung (`UsageTracking`, `TryConsumeLimitAsync`).
5. Audit + outbox event de dong bo lien module.

## 2) Kien truc luong tong quan

```
Client/API
  -> Controllers (Plans, Subscriptions, Payments)
  -> Dispatcher (CQRS)
  -> Commands/Queries
  -> Repositories + SubscriptionDbContext (schema: subscription)
  -> (neu can) PayOsService -> PayOS API
  -> OutboxMessages -> PublishEventWorker -> MessageBus publishers
```

Code chinh:

- `ClassifiedAds.Modules.Subscription/Controllers`
- `ClassifiedAds.Modules.Subscription/Commands`
- `ClassifiedAds.Modules.Subscription/Services`
- `ClassifiedAds.Modules.Subscription/HostedServices`

## 3) Workflow chi tiet theo nghiep vu

### A. Plan Management (Admin)

API:

- `GET /api/plans`
- `GET /api/plans/{id}`
- `POST /api/plans`
- `PUT /api/plans/{id}`
- `DELETE /api/plans/{id}`
- `GET /api/plans/{id}/auditlogs`

Luot chinh:

1. Admin tao/cap nhat plan qua `AddUpdatePlanCommand`.
2. Validate:
   - Ten plan unique (khong phan biet hoa thuong).
   - `LimitType` khong trung trong cung plan.
   - Neu `IsUnlimited = false` thi `LimitValue > 0`.
   - Khong cho disable plan neu van con subscriber dang `Trial/Active/PastDue`.
3. Thay toan bo `PlanLimit` khi update.
4. Qua `CrudService`, domain event create/update/delete duoc phat ra.
5. Event handler ghi `AuditLogEntry` + `OutboxMessage`.

### B. Subscription Lifecycle (manual path)

API:

- `POST /api/subscriptions`
- `PUT /api/subscriptions/{id}`
- `POST /api/subscriptions/{id}/cancel`
- `GET /api/subscriptions/{id}`
- `GET /api/subscriptions/me/current`
- `GET /api/subscriptions/users/{userId}/current`
- `GET /api/subscriptions/{id}/history`

Luot chinh:

1. Controller lay `UserId` tu claim.
2. `AddUpdateSubscriptionCommand` validate plan, billing cycle, trial info.
3. He thong cap nhat lifecycle fields (`Status`, `StartDate`, `EndDate`, `NextBillingDate`, `TrialEndsAt`).
4. Luu snapshot gia/ten goi tai thoi diem kich hoat (`Snapshot*` fields).
5. Tao ban ghi `SubscriptionHistory` voi `ChangeType` (Created/Upgraded/Downgraded/Reactivated/Cancelled).
6. `CancelSubscriptionCommand` set `CancelledAt`, tat auto-renew, va ghi history.

### C. Payment Flow qua PayOS (purchase/upgrade)

API:

- `POST /api/payments/subscribe/{planId}`
- `POST /api/payments/payos/create`
- `POST /api/payments/payos/webhook` (anonymous)
- `GET /api/payments/payos/return` (anonymous)
- `GET /api/payments/{intentId}`
- `POST /api/payments/debug/sync-payment/{intentId}`
- `POST /api/payments/debug/check-payment/{intentId}`

Luot chinh:

1. User goi `subscribe/{planId}`.
2. `CreateSubscriptionPaymentCommand`:
   - Neu plan gia = 0: active subscription ngay (khong can payment intent).
   - Neu plan co phi: tao `PaymentIntent` (`RequiresPayment`, co `ExpiresAt`).
3. User goi `payos/create` de tao checkout URL.
4. `CreatePayOsCheckoutCommand`:
   - Validate owner + trang thai intent + han su dung.
   - Tao `OrderCode` unique.
   - Goi PayOS API tao payment link.
   - Update `CheckoutUrl`, status sang `Processing`.
5. PayOS goi webhook:
   - `HandlePayOsWebhookCommand` verify signature.
   - Tim intent theo `OrderCode`.
   - Idempotent theo `(Provider, ProviderRef)`.
   - Neu thanh cong: upsert subscription active + tao history + tao payment transaction success + set intent success.
   - Neu that bai: set intent `Canceled/Expired` (va tao failed transaction neu da co `SubscriptionId`).
6. Return URL redirect ve frontend `/payment/result?status=success|failed`.
7. Neu miss webhook, API debug `sync-payment` se goi PayOS query status va re-use flow webhook.

### D. Reconcile Background Worker

Code:

- `HostedServices/ReconcilePayOsCheckoutWorker.cs`
- `Commands/ReconcilePayOsCheckoutsCommand.cs`

Luot chinh:

1. Worker chay dinh ky (mac dinh 30s).
2. Quet payment intent dang pending/processing trong khoang lookback.
3. Tu dong:
   - Danh dau expired neu qua han.
   - Dong bo paid qua `SyncPaymentFromPayOsCommand`.
   - Cap nhat cancelled/expired theo trang thai tu PayOS.
   - Bo sung `CheckoutUrl` neu truoc do bi thieu.

### E. Usage Limit / Quota Enforcement

Contract dung chung cho module khac:

- `ClassifiedAds.Contracts/Subscription/Services/ISubscriptionLimitGatewayService.cs`

API ho tro:

- `GET /api/subscriptions/users/{userId}/usage`
- `PUT /api/subscriptions/users/{userId}/usage`

Luot chinh:

1. Module khac nen goi `TryConsumeLimitAsync(userId, limitType, increment)` truoc khi tao tai nguyen.
2. `ConsumeLimitAtomicallyCommand` dung transaction `Serializable` + retry conflict de tranh race condition.
3. Neu vuot limit -> tra `IsAllowed = false` + `DenialReason`.
4. Neu hop le -> vua check vua consume usage trong 1 transaction.

Tich hop hien co:

- `ClassifiedAds.Modules.ApiDocumentation` dang goi limit cho:
  - `MaxProjects`
  - `MaxEndpointsPerProject`
  - `MaxStorageMB`

### F. Outbox + Audit

Code:

- `HostedServices/PublishEventWorker.cs`
- `Commands/PublishEventsCommand.cs`
- `OutBoxEventPublishers/*`

Luot chinh:

1. Moi ban ghi outbox chua publish se duoc worker doc theo lo.
2. Gui qua message bus.
3. Danh dau `Published = true` neu thanh cong.
4. Plan events va audit log events da co flow ro rang.

## 4) Ban can lam gi tiep theo cho du an (roadmap de lam ngay)

### Step 1 - Chot environment va run on dinh

1. Dam bao tat ca host (`WebAPI`, `Background`, `Migrator`) dung chung `ConnectionStrings:Default` (dang map tu `.env`).
2. Cung cap bien moi truong PayOS:
   - `PayOS__ClientId`
   - `PayOS__ApiKey`
   - `PayOS__SecretKey`
   - `PayOS__ReturnUrl`
   - `PayOS__CancelUrl` (optional)
   - `PayOS__FrontendBaseUrl` (de redirect ket qua)
3. Chay migrator truoc khi test flow payment/subscription.

### Step 2 - Chot flow FE/BE mua goi

Frontend nen di theo dung thu tu:

1. Goi `POST /api/payments/subscribe/{planId}`.
2. Neu `requiresPayment = false` -> xong (free plan).
3. Neu `requiresPayment = true` -> lay `paymentIntentId`.
4. Goi `POST /api/payments/payos/create` -> redirect user den `checkoutUrl`.
5. Sau redirect ve `payos/return`, FE doc query `status` + `intentId`.
6. Goi `GET /api/payments/{intentId}` de hien thi trang thai cuoi.

### Step 3 - Mo rong quota cho cac module con lai

1. Tai moi module tao resource, chen `TryConsumeLimitAsync(...)` truoc khi save.
2. Mapping dung `LimitType` phu hop (project, endpoint, test run, llm call, storage...).
3. Neu bi reject, show `DenialReason` cho user + goi y nang cap plan.

### Step 4 - Hardening truoc production

1. Bo sung test E2E cho webhook + return URL + reconcile worker.
2. Theo doi log cac endpoint debug, webhook error, va retry worker.
3. Ra soat outbox event cho payment/subscription de dam bao su kien can phat deu duoc tao.
4. Kiem tra permission matrix cho tat ca API Subscription.

## 5) Luu y quan trong theo code hien tai

1. Module da day du luong chinh (plan, subscription, payment, usage, reconcile).
2. `PayOS` config duoc bind tu section `PayOS` (khong nam san trong appsettings mac dinh).
3. `SubscriptionDbContext` dung schema mac dinh la `subscription`.
4. Co san seed plan/limit va migration seed admin Enterprise trong `ClassifiedAds.Migrator/Migrations/Subscription`.

---

Neu ban muon, minh co the viet tiep 1 file checklist release (`SUBSCRIPTION_RELEASE_CHECKLIST.md`) de team test theo tung test case API va expected result.
