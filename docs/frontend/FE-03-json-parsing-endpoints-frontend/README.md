# FE-03 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation hien tai cua:

- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-03-json-parsing-endpoints`

## 1. Pham vi FE-03

Feature nay gom 2 nhom API chinh:

- Quan ly `Specification`
- Quan ly `Endpoint`

Sub-feature `FE-03-03` hien moi o muc design. FE hien khong co endpoint rieng cho async parser flow, nen neu can doi `parseStatus` thi FE chi can goi lai:

- `GET /api/projects/{projectId}/specifications`
- `GET /api/projects/{projectId}/specifications/{specId}`

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Moi action con bi rang buoc boi permission policy o backend.
- Khi FE gap `401/403`, can kiem tra token va permission duoc cap.

## 3. Files trong thu muc nay

- `specifications-api.json`: contract cho `SpecificationsController`
- `endpoints-api.json`: contract cho `EndpointsController`

Mock support lien quan cho FE-03 duoc dat tai:

- `docs/frontend/FE-03-mock-api-prompt.md`
- `docs/frontend/FE-03-mock-data-template.json`

## 4. Nhung diem FE de noi sai

1. `GET /api/projects/{projectId}/specifications` tra `SpecificationModel[]`, khong phai `PaginatedResult`.
2. `GET /api/projects/{projectId}/specifications/{specId}/endpoints` tra `EndpointModel[]`, khong phai `PaginatedResult`.
3. `POST /upload`, `POST /manual`, va `POST /curl-import` deu tra `201 Created` kem body detail, khong phai `200 OK`.
4. `PUT /activate` va `PUT /deactivate` tra `200 OK` kem body detail, khong phai `204 No Content`.
5. `GET /upload-methods` chi tra `method` va `uploadApi`; backend hien khong tra `sourceType`, `name`, `description`, hay `allowedExtensions`.
6. Upload hien chi ho tro `uploadMethod = StorageGatewayContract`.
7. Upload file chi nhan `sourceType = OpenAPI | Postman`; `Manual` va `cURL` dung endpoint rieng.
8. Chi `OpenAPI + .json` moi duoc parse inline ngay khi upload. `OpenAPI + .yaml/.yml` va `Postman + .json` se giu `ParseStatus = Pending`.
9. `OpenAPI + .json` co the tra `ParseStatus = Success` hoac `Failed` ngay trong response upload.
10. `autoActivate = true` khong cho parse thanh cong moi kich hoat. Mot specification `Pending` hoac `Failed` van co the bi set active theo implementation hien tai.
11. `tags` trong `EndpointModel` va `EndpointDetailModel` la chuoi JSON array, khong phai mang string.
12. `CreateManualSpecificationModel` va `CreateUpdateEndpointModel` hien khong co field `securityRequirements`.
13. `EndpointDetailModel.resolvedUrl` co the la `null` neu backend khong resolve du tat ca path params tu `DefaultValue` hoac `Examples`.
14. `GET /path-param-mutations` tra danh sach mutation dong; loai mutation thay doi theo `dataType` va `format`.
15. `SpecificationDetailModel.originalFileName` co property trong model nhung query handler hien chua map gia tri; FE nen coi nhu optional/null.
16. `ValidationException` duoc WebAPI map thanh `400 application/problem+json`, khong phai `403`.

## 5. Filter, param, sort hien tai

- `GET /api/projects/{projectId}/specifications`
  - co filter: `parseStatus`, `sourceType`
  - khong co pagination
  - khong co sort param
  - backend sort co dinh: `CreatedDateTime desc`
- `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
  - khong co filter param
  - khong co pagination
  - khong co sort param
  - backend sort co dinh: `CreatedDateTime desc`
- `GET /resolved-url`
  - nhan dynamic query string key/value
  - explicit value trong query string uu tien hon `DefaultValue`, sau do moi fallback sang `Examples[0]`
- `GET /path-param-mutations`
  - khong co query param
  - neu endpoint khong co path params thi backend tra `totalMutations = 0`

## 6. Khuyen nghi su dung

- Dung thu muc nay lam handoff chinh cho FE-03.
- Khi can mock UI, co the dung them file `docs/frontend/FE-03-mock-data-template.json`.
- Neu co moi truong runtime that, doi chieu them Swagger/Scalar cua `ClassifiedAds.WebAPI` de check route runtime.
