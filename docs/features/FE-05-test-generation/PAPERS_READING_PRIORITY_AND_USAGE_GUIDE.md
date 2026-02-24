# FE-05 Papers Reading Priority And Usage Guide

Cap nhat lan cuoi: 2026-02-23

## 1) Muc tieu tai lieu

Tai lieu nay gom toan bo phan tich papers trong `papers/` vao 1 noi de:

- Xac dinh nen uu tien doc paper nao truoc.
- Hieu ro thuat toan chinh cua tung paper.
- Map truc tiep vao bai toan hien tai cua du an: FE-05A (order gate) va FE-05B (happy-path generation).

## 2) Bai toan hien tai trong codebase

Yeu cau FE-05:

- FE-05A la mandatory gate truoc FE-05B.
- Thu tu API phai duoc xac nhan va luu snapshot (`AppliedOrder`) truoc khi generate test.
- Flow phai deterministic.

Tham chieu:

- `docs/features/FE-05-test-generation/requirement.json`
- `docs/features/FE-05-test-generation/FE-05B/requirement.json`

Tinh trang implementation hien tai:

- Order dang sort theo heuristic auth/dependencyCount/httpMethod/path:
  - `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs:67`
- Dependency metadata dang suy luan kha don gian:
  - auth keyword matching + security flag:
  - `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs:139`
  - dependency chi dua tren path co `{id}` va POST cung resource:
  - `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs:155`

## 3) Thu tu uu tien doc papers (khuyen nghi)

1. `papers/KAT.pdf`
2. `papers/2411.07098v2.pdf`
3. `papers/COmbine.pdf`
4. `papers/AutoRestTest.pdf`
5. `papers/API_Response_Body_Testing.pdf` va `papers/API_Response_Body_Testing - Combining_Static_and_Dynamic_Approaches_for_Mining.pdf` (doc de doi chieu, uu tien thap hon)

## 4) Tom tat tung paper: thuat toan va muc dich su dung

### 4.1) KAT (uu tien cao nhat cho FE-05A)

- File: `papers/KAT.pdf`
- Nguon: arXiv:2407.10227v1 (2024-07-14)
- Y tuong chinh:
  - Dung GPT + prompt engineering de xay ODG (Operation Dependency Graph).
  - Ket hop:
    - Heuristic dependencies.
    - Operation-Schema dependencies.
    - Schema-Schema dependencies.
  - Tu ODG sinh operation sequences.
  - Sinh valid/invalid test data va validation scripts.
- Muc dich su dung cho du an:
  - Nang cap FE-05A order proposal tu heuristic sang dependency-aware ordering.
  - Cai thien chat luong sequence cho FE-05B generation.

### 4.2) AutoRestTest research (cho bai toan lon va dynamic)

- File: `papers/2411.07098v2.pdf`
- Nguon: arXiv:2411.07098v2 (2025-01-22)
- Y tuong chinh:
  - SPDG (Semantic Property Dependency Graph) tao canh dua tren semantic similarity.
  - Multi-agent RL (operation/parameter/value/dependency agents).
  - Value decomposition Q-learning + epsilon-greedy + epsilon-decay.
  - Refine graph theo feedback response (2xx/4xx/5xx).
- Muc dich su dung cho du an:
  - Mo rong FE-05A tu static ordering sang adaptive ordering/ranking.
  - Phu hop specs lon, nhieu endpoint (vi du GitLab OpenAPI).

### 4.3) RBCTest / Combining Static + Dynamic (cho FE-05B/FE-08)

- File: `papers/COmbine.pdf`
- Nguon: arXiv:2504.17287v1 (2025-04-24)
- Y tuong chinh:
  - Static mining constraints tu OAS (LLM-based).
  - Dynamic invariants (AGORA/Daikon) tu runtime execution.
  - Observation-Confirmation prompting de giam hallucination.
  - Semantic verifier (doi chieu voi examples trong OAS) de loc false positives.
  - Sinh test scripts de validate response-body constraints.
- Muc dich su dung cho du an:
  - Dung de tao `TestCaseExpectation` chat luong cao cho FE-05B.
  - Chuan bi nen tang cho FE-08 deterministic validation (vuot qua status/schema check don thuan).

### 4.4) AutoRestTest tool paper (implementation-oriented)

- File: `papers/AutoRestTest.pdf`
- Nguon: arXiv:2501.08600v2 (2025-03-04)
- Y tuong chinh:
  - Ban tool/demo cua huong SPDG + MARL.
  - Mo ta ro cach config, request generation, reward, va bao cao.
- Muc dich su dung cho du an:
  - Lam reference implementation chi tiet de PoC nhanh.
  - Khong phai paper uu tien cao nhat ve novelty (vi da co paper research 2411).

### 4.5) Hai paper API response body testing con lai

- Files:
  - `papers/API_Response_Body_Testing.pdf`
  - `papers/API_Response_Body_Testing - Combining_Static_and_Dynamic_Approaches_for_Mining.pdf`
- Nhan dinh:
  - Thuoc cung line nghien cuu RBCTest voi `COmbine.pdf`.
  - Doc de doi chieu phrasing/prompt/experimental framing khi can.

## 5) Reading track theo phase implementation

### Track A: Giai bai toan FE-05A truoc (mandatory gate)

1. Doc `papers/KAT.pdf` de lay ODG + dependency extraction.
2. Doc `papers/2411.07098v2.pdf` de bo sung semantic graph + adaptive ranking.
3. Ap dung vao service metadata/order:
   - `ClassifiedAds.Modules.ApiDocumentation/Services/ApiEndpointMetadataService.cs`
   - `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderService.cs`

### Track B: Giai bai toan FE-05B + FE-08

1. Doc `papers/COmbine.pdf` de lay constraint mining + oracle generation.
2. Doi chieu `papers/API_Response_Body_Testing.pdf` khi can prompt chi tiet.
3. Ap dung vao:
   - sinh `TestCaseExpectation` logic-based.
   - deterministic validator cho response body.

### Track C: Tooling va PoC

1. Doc `papers/AutoRestTest.pdf` de tham khao cach to chuc pipeline va metrics.
2. Dung khi can setup PoC/benchmark nhanh.

## 6) De xuat roadmap ap dung ngan gon

1. V1 (FE-05A): thay heuristic dependency bang ODG-style dependency extractor (rule + semantic + LLM fallback).
2. V2 (FE-05A): nang algorithm ordering thanh dependency-aware topological ranking (van deterministic va co tie-break ro rang).
3. V3 (FE-05B): them constraint-aware expectation generation tu OAS descriptions.
4. V4 (FE-08): tao response validator theo constraint scripts (khong de LLM quyet dinh pass/fail).

## 7) Ghi chu thuc te khi doc

- Uu tien paper co tac dong truc tiep len blocker hien tai (FE-05A gate ordering) truoc.
- Tach ro:
  - LLM de hieu docs/sinh de xuat.
  - Rule-based engine de ra ket qua deterministic.
- Voi specs lon (vi du `papers/tmp_gitlab_openapi.yaml`), can tranh phu thuoc thuan heuristic path/method.
