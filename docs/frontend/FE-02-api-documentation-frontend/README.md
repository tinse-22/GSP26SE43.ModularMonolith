# FE-02 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation hien tai cua:

- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, va luu y runtime de tich hop nhanh
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-02-api-documentation`

## 1. Pham vi FE-02

Feature nay gom 2 nhom API chinh:

- Quan ly `Project`
- Quan ly `ApiSpecification`

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Moi action con bi rang buoc boi permission policy o backend.
- Khi FE gap `401/403`, can kiem tra token va permission duoc cap.

## 3. Files trong thu muc nay

- `projects-api.json`: contract cho `ProjectsController`
- `specifications-api.json`: contract cho `SpecificationsController`

## 4. Nhung diem FE de noi sai

1. `POST /api/projects` va `PUT /api/projects/{id}` thuc te tra `ProjectDetailModel`, khong chi `ProjectModel`.
2. `POST /api/projects/{projectId}/specifications/upload` tra `SpecificationDetailModel`.
3. `PUT activate/deactivate specification` cung tra `SpecificationDetailModel`.
4. `DELETE /api/projects/{projectId}/specifications/{specId}` tra `204 No Content`.
5. Upload spec hien bat buoc co `uploadMethod`; backend hien chi ho tro `StorageGatewayContract`.
6. Upload `OpenAPI JSON` co the parse inline ngay trong request, nen `parseStatus` co the la `Success`, `Failed`, hoac `Pending`.
7. `SpecificationDetailModel.originalFileName` co property trong model nhung backend hien chua map gia tri; FE nen coi nhu optional/null.

## 5. Filter, param, sort hien tai

- `GET /api/projects`
  - co filter: `status`, `search`
  - co pagination: `page`, `pageSize`
  - khong co sort param
  - backend sort co dinh: `CreatedDateTime desc`
- `GET /api/projects/{projectId}/specifications`
  - co filter: `parseStatus`, `sourceType`
  - khong co pagination
  - khong co sort param
  - backend sort co dinh: `CreatedDateTime desc`
- `GET /api/projects/{id}/auditlogs`
  - khong co filter param
  - khong co pagination
  - backend tra ve da sort `CreatedDateTime desc`
- Cac endpoint detail/create/update/delete chi dung path params va body/form-data, khong co query sort/filter rieng.

## 6. Khuyen nghi su dung

- Dung thu muc nay lam handoff chinh cho FE.
- Neu co moi truong chay that, doi chieu them Swagger/Scalar cua `ClassifiedAds.WebAPI` de check route runtime.
