# FE-14 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-28

Thu muc nay duoc viet rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.Subscription`
- `ClassifiedAds.WebAPI`
- `ClassifiedAds.Background` (chi lien quan worker reconcile PayOS)

Muc tieu:

- chot dung route, request, response, status code, va runtime behavior cua FE-14 theo code dang chay
- cover du surface frontend-facing hien tai: quan ly plan, vong doi subscription, usage tracking, payment intent, PayOS checkout/webhook/return
- tranh de FE phai noi theo planning docs cu trong `docs/features/FE-14-subscription-billing`

## 1. Pham vi FE-14 runtime

Runtime frontend-facing hien tai trai tren 3 controller:

- `PlansController`
  - `GET /api/plans`
  - `GET /api/plans/{id}`
  - `POST /api/plans`
  - `PUT /api/plans/{id}`
  - `DELETE /api/plans/{id}`
  - `GET /api/plans/{id}/auditlogs`
- `SubscriptionsController`
  - `GET /api/subscriptions/{id}`
  - `GET /api/subscriptions/plans`
  - `GET /api/subscriptions/users/{userId}/current`
  - `GET /api/subscriptions/me/current`
  - `POST /api/subscriptions`
  - `PUT /api/subscriptions/{id}`
  - `POST /api/subscriptions/{id}/cancel`
  - `GET /api/subscriptions/{id}/history`
  - `GET /api/subscriptions/{id}/payments`
  - `POST /api/subscriptions/{id}/payments`
  - `GET /api/subscriptions/users/{userId}/usage`
  - `PUT /api/subscriptions/users/{userId}/usage`
- `PaymentsController`
  - `POST /api/payments/subscribe/{planId}`
  - `GET /api/payments/plans`
  - `GET /api/payments/{intentId}`
  - `POST /api/payments/payos/create`
  - `POST /api/payments/payos/webhook`
  - `GET /api/payments/payos/return`
  - `POST /api/payments/debug/check-payment/{intentId}`
  - `POST /api/payments/debug/sync-payment/{intentId}`

## 2. Files trong thu muc nay

- `plans-api.json`: contract frontend-facing cho `PlansController`
- `subscriptions-api.json`: contract cho `SubscriptionsController`, gom subscription lifecycle, payment transaction helper, va usage tracking
- `payments-api.json`: contract cho `PaymentsController`, gom plan purchase, payment intent, PayOS checkout/webhook/return, va debug recovery APIs

## 3. Auth va permission

- Tat ca route deu yeu cau Bearer token, tru:
  - `POST /api/payments/payos/webhook`
  - `GET /api/payments/payos/return`
- Moi action duoc bao ve boi permission policy rieng. Mapping cu the nam trong tung file JSON.
- Runtime ownership rat quan trong:
  - Non-admin doc/cap nhat subscription cua user khac se nhan `404`, khong phai `403`.
  - `GET /api/payments/{intentId}` va 2 route debug payment khong co admin bypass; chi doc payment intent cua chinh token user hien tai.
  - Voi `GET/PUT /api/subscriptions/users/{userId}/current|usage`, non-admin bi ignore `userId` tren path va backend tu dong thay bang current user.

## 4. Nhung diem FE de noi sai

1. FE-14 runtime hien tai khong co `JsonStringEnumConverter` global. Da so enum request/response cua Subscription ra dang so, khong phai chuoi.
2. Ngoai le quan trong duy nhat trong FE-14 la `PlanLimitModel.limitType`, field nay dung string enum va reject integer value.
3. `GET /api/subscriptions/plans` va `GET /api/payments/plans` la 2 route duplicate de phuc vu UX khac nhau. Ca 2 deu default `isActive=true`.
4. `GET /api/plans` la route admin day du; no khong default loc `isActive=true`.
5. `POST /api/subscriptions` khong phai create thuan. Handler se lay subscription moi nhat cua current user neu da ton tai, cap nhat no, nhung van tra `201 Created`.
6. `CreateUpdateSubscriptionModel.userId`, `externalSubId`, `externalCustId`, va `changeReason` deu bi `JsonIgnore`. FE gui len se khong co tac dung.
7. `PUT /api/subscriptions/{id}` khong cho doi owner. Controller tu lay `UserId` tu subscription dang ton tai.
8. `GET /api/subscriptions/users/{userId}/current` va `GET/PUT /api/subscriptions/users/{userId}/usage` se ignore path `userId` neu token khong phai admin.
9. `GET /api/subscriptions/me/current` va `GET /api/subscriptions/users/{userId}/current` chi xem status `Trial | Active | PastDue` la current subscription.
10. `DELETE /api/plans/{id}` va `PUT /api/plans/{id}` khi doi `isActive=true -> false` deu block neu plan van con subscriber `Trial | Active | PastDue`. Runtime hien tai tra `400`, khong tra `409`.
11. `GET /api/plans/{id}/auditlogs` khong validate plan ton tai. Neu id khong co audit entry, route van tra `200 []`.
12. `PlanModel.limits` khong duoc backend sort. FE khong nen tin vao thu tu limit trong response.
13. `POST /api/payments/subscribe/{planId}` la entry point chinh cho mua goi. Neu plan mien phi thi activate subscription ngay va khong tao `PaymentIntent`.
14. Flow goi tra phi hien tai la 2 buoc: `POST /api/payments/subscribe/{planId}` -> `POST /api/payments/payos/create`. Route `subscribe` khong tra checkout URL.
15. `POST /api/payments/payos/create` co the duoc goi lai cho cung `intentId`; backend reuse `orderCode` neu da co.
16. Neu FE override `returnUrl` tron sang frontend URL rieng, redirect se di thang ve URL do va backend `GET /api/payments/payos/return` se khong tham gia normalize `status/intenId` nua.
17. `POST /api/payments/payos/webhook` luon tra `200` JSON (`ok`, `ignored`, hoac `error`). Signature sai va payload khong hop le khong doi sang HTTP error.
18. Runtime webhook hien tai verify `payload.signature` trong JSON body; `x-signature` header duoc doc nhung chua anh huong ket qua xu ly.
19. `GET /api/payments/payos/return` luon redirect, khong co JSON body. Redirect dich la `/payment/result?status=success|failed[&intentId=...]`.
20. `GET /api/payments/{intentId}` va 2 route debug payment khong cho admin doc intent cua user khac. Muon xem ket qua cho current checkout thi token va intent phai cung owner.
21. `POST /api/subscriptions/{id}/payments` chi tao pending `PaymentTransaction` local. Day khong phai primary checkout flow cua PayOS.
22. `POST /api/subscriptions/{id}/payments` co logic idempotent theo `externalTxnId` hoac `providerRef` va co the tra lai transaction cu.
23. `POST /api/subscriptions/{id}/payments` tra `Location: /api/subscriptions/{id}/payments/{transactionId}`, nhung runtime hien tai khong co GET route cho duong dan do.
24. `PUT /api/subscriptions/users/{userId}/usage` tra ve `UsageTrackingModel[]`, khong phai 1 object. Khi `replaceValues=false`, backend cong don cac metric vao ban ghi hien co.
25. `ValidationException` trong module nay duoc global handler map thanh `400 application/problem+json`, ngay ca voi business rule conflict.
26. Background worker `ReconcilePayOsCheckoutWorker` co the doi `PaymentIntent.status` sau khi FE roi khoi checkout page. FE nen refresh `GET /api/payments/{intentId}` va, neu can, `GET /api/subscriptions/me/current`.

## 5. Flow FE nen bam

1. Load plan:
   - Self-service: `GET /api/payments/plans` hoac `GET /api/subscriptions/plans`
   - Admin CRUD: `GET /api/plans`
2. User mua goi:
   - Goi `POST /api/payments/subscribe/{planId}`
   - Neu `requiresPayment = false`, xem nhu da activate xong va refresh current subscription
   - Neu `requiresPayment = true`, lay `paymentIntentId`
3. Tao checkout PayOS:
   - Goi `POST /api/payments/payos/create`
   - Thuong nen de backend dung `PayOS.ReturnUrl` mac dinh; chi override `returnUrl` khi chu dong muon bo qua route backend `/api/payments/payos/return`
   - Redirect user den `checkoutUrl`
4. Sau khi user quay lai frontend:
   - Nhan redirect tu `GET /api/payments/payos/return`
   - Goi `GET /api/payments/{intentId}` de lay trang thai intent moi nhat
   - Goi them `GET /api/subscriptions/me/current` neu can hien goi da active
5. Neu user vao man hinh subscription detail:
   - `GET /api/subscriptions/me/current`
   - `GET /api/subscriptions/{id}/history`
   - `GET /api/subscriptions/{id}/payments`
6. Man hinh admin/ops:
   - plan CRUD + `GET /api/plans/{id}/auditlogs`
   - usage tracking qua `GET/PUT /api/subscriptions/users/{userId}/usage`

## 6. Khuyen nghi su dung

- Dung enum mapping so trong FE model cho: `BillingCycle`, `SubscriptionStatus`, `ChangeType`, `PaymentStatus`, `PaymentPurpose`, `PaymentIntentStatus`.
- Dung enum chuoi cho `PlanLimit.limitType`.
- Khong hardcode plan id seed; chi hardcode enum names/values.
- Khong dua vao `Location` header cua `POST /api/subscriptions/{id}/payments`.
- Sau checkout, coi `GET /api/payments/{intentId}` la API read chinh cho trang thai current payment.
- Neu FE can admin impersonation/payment support cho user khac, can luu y runtime hien tai chua ho tro doc `PaymentIntent` cua user khac qua `PaymentsController`.

## 7. Lien ket tham khao

- `docs/guides/SUBSCRIPTION_WORKFLOW_GUIDE.md`
- `docs/features/FE-14-subscription-billing`
- `docs/tracking/FE_COMPLETION_TRACKER.md`
