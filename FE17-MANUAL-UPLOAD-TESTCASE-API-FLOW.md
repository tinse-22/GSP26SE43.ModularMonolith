# FE-17: API Flow Upload API Thu Cong Va Tao Test Case

Ngay cap nhat: 2026-04-05
Muc tieu: Tai lieu nay gom full flow de FE goi API tu luc tao project, upload API spec thu cong, den generate va lay danh sach test case.

## 1) Dieu kien chung

- Tat ca endpoint ben duoi (tru callback noi bo) deu can JWT Bearer token.
- Header co ban:
  - Authorization: Bearer <access_token>
  - Content-Type: application/json (tru endpoint upload file dung multipart/form-data)

## 2) Flow tong quan (happy-path)

1. Tao project.
2. Upload API spec thu cong (chon 1 trong 3 cach):
   - Upload file OpenAPI/Postman.
   - Nhap manual endpoint definitions.
   - Import 1 lenh cURL.
3. Lay chi tiet spec va cho parse xong (neu can).
4. Lay danh sach endpoints cua spec.
5. Tao test suite va chon endpoint scope.
6. Tao test order proposal.
7. Approve proposal.
8. Generate happy-path test cases.
9. Lay danh sach test cases.

## 3) API chi tiet theo tung buoc

## Buoc 1 - Tao project

POST /api/projects

Request body:
```json
{
  "name": "User Service API",
  "description": "Spec cho module user",
  "baseUrl": "https://api.example.com"
}
```

Response 201 (rut gon):
```json
{
  "id": "project-guid",
  "name": "User Service API",
  "activeSpecId": null
}
```

## Buoc 2 - Upload API spec thu cong

Chon 1 trong 3 endpoint sau.

### 2A) Upload file

POST /api/projects/{projectId}/specifications/upload
Content-Type: multipart/form-data

Form fields:
- uploadMethod: StorageGatewayContract (bat buoc)
- file: <file .json/.yaml/.yml, <= 10MB>
- name: ten specification (bat buoc)
- sourceType: OpenAPI hoac Postman
- version: optional
- autoActivate: true/false

Response 201:
```json
{
  "id": "spec-guid",
  "projectId": "project-guid",
  "name": "User API v1",
  "sourceType": "OpenAPI",
  "parseStatus": "Pending",
  "isActive": false
}
```

Luu y parse:
- Neu upload OpenAPI JSON, backend co the parse ngay va tra parseStatus=Success.
- Voi mot so truong hop khac (yaml/postman), parse co the Pending va xu ly bat dong bo.

### 2B) Manual endpoint definitions

POST /api/projects/{projectId}/specifications/manual

Request body mau:
```json
{
  "name": "Manual User API",
  "version": "1.0.0",
  "autoActivate": true,
  "endpoints": [
    {
      "httpMethod": "GET",
      "path": "/users/{id}",
      "operationId": "getUserById",
      "summary": "Get user by id",
      "description": "Lay thong tin user",
      "tags": ["Users"],
      "isDeprecated": false,
      "parameters": [
        {
          "name": "id",
          "location": "Path",
          "dataType": "String",
          "isRequired": true
        }
      ],
      "responses": [
        {
          "statusCode": 200,
          "description": "OK",
          "schema": "{ \"type\": \"object\" }"
        }
      ]
    }
  ]
}
```

Response 201: spec detail. Thuong parseStatus=Success ngay vi du lieu da duoc nhap truc tiep.

### 2C) Import cURL

POST /api/projects/{projectId}/specifications/curl-import

Request body:
```json
{
  "name": "Curl Imported API",
  "version": "1.0.0",
  "curlCommand": "curl -X GET 'https://api.example.com/users/1' -H 'Authorization: Bearer xxx'",
  "autoActivate": false
}
```

Response 201: spec detail. Thuong parseStatus=Success ngay.

## Buoc 3 - Kiem tra trang thai parse

GET /api/projects/{projectId}/specifications/{specId}

Response fields can dung:
- id
- parseStatus: Pending | Success | Failed
- parseErrors: danh sach loi parse (neu Failed)
- endpointCount

Khuyen nghi FE:
- Neu parseStatus=Pending: poll moi 2-3 giay.
- Timeout UI sau 60-90 giay va cho phep user refresh tay.

## Buoc 4 - Lay endpoints cua spec

GET /api/projects/{projectId}/specifications/{specId}/endpoints

Response 200 (rut gon):
```json
[
  {
    "id": "endpoint-guid-1",
    "httpMethod": "GET",
    "path": "/users/{id}",
    "summary": "Get user by id"
  }
]
```

## Buoc 5 - Tao test suite

POST /api/projects/{projectId}/test-suites

Request body:
```json
{
  "name": "Smoke Suite",
  "description": "Happy path cho User API",
  "apiSpecId": "spec-guid",
  "generationType": "Auto",
  "selectedEndpointIds": ["endpoint-guid-1"],
  "endpointBusinessContexts": {
    "endpoint-guid-1": "Chi tra ve user active"
  },
  "globalBusinessRules": "Tat ca API can auth"
}
```

Response 201 (rut gon):
```json
{
  "id": "suite-guid",
  "projectId": "project-guid",
  "apiSpecId": "spec-guid",
  "rowVersion": "base64-rowversion"
}
```

## Buoc 6 - Tao order proposal

POST /api/test-suites/{suiteId}/order-proposals

Request body:
```json
{
  "specificationId": "spec-guid",
  "selectedEndpointIds": ["endpoint-guid-1"],
  "source": "Ai",
  "llmModel": "gpt-4.1",
  "reasoningNote": "Uu tien auth truoc"
}
```

Response 201 (rut gon):
```json
{
  "proposalId": "proposal-guid",
  "status": "Pending",
  "proposedOrder": [
    {
      "endpointId": "endpoint-guid-1",
      "orderIndex": 1
    }
  ],
  "rowVersion": "base64-rowversion"
}
```

## Buoc 7 - Approve proposal

POST /api/test-suites/{suiteId}/order-proposals/{proposalId}/approve

Request body:
```json
{
  "rowVersion": "base64-rowversion",
  "reviewNotes": "Approved by FE"
}
```

Response 200: proposal sau approve (status thuong la Approved hoac ModifiedAndApproved).

Meo cho FE:
- Neu khong chac rowVersion moi nhat, goi GET /api/test-suites/{suiteId}/order-proposals/latest truoc.

## Buoc 8 - Generate happy-path test cases

POST /api/test-suites/{suiteId}/test-cases/generate-happy-path

Request body:
```json
{
  "specificationId": "spec-guid",
  "forceRegenerate": false
}
```

Response 201 (rut gon):
```json
{
  "testSuiteId": "suite-guid",
  "totalGenerated": 12,
  "endpointsCovered": 5,
  "generatedAt": "2026-04-05T10:20:30Z",
  "testCases": [
    {
      "testCaseId": "case-guid",
      "endpointId": "endpoint-guid-1",
      "name": "GET /users/{id} - happy path"
    }
  ]
}
```

## Buoc 9 - Lay danh sach test cases

GET /api/test-suites/{suiteId}/test-cases?testType=HappyPath&includeDisabled=false

Response 200:
```json
[
  {
    "id": "case-guid",
    "name": "GET /users/{id} - happy path",
    "testType": "HappyPath",
    "priority": "High",
    "isEnabled": true,
    "request": {
      "httpMethod": "GET",
      "url": "/users/{id}"
    },
    "expectation": {
      "expectedStatus": "200"
    }
  }
]
```

## 4) API bo sung (tuy chon)

- Generate boundary/negative:
  - POST /api/test-suites/{suiteId}/test-cases/generate-boundary-negative
  - Body: specificationId, forceRegenerate, includePathMutations, includeBodyMutations, includeLlmSuggestions.
- Tao test case thu cong:
  - POST /api/test-suites/{suiteId}/test-cases
- Trigger flow async n8n cu:
  - POST /api/test-suites/{suiteId}/generate-tests (tra 202 Accepted)
  - Callback /api/test-suites/{suiteId}/test-cases/from-ai la endpoint noi bo cho n8n, FE khong goi truc tiep.

## 5) Loi thuong gap can xu ly UI

- Upload file:
  - "File la bat buoc."
  - "Kich thuoc file khong duoc vuot qua 10MB."
  - "Chi ho tro file .json, .yaml, .yml."
  - "Loai nguon phai la OpenAPI hoac Postman."
- Manual spec:
  - "Danh sach endpoint la bat buoc. Vui long them it nhat mot endpoint."
  - "HTTP method khong hop le..."
- Order/Test case:
  - "Can approve test order truoc khi generate test cases."
  - "Du lieu proposal da thay doi..." (concurrency, can reload rowVersion).

## 6) Contract enums quan trong

- sourceType (upload file): OpenAPI | Postman
- specification sourceType trong he thong: OpenAPI | Postman | Manual | cURL
- parseStatus: Pending | Success | Failed
- generationType: Auto | Manual | LLMAssisted
- proposal source: Ai | User | System | Imported

## 7) Checklist FE implementation

- Tao project thanh cong va luu projectId.
- Upload spec thu cong (chon 1 cach) va luu specId.
- Poll parseStatus den khi Success.
- Lay endpoints de user chon scope.
- Tao suite + propose + approve.
- Goi generate-happy-path.
- Reload danh sach test-cases de hien thi ket qua.
