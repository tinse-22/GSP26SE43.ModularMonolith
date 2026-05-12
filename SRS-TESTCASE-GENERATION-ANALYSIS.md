# Phan tich luong generate testcase co lay SRS lam chuan hay khong

Ngay phan tich: 2026-05-11

Pham vi doc code: `ClassifiedAds.Modules.TestGeneration`, `ClassifiedAds.WebAPI/appsettings*.json`, cac controller/command/service lien quan den SRS, n8n, LLM va persist testcase.

## Ket luan ngan gon

He thong **co dua SRS vao luong generate testcase**, nhung **chua the noi SRS la nguon chuan tuyet doi trong moi truong hop**.

Ket luan theo tung luong:

| Luong | Co goi LLM/n8n that khong? | Co dua SRS vao khong? | SRS co lam chuan bat buoc khong? | Rui ro |
|---|---:|---:|---:|---|
| Unified callback flow, dang bat theo config | Co, qua n8n webhook va callback | Co, dua `srsRequirements` vao payload | Chu yeu dua vao prompt, BE khong validate lai ky | Config hien thieu webhook key `generate-test-cases-unified` trong appsettings |
| Legacy happy-path flow | Co, qua n8n `generate-happy-path` | Gan nhu khong | Khong | Co post-process status 2xx, khong dua SRS lam oracle |
| Legacy boundary/negative flow - LLM suggestions | Co, qua n8n `generate-llm-suggestions` | Co, dua full SRS context va requirements | Mot phan. Prompt noi SRS la primary, nhung `ExpectationResolver` uu tien LLM neu LLM tra status hop le | Nhieu buoc repair/fallback co the thay doi output LLM |
| Legacy boundary/negative flow - path/body mutations | Khong nhat thiet | Co the dung SRS qua `ExpectationResolver` | Neu SRS constraint match duoc | Day la rule-based, khong phai testcase do LLM viet truc tiep |
| Fallback khi n8n loi | Khong | Co the dung Swagger/SRS/heuristic | Khong dam bao | Van sinh testcase bang heuristic/local fallback |

Noi ngan gon: **gen testcase khong dua hoan toan vao LLM**, va cung **khong dua hoan toan vao SRS**. No la pipeline tron: API spec + business rules + SRS + LLM/n8n + nhieu buoc repair/fallback trong BE.

## Luong dang chay theo config hien tai

Trong `ClassifiedAds.WebAPI/appsettings.json` va `ClassifiedAds.WebAPI/appsettings.Development.json`, option:

```json
"UseDotnetIntegrationWorkflowForGeneration": true
```

Dang duoc bat. Khi option nay bat:

- API `POST /api/test-suites/{suiteId}/test-cases/generate-happy-path`
- API `POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative`

deu khong chay generator cu truc tiep, ma queue `GenerateTestCasesCommand`, sau do background consumer build payload va trigger n8n unified workflow.

Bang chung:

- `TestCasesController.GenerateHappyPath` check `UseDotnetIntegrationWorkflowForGeneration` roi dispatch `GenerateTestCasesCommand`.
- `TestCasesController.GenerateBoundaryNegative` cung check option nay roi dispatch `GenerateTestCasesCommand`.
- `TriggerTestGenerationConsumer` build payload bang `ITestGenerationPayloadBuilder.BuildPayloadAsync(...)`, trigger n8n bang `IN8nIntegrationService.TriggerWebhookWithResultAsync(...)`, roi doi callback.
- Callback nhan ket qua tai `TestOrderController.ReceiveAiGeneratedTestCases`, sau do persist bang `SaveAiGeneratedTestCasesCommand`.

### Van de config rat quan trong

Code unified flow dung webhook name:

```csharp
N8nWebhookNames.GenerateTestCasesUnified = "generate-test-cases-unified"
```

Nhung trong 2 file config da kiem tra, `Webhooks` chi co:

- `generate-llm-suggestions`
- `explain-failure`
- `analyze-srs`
- `refine-srs-requirements`

Khong thay key `generate-test-cases-unified`.

Neu khong co env var override tu ngoai, `N8nIntegrationService.ResolveWebhookUrl(...)` se khong tim thay webhook va unified generation se fail truoc khi goi n8n. Neu may local/docker co override bang environment variable thi luong van co the chay.

### Doi chieu voi file n8n export

Da doi chieu them file `LLM API Test Generator.json` trong root repo. Workflow export nay co cac webhook node:

| n8n node | Method | Path |
|---|---:|---|
| `Webhook1` | POST | `explain-failure` |
| `Webhook` | POST | `analyze-srs` |
| `Webhook3` | POST | `refine-srs-requirements` |
| `Webhook2` | POST | `generate-llm-suggestions` |

Workflow export nay **khong co** webhook path `generate-test-cases-unified`, va cung **khong co** webhook path `generate-happy-path`.

Vi vay link dang UI/execution cua n8n, vi du:

```text
https://tinem226.app.n8n.cloud/workflow/lk0U33vamPTSQ5vz/executions/43?projectId=...
```

khong phai URL webhook de BE goi. URL webhook runtime ma BE goi se co dang:

```text
https://tinem226.app.n8n.cloud/webhook/<path>
```

Voi workflow export hien tai, path co that cho luong gen boundary/negative LLM suggestions la:

```text
https://tinem226.app.n8n.cloud/webhook/generate-llm-suggestions
```

Con unified flow trong BE dang can:

```text
https://tinem226.app.n8n.cloud/webhook/generate-test-cases-unified
```

nhung path nay khong thay trong workflow export.

## SRS duoc dua vao unified flow nhu the nao?

Trong `TestGenerationPayloadBuilder.BuildPayloadAsync(...)`, neu `suite.SrsDocumentId.HasValue`, code load `SrsRequirement` cua document va gan vao:

```csharp
payload.SrsRequirements = requirements.Select(...)
```

Moi requirement gui sang n8n gom:

- `id`
- `code`
- `title`
- `description`
- `requirementType`
- `effectiveConstraints` = `RefinedConstraints` neu co, nguoc lai `TestableConstraints`
- `assumptions`
- `ambiguities`
- `confidenceScore`

Prompt config cua unified flow cung co rule:

- neu co `srsRequirements`, LLM phai populate `coveredRequirementIds`
- expectation phai duoc derive tu `effectiveConstraints`
- khong duoc fabricate expectation trai voi `effectiveConstraints`
- low confidence hoac ambiguity thi phai co rationale va traceability score

=> Ve mat payload/prompt, unified flow **co dua SRS vao va yeu cau LLM dung SRS lam co so**.

Nhung khi callback ve, `SaveAiGeneratedTestCasesCommand` **khong validate lai expectation co dung SRS hay khong**. No chi:

- tao `TestCase`, `TestCaseRequest`, `TestCaseExpectation` tu payload n8n
- normalize JSON/string
- validate `coveredRequirementIds` co thuoc SRS document khong
- tao `TestCaseRequirementLink`
- neu thieu `coveredRequirementIds` thi log warning, khong reject

Vi vay, trong unified flow, SRS la **instruction cho LLM/n8n**, chua phai **server-side oracle bat buoc**.

## Co that su lay du lieu tu LLM khi gen testcase khong?

Co, neu webhook config dung.

Cac dau hieu code:

- `N8nIntegrationService.TriggerWebhookAsync<TPayload,TResponse>` POST payload sang n8n va deserialize JSON response.
- Unified flow dung `TriggerWebhookWithResultAsync(...)` de trigger n8n roi doi callback.
- Legacy happy-path dung webhook `generate-happy-path` va parse `N8nHappyPathResponse`.
- Legacy boundary/negative dung webhook `generate-llm-suggestions` va parse `N8nBoundaryNegativeResponse`.
- `LlmScenarioSuggester` hien set `useCacheLookup = false`, log ro "Fresh n8n call is enforced for every generate request", nen nhanh LLM suggestion khong lay cache cu.

Tuy nhien, khong phai moi testcase deu la output LLM thuan:

- boundary/negative co the sinh path mutation va body mutation bang rule-based local code
- khi n8n transient failure, `LlmScenarioSuggester` co `local-fallback`
- sau khi parse LLM response, BE co them adaptive fallback scenarios neu thieu coverage
- request/expectation co the bi repair/normalize boi cac service noi bo

## Nhung buoc co the lam testcase khac voi output goc cua LLM

### 1. `ExpectationResolver` co the doi expectation

Trong legacy boundary/negative, sau khi LLM tra scenario, code tao `candidateExpectation`, sau do goi:

```csharp
_expectationResolver.Resolve(...)
```

Resolver co logic:

1. Thu lay expectation tu LLM.
2. Neu LLM co status hop le, dung LLM lam authoritative base, chi enrich bang SRS/Swagger khi thieu.
3. Neu LLM khong co status, fallback sang SRS.
4. Sau do Swagger.
5. Sau do default.

Diem can chu y: prompt cua `LlmScenarioSuggester` noi "SRS IS THE PRIMARY SOURCE OF TRUTH", nhung implementation cua `ExpectationResolver` lai uu tien LLM neu LLM da tra expected status hop le. Nghia la neu LLM tra `[200]` trong khi SRS constraint dang noi `400`, resolver co kha nang giu `[200]` vi no coi LLM da doc SRS roi.

Day la rui ro lon neu muc tieu cua ban la: **SRS phai override LLM mot cach bat buoc**.

### 2. `ContractAwareRequestSynthesizer` sua request

Sau khi parse LLM scenario, code goi:

```csharp
ContractAwareRequestSynthesizer.RepairScenario(...)
```

No co the them/sua:

- required path params
- required query params
- Authorization header
- request body neu missing/meaningless
- variable extraction
- placeholder cho dependency/resource id

Buoc nay giup testcase executable hon, nhung neu schema/API metadata sai hoac thieu, request co the bi synthesize sai so voi y do LLM/SRS.

### 3. Email uniqueness enforcement sua body

`EnforceEmailUniqueness(...)` co the sua email literal trong register-like scenario thanh email co `{{tcUniqueId}}`.

Buoc nay hop ly de tranh collision runtime, nhung van la thay doi sau LLM. Rieng duplicate email case duoc co gang giu `{{registeredEmail}}`.

### 4. Adaptive coverage them testcase khong phai LLM tra ve

`EnsureAdaptiveCoverage(...)` them fallback scenario neu endpoint thieu HappyPath/Negative/Boundary theo target. Cac testcase nay duoc tao boi `CreateFallbackScenario(...)` dua tren:

- endpoint order
- Swagger metadata
- `ContractAwareRequestSynthesizer.BuildRequestData(...)`
- `ExpectationResolver`
- SRS neu match duoc

Day khong phai output LLM thuan. Neu dang review "LLM gen sai", can tach rieng testcase co tag `coverage-gap-fill`.

### 5. Happy-path legacy normalize expected status

Trong legacy happy-path, `NormalizeHappyPathExpectedStatuses(...)` loc chi 2xx va merge default theo HTTP method:

- POST -> `[201, 200]`
- PUT/PATCH -> `[200, 204]`
- DELETE -> `[204, 200, 202]`
- GET -> `[200]`

Day co the lam expected status rong hon so voi LLM tra ve, va khong dung SRS.

### 6. Dependency enricher them dependency/variables

`GeneratedTestCaseDependencyEnricher.Enrich(...)` duoc goi sau generation de wire dependency chain. Viec nay can cho runtime flow, nhung co the lam testcase chay theo thu tu/dependency khac voi output LLM.

## SRS analysis cung phu thuoc LLM

Truoc khi gen testcase dua tren SRS, SRS duoc phan tich bang webhook `analyze-srs`.

`TriggerSrsAnalysisCommand`:

- lay `ParsedMarkdown` hoac `RawContent`
- gui raw SRS content + endpoint refs sang n8n
- nhan `requirements`, `clarificationQuestions`
- persist thanh `SrsRequirement`

Neu n8n `analyze-srs` NotFound/not registered, co local fallback, nhung fallback chi la heuristic doc text va map constraint don gian.

Diem can chu y: `ProcessSrsAnalysisCallbackCommand` persist:

- `RequirementCode`
- `Title`
- `Description`
- `TestableConstraints`
- `Assumptions`
- `Ambiguities`
- `ConfidenceScore`
- `MappedEndpointPath`
- `IsReviewed = false`

Nhung code callback hien **khong set `EndpointId`** tu ket qua n8n, vi DTO chi co `mappedEndpointPath`. Trong khi cac buoc generation/expectation lai rat can `EndpointId` de match SRS voi endpoint.

He qua:

- requirement co `EndpointId = null` se bi xem nhu global trong nhieu cho
- SRS constraint co the ap vao nhieu endpoint hon muc can thiet
- hoac endpoint-specific prompt block khong duoc inject neu requirement chua reviewed/mapped ro
- can user/manual update `EndpointId` qua `UpdateSrsRequirementCommand` de mapping chinh xac hon

## Danh gia theo cau hoi cua ban

### 1. "Gen testcase co lay SRS lam chuan khong?"

Co, nhung **chua bat buoc du manh o server-side**.

- Unified flow: SRS duoc gui vao n8n va prompt yeu cau LLM derive expectation tu SRS.
- Legacy boundary/negative: SRS duoc dua vao prompt va `ExpectationResolver`.
- Legacy happy-path: khong thay SRS duoc dua vao payload.

Neu muon noi "SRS la chuan", can them validation sau LLM: expectedStatus/bodyContains/jsonPathChecks phai khop `effectiveConstraints`, sai thi reject hoac flag review.

### 2. "Hay dua hoan toan vao LLM?"

Khong.

He thong dua vao:

- LLM/n8n
- API metadata/Swagger
- approved API order
- endpoint business context
- global business rules
- SRS requirements
- rule-based path/body mutation
- local fallback/adaptive coverage
- request/expectation repair

Nen ket qua khong phai "LLM tu viet sao luu y nguyen vay".

### 3. "Co that su dang lay du lieu tu LLM khi gen testcase khong?"

Co, nhung co dieu kien:

- webhook config phai day du
- n8n phai response/callback dung contract
- khong roi vao local fallback

Rieng config hien check trong repo dang thieu key `generate-test-cases-unified`, trong khi unified mode dang bat. Neu khong co override ngoai appsettings, generate unified se fail truoc khi lay du lieu LLM.

### 4. "Co dang them nhieu thao tac lam testcase sai hoac khong chuan tu LLM khong?"

Co nhieu thao tac sau LLM. Khong the ket luan chung la "lam sai", vi nhieu buoc dung de testcase executable hon. Nhung day la cac diem co the lam sai:

- `ExpectationResolver` uu tien LLM status truoc SRS trong legacy boundary/negative.
- fallback scenario co the tao testcase khong do LLM tra ve.
- contract repair co the synthesize body/path/query/header sai neu Swagger/metadata sai.
- happy-path status normalization co the broaden expectedStatus khong theo SRS.
- SRS requirement mapping endpoint chua chac chinh xac vi analysis callback khong set `EndpointId`.
- unified callback save LLM output ma khong server-side verify expectation vs SRS.

## Khuyen nghi de bien SRS thanh chuan that su

1. Bo sung webhook config cho unified flow:

```json
"generate-test-cases-unified": "generate-test-cases-unified"
```

hoac cau hinh env var tuong duong.

2. Them buoc server-side SRS validation sau callback:

- parse `effectiveConstraints`
- compare voi `expectedStatus`
- compare voi `bodyContains/jsonPathChecks`
- neu mismatch thi mark `NeedsReview` hoac reject callback

3. Set/match `EndpointId` khi process SRS analysis:

- n8n nen tra `endpointId`
- BE nen map `mappedEndpointPath` -> endpoint GUID neu n8n chi tra path
- khong nen de requirement quan trong o `EndpointId = null` neu no chi ap cho mot endpoint

4. Ghi ro provenance tren testcase:

- `source = llm | srs | swagger | fallback | rule-based`
- `llmModel`
- `tokensUsed`
- `usedLocalFallback`
- `expectationSource`
- `coveredRequirementIds`

5. Neu muon output LLM "nguyen ban", can tat/gioi han:

- `EnsureAdaptiveCoverage`
- `ContractAwareRequestSynthesizer.RepairScenario`
- rule-based path/body mutations
- status normalization

Hoac it nhat tag ro testcase nao la `llm-suggested`, `rule-based`, `coverage-gap-fill`.

6. Neu muc tieu la testcase chuan theo SRS, nen uu tien luong:

SRS reviewed + EndpointId mapped -> unified n8n payload -> callback -> server-side SRS validator -> persist.

## Ket luan cuoi

Code hien tai **co thiet ke de dua SRS vao generation**, dac biet o unified flow va boundary/negative LLM flow. Nhung chat luong "SRS lam chuan" hien van phu thuoc nhieu vao:

- SRS analysis co extract dung constraint khong
- requirement co duoc reviewed/refined/mapped endpoint khong
- n8n/LLM co tuan prompt khong
- fallback/repair co can thiep qua muc khong
- config webhook co day du khong

Vi vay, neu testcase dang sai expected status/body/assertion so voi SRS, nguyen nhan co kha nang nam o mot trong 4 lop:

1. SRS extraction/mapping sai hoac thieu `EndpointId`.
2. n8n/LLM khong follow `effectiveConstraints`.
3. BE post-processing/fallback thay doi output.
4. Unified workflow chua goi duoc n8n do thieu webhook config.

Uu tien fix dau tien nen la **config unified webhook** va **server-side SRS expectation validator**, vi day la hai cho quyet dinh "co that su lay LLM/SRS dung cach hay khong".
