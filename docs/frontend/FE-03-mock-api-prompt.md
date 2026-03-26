# Prompt Yeu Cau AI Dung File Mock JSON (FE-03)

Ban co the copy noi dung ben duoi va gui cho AI de tao mock JSON cho Frontend dua tren implementation hien tai cua FE-03.

---

**[Copy phan ben duoi gui cho AI]**

Dong vai tro la Backend Engineer va API Designer. Nhiem vu cua ban la tao **mot file JSON duy nhat** de Frontend mock API cho tinh nang `FE-03 (JSON Parsing Endpoints)`.

Nguon handoff chinh:

- `docs/frontend/FE-03-json-parsing-endpoints-frontend/specifications-api.json`
- `docs/frontend/FE-03-json-parsing-endpoints-frontend/endpoints-api.json`

Yeu cau:

1. Tao JSON hop le, de FE co the dung lam mock data hoac map vao mock service.
2. Bam sat contract hien tai cua backend, khong dung contract planning cu.
3. Bao gom du lieu cho:
   - `GET /api/projects/{projectId}/specifications`
   - `GET /api/projects/{projectId}/specifications/{specId}`
   - `GET /api/projects/{projectId}/specifications/upload-methods`
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/resolved-url`
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/path-param-mutations`
4. Bao gom sample request bodies cho:
   - upload specification
   - create manual specification
   - import cURL
   - create endpoint
   - update endpoint
5. Bao gom sample `application/problem+json` cho it nhat:
   - `400 Bad Request`
   - `404 Not Found`
6. It nhat co:
   - 3 specifications: 1 `Success`, 1 `Pending`, 1 `Failed`
   - 3-5 endpoints trong 1 specification
   - 1 endpoint detail co day du `parameters`, `responses`, `securityRequirements`
   - 1 `ResolvedUrlResult` thanh cong
   - 1 `ResolvedUrlResult` chua resolve du
   - 1 `PathParamMutationsResult` cho parameter `integer`
   - 1 `PathParamMutationsResult` cho parameter `uuid`
7. Cac luu y bat buoc phai dung:
   - `GET /specifications` tra `SpecificationModel[]`, khong phai `PaginatedResult`
   - `GET /endpoints` tra `EndpointModel[]`, khong phai `PaginatedResult`
   - `POST /upload`, `POST /manual`, `POST /curl-import` tra `201 Created`
   - `PUT /activate`, `PUT /deactivate` tra `200 OK`
   - `GET /upload-methods` chi tra `{ method, uploadApi }[]`
   - `tags` trong response endpoint la chuoi JSON array
   - `dataType` trong JSON request body phai la chuoi lowercase: `string`, `integer`, `number`, `boolean`, `object`, `array`, `uuid`

Muc tieu dau ra:

- file JSON gon, ro, de FE map vao state
- ten field dung 100% theo handoff contract
- sample values thuc te, de FE co the render list, detail, status badge, mutation table, va URL preview

**[Ket thuc doan copy]**
