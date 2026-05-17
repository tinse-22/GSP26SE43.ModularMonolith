# FE-15 Async LLM Suggestion Generation Flow

Cap nhat lan cuoi: 2026-05-16

Backend hien tai dung flow:

1. FE goi generate.
2. BE tao generation job va queue background trigger n8n.
3. BE tra `202 Accepted` voi `jobId`.
4. n8n chay va callback ve BE.
5. FE poll job status.
6. Khi job `Completed`, FE moi fetch suggestion list de review.

## 1. Endpoint FE duoc goi

### Start async generation

```http
POST /api/test-suites/{suiteId}/llm-suggestions/generate
Authorization: Bearer <token>
Content-Type: application/json
```

Request:

```json
{
  "specificationId": "0314ea2a-b24d-40c8-b880-a9e520fb5b84",
  "forceRefresh": false,
  "algorithmProfile": {
    "enableBoundary": true,
    "enableNegative": true,
    "enableSecurity": true,
    "enablePerformance": true
  }
}
```

Response `202 Accepted`:

```json
{
  "jobId": "a465978a-2bf6-40bf-b2a8-a0c596545bdd",
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "mode": "callback",
  "message": "Đã tạo job và đưa yêu cầu trigger n8n vào hàng đợi. Suggestions sẽ xuất hiện sau khi callback hoàn tất."
}
```

Sau generate thanh cong, FE chua co suggestion moi de render. Chi poll job status va fetch list sau khi job `Completed`.

### Poll generation job status

```http
GET /api/test-suites/{suiteId}/generation-status?jobId={jobId}
Authorization: Bearer <token>
```

Response `200 OK`:

```json
{
  "jobId": "a465978a-2bf6-40bf-b2a8-a0c596545bdd",
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "status": "WaitingForCallback",
  "queuedAt": "2026-05-16T06:10:00Z",
  "triggeredAt": "2026-05-16T06:10:02Z",
  "completedAt": null,
  "testCasesGenerated": null,
  "errorMessage": null,
  "webhookName": "generate-llm-suggestions"
}
```

Job status FE can handle:

- `Queued`: BE da tao job, background consumer chua nhan.
- `Triggering`: background dang goi webhook n8n.
- `WaitingForCallback`: n8n da nhan job, dang xu ly.
- `Completed`: n8n da callback, suggestions da san sang de review.
- `Failed`: n8n trigger/callback fail.
- `Cancelled`: dung poll.

### Refetch current suggestions

```http
GET /api/test-suites/{suiteId}/llm-suggestions?reviewStatus=Pending
Authorization: Bearer <token>
```

Dung route nay:

- khi polling status thanh `Completed`
- khi user quay lai man hinh review sau khi job da xong

## 2. Endpoint FE khong duoc goi

```http
POST /api/test-generation/llm-suggestions/callback/{jobId}
```

Route nay chi danh cho n8n. FE khong goi route nay. Auth cua route nay dung header `x-callback-api-key`, khong dung JWT user.

## 3. State machine tren UI

| Backend value | UI state | Hanh vi |
|---|---|---|
| `Queued` / `Triggering` | Starting generation | Poll 2-3s/lap |
| `WaitingForCallback` | Generating with n8n | Poll 5s/lap, khong hien list review moi |
| `Completed` | Suggestions ready | Stop poll, refetch `GET /llm-suggestions`, mo review |
| `Failed` | Generation failed | Stop poll, hien retry voi `forceRefresh=true` |
| `Cancelled` | Generation cancelled | Stop poll |

Trong luc job chua terminal, khong render suggestion moi de review.

## 4. TypeScript client mau

```ts
export type GenerationJobStatus =
  | "Queued"
  | "Triggering"
  | "WaitingForCallback"
  | "Completed"
  | "Failed"
  | "Cancelled";

export interface GenerateLlmSuggestionsAcceptedResponse {
  jobId: string;
  testSuiteId: string;
  mode: "callback" | string;
  message: string;
}

export interface GenerationJobStatusDto {
  jobId: string;
  testSuiteId: string;
  status: GenerationJobStatus;
  queuedAt: string;
  triggeredAt?: string | null;
  completedAt?: string | null;
  testCasesGenerated?: number | null;
  errorMessage?: string | null;
  webhookName?: string | null;
}
```

## 5. Nhung cho FE cu can sua

Bo cac assumption cu:

- Khong mong `POST /generate` tra suggestions de review.
- Khong render draft local sau generate.
- Khong goi callback endpoint tu browser.

Them logic moi:

- Luu `jobId` sau generate.
- Poll `/generation-status?jobId=...` den khi terminal.
- Khi `Completed`, refetch `/llm-suggestions`.
- Khi `Failed`, cho user regenerate voi `forceRefresh=true`.
- Hien badge ro rang: `Generating`, `Ready`, `Failed`.

## 6. Error handling

- `400` pending suggestions exist: show choice `Continue current review set` hoac `Regenerate` voi `forceRefresh=true`.
- `409 ORDER_CONFIRMATION_REQUIRED`: dieu huong ve FE-05A approve API order.
- `404 generation-status`: job id sai hoac job khong thuoc suite/user; stop poll va refetch list.
- Poll network fail: retry exponential hoac interval 7-10s.

## 7. n8n dependency FE can biet

FE khong can config n8n. Neu n8n khong callback thanh cong, job se chuyen `Failed` va FE khong co suggestion moi de review.
