# Payment Feature Walkthrough

Implemented a comprehensive payment feature using PayOS for subscription plans in the `ClassifiedAds.Modules.Subscription` module.

## Changes Overview

### 1. Domain Entities

#### PaymentIntent
- Tracks payment lifecycle (`Pending`, `Succeeded`, `Failed`, `Cancelled`).
- Linked to `User` and `Subscription`.

#### PaymentTransaction
- Updated to include PayOS-specific fields:
  - `Provider`
  - `ProviderTransactionId`
  - `PaymentIntentId`

#### UserSubscription
- Added `Pending` status to `SubscriptionStatus` enum.

### 2. Services and Integration

#### PayOsService
- Handles interaction with PayOS API:
  - Create payment link
  - Get payment link information
  - Verify webhook payload/signature

#### PayOsOptions
- Configuration model for PayOS credentials and callback URLs.

### 3. Application Logic (CQRS)

#### Commands

##### CreateSubscriptionPaymentCommand
- Initiates payment flow.
- Creates `PaymentIntent`.
- Creates pending `UserSubscription`.

##### CreatePayOsCheckoutCommand
- Generates PayOS checkout URL for an existing payment intent.

##### HandlePayOsWebhookCommand
- Processes PayOS webhooks.
- Verifies payload/signature.
- Creates `PaymentTransaction`.
- Activates `UserSubscription` on successful payment.

##### SyncPaymentFromPayOsCommand
- Manually syncs payment status from PayOS for recovery/debug scenarios.

#### Queries

##### GetPaymentIntentQuery
- Retrieves payment intent details by intent ID.

##### GetPaymentIntentByOrderCodeQuery
- Retrieves payment intent by PayOS `orderCode`.

### 4. API Endpoints

#### PaymentsController
- `POST /api/payments/subscribe`: Start subscription payment.
- `GET /api/payments/intents/{id}`: Get payment intent details.
- `POST /api/payments/payos/webhook`: PayOS webhook endpoint.
- `GET /api/payments/payos/return`: Return URL handler.

### 5. Infrastructure

#### Migrations
- Created EF Core migration `PaymentFeature` in `ClassifiedAds.Migrator`.

#### Dependency Injection
- Registered repositories and services in `ServiceCollectionExtensions.cs`.

## Verification

### Automated Checks
- Ran `dotnet build` to ensure compilation success.
- Created EF Core migration successfully.

### Manual Verification Steps

1. Configure PayOS in `appsettings.json` under `Modules:Subscription:PayOs`:
   - `ClientId`
   - `ApiKey`
   - `ChecksumKey`
   - `ReturnUrl`
   - `CancelUrl`

2. Apply migration:
   - `dotnet ef database update --project ClassifiedAds.Migrator`
   - or run migrator:
   - `dotnet run --project ClassifiedAds.Migrator`

3. Test flow:
   - Call `POST /api/payments/subscribe` with `PlanId`.
   - Use returned `checkoutUrl` to pay via PayOS sandbox.
   - Verify `PaymentIntent` status becomes `Pending`.
   - After payment, verify:
     - `PaymentTransaction` is created.
     - `UserSubscription` becomes `Active`.
   - Check webhook processing logs.

## API Usage Examples

### Initiate Payment

```http
POST /api/payments/subscribe
Content-Type: application/json

{
  "planId": "GUID",
  "frequency": "Monthly",
  "returnUrl": "https://myapp.com/success",
  "cancelUrl": "https://myapp.com/cancel"
}
```

### Webhook Payload (Simulated)

```json
{
  "code": "00",
  "desc": "Success",
  "data": {
    "orderCode": 123456,
    "amount": 100000,
    "reference": "PayOS-Ref-ID",
    "transactionDateTime": "2023-10-27T10:00:00+07:00",
    "currency": "VND",
    "paymentLinkId": "link-id",
    "code": "00",
    "desc": "Success",
    "counterAccountBankId": null,
    "counterAccountBankName": null,
    "counterAccountName": null,
    "counterAccountNumber": null,
    "virtualAccountName": null,
    "virtualAccountNumber": null
  },
  "signature": "..."
}
```
