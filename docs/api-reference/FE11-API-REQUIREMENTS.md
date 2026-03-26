# FE-11 Manual Entry API Requirements

## 1) Scope
- Feature: `FE-11 | Manual Entry mode | ApiDocumentation`
- Package nay chi cover flow manual entry that su:
  - tao manual specification
  - xem manual specification
  - CRUD endpoint inline trong specification
- Ngoai scope:
  - OpenAPI/Postman upload (`FE-02`)
  - cURL import (`FE-13`)
  - `resolved-url` va `path-param-mutations` (`FE-12`)

## 2) FE Flow
1. FE da co `projectId` tu flow project management co san.
2. FE goi `POST /api/projects/{projectId}/specifications/manual` de tao manual spec.
3. Sau khi tao, FE load spec detail va endpoint list.
4. User them/sua/xoa endpoint inline duoi spec vua tao.

## 3) Cross-cutting Rules
- Tat ca endpoint deu can `Authorization: Bearer <token>`.
- Backend con check ownership cua project + permission theo tung endpoint.
- HTTP methods hop le: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`.
- Parameter locations hop le: `Path`, `Query`, `Header`, `Body`, `Cookie`.
- Parameter data types hop le: `string`, `integer`, `number`, `boolean`, `object`, `array`, `uuid`.
- Neu `location` khong map duoc, backend fallback ve `Query`.
- `Create manual spec` set `sourceType = Manual`, `parseStatus = Success`.
- `autoActivate = true` se activate spec moi va deactivate spec cu.
- `PUT endpoint` la replace-all cho `parameters` va `responses`.
- Hanh vi hien tai da gom FE-12 enhancement: path params duoc backend auto-sync theo placeholder trong path.
- `tags` request la `string[]`, nhung `tags` response hien tai la chuoi JSON serialized.
- Request body manual hien tai duoc model hoa qua `parameters` co `location=Body` + `schema/examples`, chua co field `contentType` rieng.

## 4) Endpoint Contract

### 4.1 `apidoc.manual.specifications.create`
- Route: `POST /api/projects/{projectId}/specifications/manual`
- Permission: `Permission:AddSpecification`
- Body:
  - `name` required, max `200`
  - `version?`
  - `autoActivate?` default `false`
  - `endpoints[]` required, min `1`
- `ManualEndpointDefinition`:
  - `httpMethod` required
  - `path` required, max `500`
  - `operationId?`, `summary?`, `description?`
  - `tags?: string[]`
  - `isDeprecated?`
  - `parameters?: ManualParameterDefinition[]`
  - `responses?: ManualResponseDefinition[]`
- `ManualParameterDefinition`:
  - `name?`, `location?`, `dataType?`, `format?`, `isRequired?`, `defaultValue?`, `schema?`, `examples?`
- `ManualResponseDefinition`:
  - `statusCode?`, `description?`, `schema?`, `examples?`, `headers?`
- Success:
  - `201` -> `SpecificationDetailModel`
  - FE nen ky vong:
    - `sourceType = Manual`
    - `parseStatus = Success`
    - `endpointCount = endpoints.length`
- Error:
  - `400`, `401`, `403`, `404`

### 4.2 `apidoc.manual.specifications.getById`
- Route: `GET /api/projects/{projectId}/specifications/{specId}`
- Permission: `Permission:GetSpecifications`
- Success:
  - `200` -> `SpecificationDetailModel`
- Response chinh:
  - `id`, `projectId`, `name`, `sourceType`, `version`, `isActive`
  - `parseStatus`, `parsedAt`, `endpointCount`, `parseErrors`
- Error:
  - `401`, `403`, `404`

### 4.3 `apidoc.manual.endpoints.list`
- Route: `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
- Permission: `Permission:GetEndpoints`
- Success:
  - `200` -> `EndpointModel[]`
- Response chinh:
  - `id`, `apiSpecId`, `httpMethod`, `path`
  - `operationId`, `summary`, `description`
  - `tags` (JSON string), `isDeprecated`
- Error:
  - `401`, `403`, `404`

### 4.4 `apidoc.manual.endpoints.getById`
- Route: `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
- Permission: `Permission:GetEndpoints`
- Success:
  - `200` -> `EndpointDetailModel`
- Response chinh:
  - base `EndpointModel`
  - `resolvedUrl`
  - `parameters[]`
  - `responses[]`
  - `securityRequirements[]`
- FE note:
  - `resolvedUrl` chi co gia tri khi path params co `defaultValue` hoac `examples` resolve duoc.
- Error:
  - `401`, `403`, `404`

### 4.5 `apidoc.manual.endpoints.create`
- Route: `POST /api/projects/{projectId}/specifications/{specId}/endpoints`
- Permission: `Permission:AddEndpoint`
- Body:
  - `httpMethod` required
  - `path` required, max `500`
  - `operationId?`, `summary?`, `description?`
  - `tags?: string[]`
  - `isDeprecated?`
  - `parameters?`, `responses?`
- Success:
  - `201` -> `EndpointDetailModel`
- Error:
  - `400`, `401`, `403`, `404`

### 4.6 `apidoc.manual.endpoints.update`
- Route: `PUT /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
- Permission: `Permission:UpdateEndpoint`
- Body:
  - cung shape voi create endpoint
- Success:
  - `200` -> `EndpointDetailModel`
- Error:
  - `400`, `401`, `403`, `404`
- FE note:
  - Backend xoa `parameters` va `responses` cu roi tao lai tu payload moi.

### 4.7 `apidoc.manual.endpoints.delete`
- Route: `DELETE /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
- Permission: `Permission:DeleteEndpoint`
- Success:
  - `204 No Content`
- Error:
  - `401`, `403`, `404`

## 5) Important FE Notes
- FE-11 can project co san; project CRUD nam o FE-02, khong nam trong package nay.
- Neu FE muon chi hien manual specs, nen filter `sourceType = Manual`.
- `tags` request/response dang lech shape:
  - request: `string[]`
  - response: `string` chua JSON array
- Manual body hien tai la free-form metadata qua `parameters` + `schema/examples`, nen FE co the can metadata bo sung o client de phan biet JSON/form-data/x-www-form-urlencoded.

## 6) Open Questions
- FE co nen parse `tags` response tu JSON string sang `string[]` o client khong?
- Co can bo sung metadata `contentType` cho manual request body o UI layer khong?
- Neu can API `resolved-url` va `path-param-mutations`, nen theo tai lieu FE-12 rieng.

## 7) Files Output
- `docs/api-reference/api-requirements.json`
- `docs/api-reference/FE11-API-REQUIREMENTS.md`
