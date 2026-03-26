# FE-03 JSON Parsing Endpoints

Cap nhat lan cuoi: 2026-03-25

Thu muc nay la bo docs backend planning + implementation mapping cho feature `FE-03`.

## 1. Muc tieu

`FE-03` gom 3 phan:

- `FE-03-01`: Specification Management
- `FE-03-02`: Endpoint Management
- `FE-03-03`: Parser Flow (design-only)

Trang thai hien tai theo codebase:

- `FE-03-01`: da implement
- `FE-03-02`: da implement
- `FE-03-03`: chua co source code runtime, chi moi co design

## 2. Files chinh trong thu muc nay

- `requirement.json`: tong hop muc tieu, scope, dependency, acceptance criteria
- `workflow.json`: workflow cap feature
- `implementation-map.json`: map planning vao file C# hien tai
- `FE-03-01/requirement.json`: detail requirement cho specification management
- `FE-03-01/contracts.json`: contracts cho `SpecificationsController`
- `FE-03-02/requirement.json`: detail requirement cho endpoint management
- `FE-03-02/contracts.json`: contracts cho `EndpointsController`
- `FE-03-03/requirement.json`: design requirement cho parser flow async

## 3. Handoff cho Frontend

Bo handoff rieng cho FE duoc dat tai:

- `docs/frontend/FE-03-json-parsing-endpoints-frontend/README.md`
- `docs/frontend/FE-03-json-parsing-endpoints-frontend/specifications-api.json`
- `docs/frontend/FE-03-json-parsing-endpoints-frontend/endpoints-api.json`

Mock support cho FE-03 duoc dat tai:

- `docs/frontend/FE-03-mock-api-prompt.md`
- `docs/frontend/FE-03-mock-data-template.json`

## 4. Luu y quan trong

1. Upload specification hien chi parse inline voi `OpenAPI + .json`.
2. `OpenAPI + .yaml/.yml` va `Postman + .json` se o `ParseStatus = Pending` sau upload.
3. `GET /specifications` va `GET /endpoints` hien tra plain array, khong co pagination.
4. `tags` trong response endpoint la chuoi JSON, khong phai mang string.
5. FE-03-03 hien chua co endpoint rieng cho parser job; FE muon cap nhat trang thai thi goi lai list/detail cua specification.
