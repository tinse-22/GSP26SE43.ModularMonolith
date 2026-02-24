# Phân tích chi tiết thuật toán trong TestGeneration Module

> Module TestGeneration chứa 8 thuật toán cốt lõi, dựa trên 3 bài báo nghiên cứu:
> - **KAT** (arXiv:2407.10227) — Automated API test ordering
> - **SPDG** (arXiv:2411.07098) — Semantic Property Dependency Graph
> - **RBCTest / COmbine** (arXiv:2504.17287) — LLM-based test expectation generation

---

## Mục lục

1. [Tổng quan Pipeline](#1-tổng-quan-pipeline)
2. [Thuật toán 1: Schema Reference Graph Builder](#2-thuật-toán-1-schema-reference-graph-builder)
3. [Thuật toán 2: Warshall's Transitive Closure](#3-thuật-toán-2-warshalls-transitive-closure)
4. [Thuật toán 3: Fuzzy Schema Name Extraction](#4-thuật-toán-3-fuzzy-schema-name-extraction)
5. [Thuật toán 4: Semantic Token Matching (5-Stage Pipeline)](#5-thuật-toán-4-semantic-token-matching-5-stage-pipeline)
6. [Thuật toán 5: English Singularization](#6-thuật-toán-5-english-singularization)
7. [Thuật toán 6: Modified Kahn's Topological Sort](#7-thuật-toán-6-modified-kahns-topological-sort)
8. [Thuật toán 7: Observation-Confirmation Prompting](#8-thuật-toán-7-observation-confirmation-prompting)
9. [Thuật toán 8: API Test Order Pipeline (Orchestrator)](#9-thuật-toán-8-api-test-order-pipeline-orchestrator)
10. [Bảng tổng hợp](#10-bảng-tổng-hợp)

---

## 1. Tổng quan Pipeline

Toàn bộ pipeline hoạt động theo luồng sau:

```
┌─────────────────────────────────────────────────────────────────────┐
│                    ApiTestOrderAlgorithm (Orchestrator)             │
│                                                                     │
│  Input: Danh sách API endpoints từ OpenAPI spec                     │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Bước 1a: Thu thập dependency đã tính trước (Rules 1-4)       │   │
│  │   → PathBased, OperationSchema, AuthBootstrap                │   │
│  │   → Confidence = 1.0                                         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          ↓                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Bước 1b: SchemaRelationshipAnalyzer                          │   │
│  │   → BuildSchemaReferenceGraph (trích $ref từ JSON)           │   │
│  │   → ComputeTransitiveClosure (Warshall's algorithm)          │   │
│  │   → FindTransitiveSchemaDependencies                         │   │
│  │   → Confidence = 0.85                                        │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          ↓                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Bước 1c: Fuzzy Schema Name Matching                          │   │
│  │   → ExtractSchemaBaseName (strip prefix/suffix)              │   │
│  │   → Match consumer ↔ producer bằng base name                 │   │
│  │   → Confidence = 0.65                                        │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          ↓                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Bước 2: Gộp tất cả dependency edges                          │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          ↓                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Bước 3: DependencyAwareTopologicalSorter                     │   │
│  │   → Modified Kahn's algorithm với fan-out ranking            │   │
│  │   → Xử lý cycle, tie-breaking đa tiêu chí                   │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                          ↓                                          │
│  Output: Danh sách endpoints đã sắp xếp theo thứ tự thực thi      │
└─────────────────────────────────────────────────────────────────────┘
```

**Ý tưởng cốt lõi:** Khi test API tự động, thứ tự rất quan trọng. Ví dụ:
- `POST /users` phải chạy trước `GET /users/{id}` (vì cần tạo user trước mới lấy được)
- `POST /auth/login` phải chạy trước mọi endpoint cần token
- `POST /categories` phải chạy trước `POST /products` (vì product cần `categoryId`)

Pipeline tự động phát hiện các quan hệ phụ thuộc này và sắp xếp thứ tự tối ưu.

---

## 2. Thuật toán 1: Schema Reference Graph Builder

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/SchemaRelationshipAnalyzer.cs` (phương thức `BuildSchemaReferenceGraph`)

**Nguồn gốc:** KAT paper Section 4.2

### Bài toán

Trong OpenAPI spec, các schema thường tham chiếu lẫn nhau qua `$ref`. Ví dụ:

```json
// Schema: OrderResponse
{
  "properties": {
    "user": { "$ref": "#/components/schemas/UserSummary" },
    "items": {
      "type": "array",
      "items": { "$ref": "#/components/schemas/OrderItem" }
    }
  }
}
```

`OrderResponse` tham chiếu `UserSummary` và `OrderItem`. Ta cần xây đồ thị có hướng thể hiện quan hệ này.

### Cách hoạt động

```
Input: Tập hợp tất cả schema JSON payloads từ endpoints

Bước 1: Trích xuất $ref bằng Regex
  Regex: #/(?:components/schemas|definitions)/(?<name>[A-Za-z0-9_.\-]+)
  
  Ví dụ:
    Payload chứa "$ref": "#/components/schemas/User"
    → Trích ra: "User"

Bước 2: Xây đồ thị co-reference
  Nếu một payload chứa refs đến {A, B, C}:
    → A → {B, C}
    → B → {A, C}  
    → C → {A, B}
  
  Tức là: tất cả schema xuất hiện cùng trong một payload
  đều có quan hệ với nhau.

Output: Dictionary<string, HashSet<string>>
  Ví dụ: {
    "OrderResponse" → {"UserSummary", "OrderItem"},
    "UserSummary" → {"OrderResponse"},
    "OrderItem" → {"OrderResponse"}
  }
```

### Ví dụ minh họa

```
Payload 1 (OrderResponse): refs = {UserSummary, OrderItem}
Payload 2 (UserDetail):    refs = {Address, UserSummary}
Payload 3 (ProductView):   refs = {Category}

Đồ thị kết quả:
  OrderResponse ──→ UserSummary ←── UserDetail
       │                                │
       ↓                                ↓
   OrderItem                        Address
  
  ProductView ──→ Category
```

### Xử lý đặc biệt

- **Bare single ref**: Payload chỉ chứa đúng 1 `$ref` và không có `properties`, `items`, `allOf`, `anyOf`, `oneOf` → bỏ qua (chỉ là pointer, không phải definition)
- **Case-insensitive**: Tất cả so sánh tên schema đều không phân biệt hoa-thường

---

## 3. Thuật toán 2: Warshall's Transitive Closure

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/SchemaRelationshipAnalyzer.cs` (phương thức `ComputeTransitiveClosure`)

**Nguồn gốc:** KAT paper Section 4.2, dựa trên thuật toán Floyd-Warshall cổ điển

### Bài toán

Từ đồ thị tham chiếu trực tiếp (bước trước), ta cần tìm **tất cả** quan hệ phụ thuộc, kể cả gián tiếp.

Ví dụ:
- `A` ref trực tiếp `B`
- `B` ref trực tiếp `C`
- → `A` phụ thuộc **gián tiếp** vào `C` (qua `B`)

### Cách hoạt động

```
Input: Đồ thị tham chiếu trực tiếp (direct references)
  A → {B}
  B → {C}
  C → {}

Thuật toán Warshall (3 vòng lặp lồng nhau):

  foreach k in tất_cả_nodes:       // Node trung gian
    foreach i in tất_cả_nodes:     // Node nguồn
      if i có cạnh đến k:
        foreach j in cạnh_của_k:   // Node đích
          if i ≠ j:
            thêm cạnh i → j

Quá trình:
  k = A: không thay đổi
  k = B: i = A có cạnh đến B → kEdges(B) = {C} → thêm A → C
  k = C: không thay đổi

Output (Transitive Closure):
  A → {B, C}    ← A giờ phụ thuộc cả C (gián tiếp qua B)
  B → {C}
  C → {}
```

### Độ phức tạp

- **Thời gian:** $O(n^3)$ với $n$ = số schema names
- **Không gian:** $O(n^2)$ cho ma trận closure
- Trong thực tế $n$ thường nhỏ (vài chục đến vài trăm schema), nên hoàn toàn chấp nhận được.

### Ứng dụng tiếp theo

Sau khi có transitive closure, phương thức `FindTransitiveSchemaDependencies` dùng nó để:

```
Với mỗi endpoint Consumer có parameter schema ref P:
  Tìm transitive closure(P) = {S1, S2, S3, ...}
  Với mỗi Si trong closure:
    Tìm endpoint Producer nào có response schema ref = Si
    → Tạo cạnh: Consumer phụ thuộc Producer (confidence = 0.85)
```

**Ví dụ thực tế:**
```
PUT /orders/{id} — param schema refs: "UpdateOrderRequest"
  → UpdateOrderRequest nội bộ refs "OrderItem"
  → Transitive closure: UpdateOrderRequest → {OrderItem}
  → POST /items — response schema: "OrderItem"
  → KẾT LUẬN: PUT /orders/{id} phụ thuộc POST /items
```

---

## 4. Thuật toán 3: Fuzzy Schema Name Extraction

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/SchemaRelationshipAnalyzer.cs` (phương thức `ExtractSchemaBaseName` và `FindFuzzySchemaNameDependencies`)

**Nguồn gốc:** KAT paper Section 4.2 (mở rộng)

### Bài toán

Nhiều API dùng các schema khác nhau cho cùng một entity:
- `CreateUserRequest` (input khi tạo)
- `UserResponse` (output khi trả về)
- `UpdateUserDto` (input khi cập nhật)

Cả 3 đều liên quan đến entity **User**, nhưng tên khác nhau hoàn toàn. Thuật toán `$ref` trực tiếp không phát hiện được.

### Cách hoạt động

```
Input: Tên schema (ví dụ: "CreateUserRequest")

Bước 1: Strip suffix (dài nhất trước)
  Danh sách suffix:
    ListResponse, ListResult, PagedResult,
    Request, Response, Dto, DTO, Model, Input, Output,
    Command, Query, Event, Payload, Body, Result,
    Create, Update, Patch, Delete
  
  "CreateUserRequest" → strip "Request" → "CreateUser"

Bước 2: Strip prefix (dài nhất trước)
  Danh sách prefix:
    Create, Update, Patch, Delete, Add, Remove,
    Get, List, Search, Find, Fetch
  
  "CreateUser" → strip "Create" → "User"

Bước 3: Validate kết quả
  Kết quả phải có ≥ 2 ký tự

Output: "User"
```

### Ví dụ nhiều schema

```
CreateUserRequest   → strip "Request" → "CreateUser" → strip "Create" → "User"
UserResponse        → strip "Response" → "User"
UpdateUserDto       → strip "Dto" → "UpdateUser" → strip "Update" → "User"
UserListResponse    → strip "ListResponse" → "User"
GetUserQuery        → strip "Query" → "GetUser" → strip "Get" → "User"
```

Tất cả đều trả về base name `"User"` → thuật toán biết chúng liên quan.

### Ứng dụng

```
Với mỗi cặp (Consumer, Producer):
  Consumer param schema base name == Producer response schema base name?
    → Nếu trùng VÀ schema gốc khác nhau (không trùng exact)
    → Tạo cạnh phụ thuộc (confidence = 0.65)
```

**Vì sao confidence thấp hơn?** Fuzzy matching có thể false positive. Ví dụ `CategoryRequest` và `CategorizedResponse` đều có thể trích ra base khác nhau nhưng vẫn match sai. Confidence 0.65 đủ để đưa vào xem xét nhưng không bắt buộc ordering.

---

## 5. Thuật toán 4: Semantic Token Matching (5-Stage Pipeline)

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/SemanticTokenMatcher.cs`

**Nguồn gốc:** SPDG paper (arXiv:2411.07098) Section 3.2

### Bài toán

Phát hiện quan hệ phụ thuộc giữa API endpoints bằng cách so khớp **tên token** (tên tham số, tên resource trong path). Ví dụ:

- Parameter `categoryId` trong `POST /products` → liên quan đến resource `/categories`
- Parameter `org` → liên quan đến resource `/organizations`

### Pipeline 5 bước (theo thứ tự ưu tiên)

Khi so sánh 2 token `source` và `target`, thuật toán thử lần lượt 5 bước. Dừng ngay khi match:

#### Bước 1: Exact Match (score = 1.0)

```
So sánh case-insensitive trực tiếp.

"category" vs "category" → MATCH ✓ (score = 1.0)
"User" vs "user"         → MATCH ✓ (score = 1.0)
"category" vs "product"  → FAIL → tiếp bước 2
```

#### Bước 2: Plural/Singular Normalization (score = 0.95)

```
Chuyển cả hai về dạng singular, rồi so sánh.

"categories" → singularize → "category"
"category"   → singularize → "category"
→ MATCH ✓ (score = 0.95)

"users" → "user"
"user"  → "user"
→ MATCH ✓ (score = 0.95)
```

(Xem chi tiết singularization ở Thuật toán 5 bên dưới)

#### Bước 3: Abbreviation Matching (score = 0.85)

```
Bảng 30+ viết tắt phổ biến trong API (hai chiều):

  "cat"    ↔ "category", "categories"
  "org"    ↔ "organization", "organisations"
  "repo"   ↔ "repository", "repositories"
  "auth"   ↔ "authentication", "authorization"
  "config" ↔ "configuration"
  "env"    ↔ "environment"
  "msg"    ↔ "message"
  "usr"    ↔ "user"
  "acct"   ↔ "account"
  "tx"     ↔ "transaction"
  ... (tổng 30+ mapping)

"cat" vs "category"  → MATCH ✓ (score = 0.85)
"org" vs "organization" → MATCH ✓ (score = 0.85)

Cũng hỗ trợ reverse: nếu cả 2 token đều expand về cùng 1 abbreviation
→ cũng match.
```

#### Bước 4: Stem Match (score = 0.80)

```
Strip hậu tố phổ biến tiếng Anh, so sánh phần gốc.

Hậu tố: tion, sion, ment, ness, ity, ing, ous, ive, ful,
         less, able, ible, ance, ence, ated, ting, ted, ed

"creating" → strip "ing" → "creat"
"created"  → strip "ed"  → "creat"
→ MATCH ✓ (score = 0.80)

"notification" → strip "tion" → "notifica"
"notifying"    → strip "ing"  → "notify"  (khác "notifica")
→ FAIL

Điều kiện: phần gốc phải ≥ 3 ký tự.
```

#### Bước 5: Substring Containment (score = 0.70)

```
Token ngắn hơn phải ≥ 3 ký tự và nằm trong token dài hơn.

"user" vs "userId"    → "user" ⊂ "userId" → MATCH ✓ (score = 0.70)
"category" vs "categoryId" → MATCH ✓ (score = 0.70)

"us" vs "userId" → FAIL (ngắn hơn < 3 ký tự)
```

### Ngưỡng lọc

Mặc định `minScore = 0.65`. Bất kỳ match nào có score ≥ 0.65 đều được giữ lại. Kết quả được sắp xếp theo score giảm dần.

### Ví dụ end-to-end

```
Source tokens: ["categoryId", "name", "price"]   (params của POST /products)
Target tokens: ["category", "categories"]         (resource path tokens)

Kết quả matching:
  "categoryId" vs "category" → Bước 5 Substring → score 0.70 ✓
  "categoryId" vs "categories" → singularize "categories"→"category", 
                                  rồi Substring → score 0.70 ✓
  "name" vs "category" → tất cả 5 bước FAIL
  "price" vs "category" → tất cả 5 bước FAIL

→ Phát hiện: POST /products phụ thuộc POST /categories 
  (vì tham số categoryId match với resource categories)
```

---

## 6. Thuật toán 5: English Singularization

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/SemanticTokenMatcher.cs` (phương thức `Singularize`)

**Được dùng bởi:** Semantic Token Matching (Bước 2)

### Bài toán

Chuyển danh từ số nhiều tiếng Anh thành số ít để so sánh chính xác hơn.

### Quy tắc (theo thứ tự ưu tiên)

```
1. Irregular plurals (tra bảng):
   people     → person
   children   → child
   men        → man
   women      → woman
   mice       → mouse
   data       → datum
   criteria   → criterion
   analyses   → analysis
   indices    → index
   matrices   → matrix
   vertices   → vertex
   statuses   → status
   addresses  → address

2. -ies → -y (nhưng KHÔNG áp dụng cho "series"):
   categories  → category
   companies   → company
   activities  → activity

3. -ses, -zes, -xes, -ches, -shes → bỏ -es:
   addresses → address    (đã xử lý ở bước 1)
   quizzes   → quiz + z = quizz (edge case)
   boxes     → box
   watches   → watch
   crashes   → crash

4. -s → bỏ -s (KHÔNG áp dụng nếu kết thúc bằng "ss", "us", "is"):
   users     → user
   products  → product
   items     → item
   
   NGOẠI LỆ (giữ nguyên):
   address  (kết thúc "ss") — nhưng đã xử lý rule 1
   status   (kết thúc "us")
   analysis (kết thúc "is")

5. Nếu không match rule nào → giữ nguyên
```

### Tại sao cần?

Trong API, cùng một resource có thể xuất hiện ở dạng số ít và số nhiều:
- Path: `/categories` (số nhiều)
- Parameter: `categoryId` (số ít)
- Schema: `Category` (số ít)

Nếu không chuẩn hóa, thuật toán sẽ bỏ lỡ các match rõ ràng.

---

## 7. Thuật toán 6: Modified Kahn's Topological Sort

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/DependencyAwareTopologicalSorter.cs`

**Nguồn gốc:** KAT paper (arXiv:2407.10227) Section 4.3

### Bài toán

Cho một tập endpoints và các cạnh phụ thuộc (dependency edges), sắp xếp chúng theo thứ tự sao cho mọi endpoint producer đều chạy trước endpoint consumer.

Đây chính là bài toán **Topological Sort** trên đồ thị có hướng (DAG).

### Kahn's Algorithm cơ bản

```
1. Tính in-degree (số cạnh đi vào) cho mỗi node
2. Queue = tất cả node có in-degree = 0
3. While queue không rỗng:
   a. Lấy 1 node từ queue → thêm vào kết quả
   b. Với mỗi neighbor (node phụ thuộc vào node vừa lấy):
      - Giảm in-degree đi 1
      - Nếu in-degree = 0 → thêm vào queue
4. Nếu kết quả chưa đủ → có cycle
```

### Các cải tiến so với Kahn's chuẩn (KAT paper)

#### Cải tiến 1: Fan-out Ranking

**Vấn đề:** Kahn's chuẩn không định nghĩa cách chọn khi nhiều node cùng có in-degree = 0.

**Giải pháp:** Ưu tiên node có **fan-out cao** (nhiều node khác phụ thuộc vào nó). Node quan trọng nhất chạy trước.

```
Ví dụ:
  POST /auth/login  → fan-out = 10 (10 endpoint cần token)
  POST /categories  → fan-out = 3  (3 endpoint cần categoryId)
  POST /tags        → fan-out = 1  (1 endpoint cần tagId)

Cả 3 đều có in-degree = 0 (không phụ thuộc ai)
→ Chọn POST /auth/login trước (fan-out cao nhất)
→ Rồi POST /categories
→ Cuối cùng POST /tags
```

#### Cải tiến 2: Multi-criteria Tie-breaking (6 tiêu chí)

Khi fan-out bằng nhau, áp dụng tie-breaking theo thứ tự:

```
1. Auth-related first:     Endpoint xác thực luôn đứng đầu
2. Fan-out descending:     Nhiều dependent hơn → ưu tiên
3. In-degree ascending:    Ít dependency hơn → ưu tiên  
4. HTTP method weight:     POST(1) → PUT(2) → PATCH(3) → GET(4) → DELETE(5)
5. Path alphabetical:      /admin/users trước /users
6. GUID:                   Đảm bảo deterministic tuyệt đối
```

**Tại sao HTTP method weight?**
- `POST` tạo resource → cần chạy trước
- `PUT/PATCH` cập nhật resource → cần resource đã tồn tại
- `GET` đọc resource → cần resource đã tồn tại
- `DELETE` xóa resource → chạy cuối cùng

#### Cải tiến 3: Cycle Detection & Breaking

**Vấn đề:** Đồ thị phụ thuộc có thể có cycle (A phụ thuộc B, B phụ thuộc A).

**Giải pháp:**
```
Khi available (in-degree = 0) rỗng nhưng chưa sort hết:
  → Cycle detected!
  
Chọn "cycle breaker" = node chưa visited có:
  1. In-degree thấp nhất (ít ràng buộc nhất)
  2. Fan-out cao nhất (quan trọng nhất)
  3. Auth-related ưu tiên
  4. HTTP method weight
  5. Path alphabetical
  6. GUID

→ Bỏ qua dependency cycle, đưa node này vào kết quả
→ Đánh dấu IsCycleBreak = true trong metadata
```

#### Cải tiến 4: Confidence Threshold

```
Chỉ những edge có Confidence ≥ 0.5 mới tham gia ordering.

Edge confidence:
  Rule-based (PathBased, OperationSchema)  = 1.0  → enforce
  SchemaSchema transitive                  = 0.85 → enforce  
  Fuzzy schema name                        = 0.65 → enforce
  Nếu < 0.5                               → chỉ ghi nhận, không enforce

Edge dưới ngưỡng vẫn được lưu trong metadata (DependencyEdges)
để người dùng review, nhưng không ảnh hưởng thứ tự sort.
```

### Ví dụ minh họa đầy đủ

```
Endpoints:
  A: POST /auth/login        (auth, fan-out=3)
  B: POST /categories        (fan-out=2)
  C: POST /products          (depends: B, A)
  D: GET /products/{id}      (depends: C)
  E: DELETE /products/{id}   (depends: C)

Dependency edges:
  C → B (OperationSchema, conf=1.0): products cần categoryId
  C → A (AuthBootstrap, conf=1.0): cần token
  D → C (PathBased, conf=1.0): GET cần POST trước
  E → C (PathBased, conf=1.0): DELETE cần POST trước
  D → A (AuthBootstrap, conf=1.0): cần token
  E → A (AuthBootstrap, conf=1.0): cần token

In-degree: A=0, B=0, C=2, D=2, E=2
Fan-out:   A=3, B=1, C=2, D=0, E=0

Iteration 1: available = {A, B}
  A: auth=true, fan-out=3
  B: auth=false, fan-out=1
  → Chọn A (auth first + fan-out cao hơn)
  → Update: C in-degree 2→1, D 2→1, E 2→1

Iteration 2: available = {B}
  → Chọn B
  → Update: C in-degree 1→0

Iteration 3: available = {C}
  → Chọn C
  → Update: D 1→0, E 1→0

Iteration 4: available = {D, E}
  D: GET(weight=4), E: DELETE(weight=5)
  → Chọn D (GET before DELETE)

Iteration 5: available = {E}
  → Chọn E

Kết quả: A → B → C → D → E
  1. POST /auth/login       [AUTH_FIRST, HIGH_FAN_OUT]
  2. POST /categories       [PRODUCER_FIRST]
  3. POST /products         [DEPENDENCY_FIRST]
  4. GET /products/{id}     [DEPENDENCY_FIRST]
  5. DELETE /products/{id}  [DEPENDENCY_FIRST]
```

### Reason Codes (metadata output)

Mỗi kết quả đi kèm reason codes giải thích vị trí:

| Code | Ý nghĩa |
|------|---------|
| `AUTH_FIRST` | Endpoint auth, luôn ưu tiên đầu |
| `DEPENDENCY_FIRST` | Có dependencies phải chạy trước |
| `PRODUCER_FIRST` | Là producer cho endpoints khác |
| `HIGH_FAN_OUT` | Fan-out > 2, rất quan trọng |
| `CYCLE_BREAK_FALLBACK` | Vị trí do phá cycle, không đảm bảo tối ưu |
| `DETERMINISTIC_TIE_BREAK` | Luôn có (mọi node) |

---

## 8. Thuật toán 7: Observation-Confirmation Prompting

**File:** `ClassifiedAds.Modules.TestGeneration/Algorithms/ObservationConfirmationPromptBuilder.cs`

**Nguồn gốc:** COmbine/RBCTest paper (arXiv:2504.17287) Section 3

### Bài toán

Dùng LLM (Large Language Model) để tự động sinh test expectation cho API endpoints. Nhưng LLM hay **hallucinate** (bịa ra constraints không có trong spec).

### Giải pháp: Pattern 2 pha

```
┌─────────────────────────────────────────────────┐
│ Phase 1: OBSERVATION                             │
│                                                  │
│ Prompt: "Đọc spec này và liệt kê TẤT CẢ        │
│          constraints bạn tìm thấy.               │
│          KHÔNG lọc, KHÔNG đánh giá."             │
│                                                  │
│ LLM output (ví dụ):                              │
│   1. id phải là string format uuid               │
│   2. name phải là string, required               │
│   3. createdAt ≤ updatedAt                       │
│   4. status phải là enum [active, inactive]      │
│   5. price > 0 (← có thể hallucinated!)         │
└─────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────┐
│ Phase 2: CONFIRMATION                            │
│                                                  │
│ Prompt: "Xác nhận từng constraint trên:          │
│   - Trích dẫn CHÍNH XÁC text trong spec         │
│   - Cross-check với examples                     │
│   - KEEP nếu có bằng chứng, REMOVE nếu suy đoán"│
│                                                  │
│ LLM output (sau xác nhận):                       │
│   1. id: KEEP - schema defines {type:string,     │
│          format:uuid}                            │
│   2. name: KEEP - required in schema             │
│   3. createdAt ≤ updatedAt: KEEP - spec says     │
│      "updatedAt is set when modified"            │
│   4. status: KEEP - enum defined in schema       │
│   5. price > 0: REMOVE - no evidence in spec     │
│      (← hallucination bị loại!)                  │
└─────────────────────────────────────────────────┘
```

### System Prompt

LLM được giao vai:
```
"You are a precise API test engineer. Your task is to generate test 
expectations based STRICTLY on the OpenAPI specification provided."
```

Rules:
- Chỉ dùng constraints có trong spec
- KHÔNG suy đoán
- Mỗi constraint PHẢI có evidence
- Output dạng JSON structured

### Output format (mỗi constraint)

```json
{
  "field": "response.body.id",
  "constraint": "must be a non-empty string",
  "type": "type_check",
  "evidence": "Schema defines id as { type: string, format: uuid }",
  "confidence": "high",
  "assertion": "expect(response.body.id).toBeType('string')"
}
```

Constraint types:
| Type | Ý nghĩa | Ví dụ |
|------|---------|-------|
| `type_check` | Kiểm tra kiểu dữ liệu | id phải là string |
| `value_check` | Kiểm tra giá trị cụ thể | status phải là "active" |
| `presence_check` | Kiểm tra field tồn tại | name phải có mặt |
| `format_check` | Kiểm tra format | email phải đúng format email |
| `range_check` | Kiểm tra min/max | age phải 0-150 |
| `relationship_check` | Quan hệ giữa các field | createdAt ≤ updatedAt |

### Spec Block Builder

Tự động trích xuất thông tin endpoint thành dạng doc cho LLM đọc:

```
## API Endpoint Specification

**Method:** POST
**Path:** /api/v1/products
**OperationId:** CreateProduct
**Summary:** Create a new product

### Parameters
- **X-Correlation-Id** (in: header, required: false)

### Request Body Schema
```json
{ "type": "object", "properties": { "name": {...}, "price": {...} } }
```

### Responses
- **201**: Product created successfully
- **400**: Validation error
- **401**: Unauthorized

### Primary Response Body Schema
```json
{ "type": "object", "properties": { "id": {...}, "name": {...} } }
```
```

### Cross-Endpoint Context

Khi generate prompt cho một chuỗi endpoints (đã sort), thuật toán thêm context về data flow:

```
## Cross-Endpoint Context

This endpoint is tested AFTER the following endpoints (in order):
- `POST /categories` (Create a new category)
- `POST /auth/login` (Authenticate user)

Consider data produced by previous endpoints when generating expectations.
For example, if POST /users was called before GET /users/{id}, the 
response should match the created user.
```

Hai endpoint được coi là "related" nếu chia sẻ ít nhất 1 path segment (bỏ qua version prefix và path parameters).

### Combined Prompt (Single-shot)

Cho models không hỗ trợ multi-turn, kết hợp cả 2 pha thành 1 prompt với Chain-of-Thought:

```
Step 1: Observe — liệt kê tất cả constraints
Step 2: Confirm — xác nhận từng cái với evidence
Step 3: Output — chỉ trả confirmed constraints
```

---

## 9. Thuật toán 8: API Test Order Pipeline (Orchestrator)

**File:** `ClassifiedAds.Modules.TestGeneration/Services/ApiTestOrderAlgorithm.cs`

**Nguồn gốc:** Kết hợp KAT + SPDG papers

### Mục đích

Đây không phải thuật toán riêng, mà là **orchestrator** gọi tất cả thuật toán trên theo đúng thứ tự.

### Luồng thực thi chi tiết

```csharp
BuildProposalOrder(endpoints):

// ═══════ BƯỚC 1: Thu thập tất cả dependency edges ═══════

// 1a: Pre-computed rules (từ ApiEndpointMetadataService)
//     Rule 1 (PathBased):       GET /users/{id} → POST /users
//     Rule 2 (OperationSchema): param ref "User" == response ref "User"
//     Rule 3 (SemanticToken):   param "userId" → resource /users
//     Rule 4 (AuthBootstrap):   secured endpoint → POST /auth/login
//     → Confidence = 1.0

foreach endpoint:
  foreach depId in endpoint.DependsOnEndpointIds:
    edges.Add(new DependencyEdge {
      Source = endpoint.Id,
      Target = depId,
      Type = OperationSchema,
      Confidence = 1.0
    })

// 1b: Schema-Schema transitive dependencies (KAT Section 4.2)
//     Gọi SchemaRelationshipAnalyzer:
//       - BuildSchemaReferenceGraph(allSchemaPayloads)
//       - ComputeTransitiveClosure(directGraph)
//       - FindTransitiveSchemaDependencies(params, responses, closure)
//     → Confidence = 0.85

// 1c: Fuzzy schema name matching
//     Gọi FindFuzzySchemaNameDependencies(params, responses)
//     → Confidence = 0.65

// ═══════ BƯỚC 2: Build sortable operations ═══════

sortableOps = endpoints.Select(e => new SortableOperation {
  OperationId = e.EndpointId,
  HttpMethod = e.HttpMethod,
  Path = e.Path,
  IsAuthRelated = e.IsAuthRelated
})

// ═══════ BƯỚC 3: Topological Sort ═══════

sortedResults = topologicalSorter.Sort(sortableOps, allEdges)
// → Modified Kahn's with fan-out ranking

// ═══════ BƯỚC 4: Map to output model ═══════

return sortedResults.Select(r => new ApiOrderItemModel {
  EndpointId = r.OperationId,
  HttpMethod = endpoint.HttpMethod,
  Path = endpoint.Path,
  OrderIndex = r.OrderIndex,
  DependsOnEndpointIds = r.Dependencies,
  ReasonCodes = r.ReasonCodes,
  IsAuthRelated = endpoint.IsAuthRelated
})
```

### Phân tầng Confidence

| Nguồn | Confidence | Giải thích |
|-------|:---:|-----------|
| Pre-computed rules (1-4) | 1.0 | Chắc chắn, dựa trên rule xác định |
| Schema-Schema transitive (Warshall) | 0.85 | Khá chắc, qua chain `$ref` hợp lệ |
| Fuzzy schema name | 0.65 | Tương đối, có thể false positive |
| Threshold để enforce ordering | ≥ 0.5 | Edge dưới ngưỡng chỉ ghi nhận |

---

## 10. Bảng tổng hợp

| # | Thuật toán | File chính | Bài báo | Độ phức tạp | Confidence |
|:---:|-----------|-----------|:-------:|:-----------:|:----------:|
| 1 | Schema Reference Graph Builder | `SchemaRelationshipAnalyzer.cs` | KAT §4.2 | $O(P \cdot R)$ | — |
| 2 | Warshall's Transitive Closure | `SchemaRelationshipAnalyzer.cs` | KAT §4.2 | $O(n^3)$ | — |
| 3 | Fuzzy Schema Name Extraction | `SchemaRelationshipAnalyzer.cs` | KAT §4.2+ | $O(S \cdot P)$ | 0.65 |
| 4 | Semantic Token Matching (5-stage) | `SemanticTokenMatcher.cs` | SPDG §3.2 | $O(S \cdot T)$ | 0.70–1.0 |
| 5 | English Singularization | `SemanticTokenMatcher.cs` | SPDG §3.2 | $O(1)$ | — |
| 6 | Modified Kahn's Topological Sort | `DependencyAwareTopologicalSorter.cs` | KAT §4.3 | $O(V + E)$ | ≥ 0.5 |
| 7 | Observation-Confirmation Prompting | `ObservationConfirmationPromptBuilder.cs` | RBCTest §3 | — | — |
| 8 | API Test Order Pipeline | `ApiTestOrderAlgorithm.cs` | KAT+SPDG | — | — |

> **Ký hiệu:** $P$ = số payloads, $R$ = số refs/payload, $n$ = số schema names, $S$ = source tokens, $T$ = target tokens, $V$ = số operations, $E$ = số edges

---

## Tham khảo bài báo

1. **KAT** — *Keyword-driven Automated API Testing* (arXiv:2407.10227)
   - Section 4.2: Schema-Schema Dependencies (Warshall's transitive closure)
   - Section 4.3: Sequence Generation (Modified Kahn's with fan-out ranking)

2. **SPDG** — *Semantic Property Dependency Graph* (arXiv:2411.07098)
   - Section 3.2: Token matching pipeline cho dependency detection

3. **RBCTest / COmbine** — *Rule-Based Constraint Testing* (arXiv:2504.17287)
   - Section 3: Observation-Confirmation prompting pattern cho LLM
