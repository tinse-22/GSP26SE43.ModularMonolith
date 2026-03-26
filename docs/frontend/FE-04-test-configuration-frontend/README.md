# FE-04 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-25

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Modules.TestExecution`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-04-test-configuration`
- luu y: `swagger.json` check-in o repo root hien chua phan anh day du FE-04, nen handoff nay duoc doi chieu truc tiep tu controller, command, model, va runtime service hien co

## 1. Pham vi FE-04

Feature nay gom 2 nhom API chinh:

- Quan ly `Test suite scope` qua `/api/projects/{projectId}/test-suites`
- Quan ly `Execution environment` qua `/api/projects/{projectId}/execution-environments`

FE-04 hien dong vai tro handoff cho:

- FE-05A: de order proposal co persisted scope fallback
- FE-07: de test run resolve `baseUrl`, auth, headers, query params, va variables

## 2. Auth

- Tat ca endpoint trong thu muc nay deu yeu cau Bearer token.
- Moi action con bi rang buoc boi permission policy o backend.
- `TestSuite` update/delete con co owner check trong handler.
- `ExecutionEnvironment` handler hien chi rang buoc auth + permission, khong co owner check rieng theo row data.

## 3. Files trong thu muc nay

- `test-suites-api.json`: contract cho `TestSuitesController`
- `execution-environments-api.json`: contract cho `ExecutionEnvironmentsController`

## 4. Nhung diem FE de noi sai

1. FE-04 runtime hien tai khong ho tro scope kieu "toan project" hoac "toan specification khong can chon endpoint". Create/update bat buoc co `apiSpecId` va `selectedEndpointIds` voi it nhat 1 endpoint.
2. Backend normalize `selectedEndpointIds`: bo `Guid.Empty`, loai duplicate, va sort tang dan. Thu tu FE gui len khong duoc giu nguyen.
3. `endpointBusinessContexts` khong bi reject neu co key ngoai scope; backend silently drop key khong thuoc `selectedEndpointIds` va bo gia tri rong/whitespace.
4. `generationType` o request `test-suites` nhan ca string enum (`Auto`, `Manual`, `LLMAssisted`) lan integer (`0`, `1`, `2`), nhung response `generationType`, `status`, va `approvalStatus` hien ra dang so, khong phai string.
5. `testCaseCount` chi duoc tinh dung tren `GET /api/projects/{projectId}/test-suites`. `GET by id`, `POST`, va `PUT` hien tra `testCaseCount = 0`.
6. `DELETE /api/projects/{projectId}/test-suites/{suiteId}` la soft archive. List se an suite archived, nhung `GET /api/projects/{projectId}/test-suites/{suiteId}` van co the tra suite archived voi `status = 2`.
7. `GET /api/projects/{projectId}/test-suites` va `GET /api/projects/{projectId}/execution-environments` hien khong co project lookup rieng; neu khong co record phu hop thi backend tra `200` voi mang rong, khong phai `404`.
8. Ca `DELETE test-suites` va `DELETE execution-environments` deu nhan `rowVersion` qua query string, khong nhan trong body.
9. `ExecutionEnvironment` chi enforce toi da 1 default, khong enforce phai luon co 1 default. Update/delete co the de project khong con default nao.
10. Tat ca response `ExecutionEnvironment` deu mask `token`, `password`, `apiKeyValue`, va `clientSecret` thanh `******`.
11. FE khong duoc round-trip gia tri mask `******` khi update environment. Backend se luu literal `******` neu FE gui nguoc lai; neu user khong thay secret, FE phai yeu cau nhap lai hoac giu secret that o state rieng truoc khi submit.
12. `authType` va `apiKeyLocation` trong `authConfig` la string enum ca request lan response. Diem nay khac voi enum response cua `TestSuite`.
13. List environment sort `IsDefault desc`, roi toi `Name asc`. List test suite sort co dinh `CreatedDateTime desc`.
14. FE-05A co fallback scope runtime: neu `POST /api/test-suites/{suiteId}/order-proposals` gui `selectedEndpointIds = []`, backend se dung `TestSuite.SelectedEndpointIds` da luu.
15. FE-07 runtime dung `ExecutionEnvironment` de resolve `baseUrl`, auth, headers, query params, va `{{variables}}`. `baseUrl` se bi trim dau `/` cuoi, env defaults co the bi request-level data override trong luc chay test.

## 5. Filter, param, sort hien tai

- `GET /api/projects/{projectId}/test-suites`
  - khong co filter param
  - khong co pagination
  - khong co sort param
  - backend sort co dinh: `CreatedDateTime desc`
- `GET /api/projects/{projectId}/execution-environments`
  - khong co filter param
  - khong co pagination
  - khong co sort param
  - backend sort co dinh: `IsDefault desc`, sau do `Name asc`
- `DELETE /api/projects/{projectId}/test-suites/{suiteId}`
  - bat buoc query `rowVersion`
- `DELETE /api/projects/{projectId}/execution-environments/{environmentId}`
  - bat buoc query `rowVersion`

## 6. Runtime handoff cho feature lien quan

- FE-05A:
  - `POST /api/test-suites/{suiteId}/order-proposals` co the bo trong `selectedEndpointIds` de backend fallback ve scope da persist trong suite
  - `specificationId` van bat buoc va phai khop voi `apiSpecId` da luu tren suite
- FE-07:
  - `BearerToken` dung `headerName` neu co, neu khong mac dinh la `Authorization`
  - `Basic` inject `Authorization: Basic base64(username:password)` neu request-level header chua ghi de
  - `ApiKey` co the inject vao header hoac query tuy `apiKeyLocation`
  - `OAuth2ClientCredentials` se goi `tokenUrl` runtime de lay `access_token`; neu khong lay duoc token thi khong co header auth nao duoc inject
  - Env `variables` duoc resolve theo syntax `{{variableName}}`
  - Runtime uu tien bien duoc extract trong test run cao hon env variables
  - Request-level headers va query params co the override env defaults khi chay test

## 7. Khuyen nghi su dung

- Dung thu muc nay lam handoff chinh cho FE-04.
- UI edit `ExecutionEnvironment` nen dung cac field secret theo kieu write-only, khong refill tu response mask.
- UI `TestSuite` nen parse enum response dang so thay vi string.
- Neu can doi chieu runtime that, uu tien doc controller/handler hien tai hoac Swagger/Scalar dang chay tu host, khong dung file `swagger.json` check-in lam nguon su that cho FE-04.
