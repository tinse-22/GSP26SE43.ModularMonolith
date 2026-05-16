# FE-15 Async LLM Suggestion Refinement Flow

Cap nhat lan cuoi: 2026-05-16

Tai lieu nay thay the cach FE goi `POST /llm-suggestions/generate` theo kieu cu la cho backend/n8n tra full ket qua LLM sau vai phut. Backend hien tai da doi sang flow:

1. FE goi generate.
2. BE tra local draft ngay.
3. BE tao `refinementJobId` va day background job trigger n8n.
4. n8n chay DeepSeek lau bao nhieu cung duoc.
5. n8n callback ve BE.
6. FE poll job status va refetch suggestion list khi job xong.

## 1. Endpoint FE duoc goi

### Generate local draft + start refine

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

Response `201 Created`:

```json
{
  "testSuiteId": "7f081164-ba5d-455e-9bdd-150acbf105fa",
  "totalSuggestions": 3,
  "endpointsCovered": 3,
  "llmModel": "local-draft",
  "llmTokensUsed": null,
  "fromCache": false,
  "source": "local-draft",
  "refinementStatus": "pending",
  "refinementJobId": "a465978a-2bf6-40bf-b2a8-a0c596545bdd",
  "generatedAt": "2026-05-16T06:10:00Z",
  "suggestions": []
}
```

`suggestions` co the co draft rows ngay trong response. Sau generate thanh cong, FE nen render ngay `suggestions` trong response hoac goi lai list route de dong bo list.

### Poll refinement job status

```http
GET /api/test-suites/{suiteId}/generation-status?jobId={refinementJobId}
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
- `WaitingForCallback`: n8n da nhan job, dang chay DeepSeek, FE tiep tuc poll.
- `Completed`: n8n da callback, BE da thay local draft bang refined suggestions. FE phai refetch list.
- `Failed`: n8n trigger/callback fail. FE van giu draft dang co va cho user review/regenerate.
- `Cancelled`: dung poll va giu state hien tai.

### Refetch current suggestions

```http
GET /api/test-suites/{suiteId}/llm-suggestions?reviewStatus=Pending
Authorization: Bearer <token>
```

Dung route nay:

- ngay sau `POST /generate` neu FE muon lay rowVersion/list moi nhat tu DB
- khi polling status thanh `Completed`
- khi user quay lai man hinh review va dang co `refinementJobId` dang pending trong local state

## 2. Endpoint FE khong duoc goi

```http
POST /api/test-generation/llm-suggestions/callback/{jobId}
```

Route nay chi danh cho n8n. FE khong goi route nay. Auth cua route nay dung header `x-callback-api-key`, khong dung JWT user.

## 3. State machine tren UI

FE nen tach 2 state:

- suggestion list state: list rows dang review duoc.
- refinement state: background refine co dang chay hay da xong.

Mapping UI khuyen nghi:

| Backend value | UI state | Hanh vi |
|---|---|---|
| `source=local-draft`, `refinementStatus=pending` | Draft ready, refining | Render draft list ngay, hien badge `Refining...`, bat dau poll |
| job `Queued` / `Triggering` | Starting refinement | Poll cham 2-3s/lap |
| job `WaitingForCallback` | Refining with n8n | Poll 5s/lap, khong block review neu business cho phep |
| job `Completed` | Refined ready | Stop poll, refetch `GET /llm-suggestions`, hien badge `Refined` |
| job `Failed` | Refine failed | Stop poll, giu draft, hien action retry generate voi `forceRefresh=true` |
| job `Cancelled` | Refine cancelled | Stop poll, giu draft |

Khong show full-page loading sau khi `POST /generate` da tra ve. Draft la ket qua dung duoc.

## 4. TypeScript client mau

```ts
export type RefinementStatus = "pending" | "succeeded" | "failed" | "cancelled" | string;

export type GenerationJobStatus =
  | "Queued"
  | "Triggering"
  | "WaitingForCallback"
  | "Completed"
  | "Failed"
  | "Cancelled";

export interface GenerateLlmSuggestionPreviewResult {
  testSuiteId: string;
  totalSuggestions: number;
  endpointsCovered: number;
  llmModel?: string | null;
  llmTokensUsed?: number | null;
  fromCache: boolean;
  source?: string | null;
  refinementStatus?: RefinementStatus | null;
  refinementJobId?: string | null;
  generatedAt: string;
  suggestions: LlmSuggestion[];
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

export async function generateLlmSuggestionPreview(
  suiteId: string,
  body: {
    specificationId: string;
    forceRefresh?: boolean;
    algorithmProfile?: Record<string, unknown>;
  },
): Promise<GenerateLlmSuggestionPreviewResult> {
  return api.post(`/api/test-suites/${suiteId}/llm-suggestions/generate`, body);
}

export async function getGenerationStatus(
  suiteId: string,
  jobId: string,
): Promise<GenerationJobStatusDto> {
  return api.get(`/api/test-suites/${suiteId}/generation-status`, {
    params: { jobId },
  });
}

export async function getLlmSuggestions(
  suiteId: string,
  filters?: { reviewStatus?: string; testType?: string; endpointId?: string },
): Promise<LlmSuggestion[]> {
  return api.get(`/api/test-suites/${suiteId}/llm-suggestions`, {
    params: filters,
  });
}
```

## 5. React hook mau

```ts
const TERMINAL_STATUSES = new Set(["Completed", "Failed", "Cancelled"]);

export function useLlmSuggestionRefinement(suiteId: string) {
  const [refinementJobId, setRefinementJobId] = useState<string | null>(null);
  const [refinementStatus, setRefinementStatus] = useState<string | null>(null);

  const generate = async (specificationId: string, forceRefresh = false) => {
    const result = await generateLlmSuggestionPreview(suiteId, {
      specificationId,
      forceRefresh,
    });

    setSuggestions(result.suggestions);
    setRefinementStatus(result.refinementStatus ?? null);

    if (result.refinementJobId) {
      setRefinementJobId(result.refinementJobId);
    }

    return result;
  };

  useEffect(() => {
    if (!refinementJobId) return;

    let cancelled = false;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const job = await getGenerationStatus(suiteId, refinementJobId);
        if (cancelled) return;

        setRefinementStatus(job.status);

        if (job.status === "Completed") {
          const latest = await getLlmSuggestions(suiteId, { reviewStatus: "Pending" });
          if (!cancelled) {
            setSuggestions(latest);
            setRefinementJobId(null);
          }
          return;
        }

        if (job.status === "Failed" || job.status === "Cancelled") {
          setRefinementJobId(null);
          return;
        }

        timer = window.setTimeout(poll, job.status === "WaitingForCallback" ? 5000 : 2500);
      } catch (error) {
        if (!cancelled) {
          timer = window.setTimeout(poll, 7000);
        }
      }
    };

    poll();

    return () => {
      cancelled = true;
      if (timer) window.clearTimeout(timer);
    };
  }, [suiteId, refinementJobId]);

  return { generate, refinementJobId, refinementStatus };
}
```

## 6. Nhung cho FE cu can sua

Bo cac assumption cu:

- Khong coi `POST /generate` la request chay 6 phut.
- Khong show spinner full page cho den khi DeepSeek xong.
- Khong retry `POST /generate` khi response la `local-draft`.
- Khong coi `llmModel=local-draft` la loi.
- Khong goi callback endpoint tu browser.

Them logic moi:

- Luu `refinementJobId` sau generate.
- Poll `/generation-status?jobId=...` den khi terminal.
- Khi `Completed`, refetch `/llm-suggestions`.
- Khi `Failed`, van cho user review draft hoac regenerate voi `forceRefresh=true`.
- Hien badge ro rang: `Draft`, `Refining`, `Refined`, `Refine failed`.

## 7. Error handling

- `400` pending suggestions exist: show choice `Continue current draft` hoac `Regenerate` voi `forceRefresh=true`.
- `409 ORDER_CONFIRMATION_REQUIRED`: dieu huong ve FE-05A approve API order.
- `404 generation-status`: job id sai hoac job khong thuoc suite/user; stop poll va refetch list.
- Poll network fail: khong xoa draft; retry exponential hoac interval 7-10s.

## 8. n8n dependency FE can biet

FE khong can config n8n. Tuy nhien neu n8n chua doi sang async ACK + callback, BE van tra draft nhanh nhung refinement job co the chuyen `Failed`. Day la expected degraded mode, khong phai loi FE.
