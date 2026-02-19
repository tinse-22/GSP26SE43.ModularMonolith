# FE-04 Runbook Van Hanh (Test Scope & Execution Configuration)

Cap nhat lan cuoi: 2026-02-19

## 1) Muc tieu FE-04

FE-04 cung cap API cau hinh bat buoc truoc khi sinh/chay test:

- FE-04-01: cau hinh scope test suite trong `TestGeneration`
- FE-04-02: cau hinh execution environment trong `TestExecution`

Day la gate bat buoc truoc FE-05A/FE-05B va FE-07.

## 2) Pham vi da implement

### FE-04-01 (Scope)

- Route goc: `/api/projects/{projectId}/test-suites`
- API:
  - `GET /`
  - `GET /{suiteId}`
  - `POST /`
  - `PUT /{suiteId}`
  - `DELETE /{suiteId}?rowVersion=...` (soft archive)
- Rule chinh:
  - `selectedEndpointIds` phai thuoc `apiSpecId` da chon
  - write operation yeu cau owner cua suite
  - update/delete bat buoc `rowVersion` (base64)
  - suite da `Archived` khong duoc update
  - FE-05A fallback dung `TestSuite.SelectedEndpointIds` neu request khong gui danh sach endpoint

### FE-04-02 (Execution Environment)

- Route goc: `/api/projects/{projectId}/execution-environments`
- API:
  - `GET /`
  - `GET /{environmentId}`
  - `POST /`
  - `PUT /{environmentId}`
  - `DELETE /{environmentId}?rowVersion=...`
- Rule chinh:
  - validate `Name`, `BaseUrl`, `Headers`, `Variables`, `AuthConfig`
  - update/delete bat buoc `rowVersion` (base64)
  - secret trong `AuthConfig` luon bi mask khi response
  - thao tac dat `IsDefault=true` duoc chay transaction voi `IsolationLevel.Serializable`
  - chan >1 default environment/project (`DEFAULT_ENVIRONMENT_CONFLICT`)

## 3) Security va Authorization

- Tat ca endpoint FE-04 deu co `[Authorize]`.
- Policy permission theo action:
  - `Permission:GetTestSuites`
  - `Permission:AddTestSuite`
  - `Permission:UpdateTestSuite`
  - `Permission:DeleteTestSuite`
  - `Permission:GetExecutionEnvironments`
  - `Permission:AddExecutionEnvironment`
  - `Permission:UpdateExecutionEnvironment`
  - `Permission:DeleteExecutionEnvironment`

## 4) Quy trinh van hanh de xuat

1. Tao scope (`POST /test-suites`) voi `apiSpecId` + `selectedEndpointIds`.
2. Doc suite (`GET`) de lay `rowVersion` hien tai.
3. Cap nhat scope (`PUT`) bang `rowVersion` moi nhat.
4. Archive scope (`DELETE`) bang `rowVersion` moi nhat.
5. Tao execution environment (`POST`) voi `baseUrl`, tuy chon `headers/variables/authConfig`.
6. Dat duy nhat 1 default environment/project (`isDefault=true`).
7. Neu can, cap nhat environment (`PUT`) voi `rowVersion` hien tai.
8. Xoa environment (`DELETE`) voi `rowVersion` hien tai.

## 5) Playbook xu ly loi

- `400 ValidationException`:
  - `apiSpecId` sai, endpoint scope sai
  - `baseUrl` sai, `authConfig` sai, key header/variable rong
  - `rowVersion` thieu/sai
- `404 NotFoundException`:
  - suite/environment khong ton tai trong project context
- `409 ConflictException`:
  - `CONCURRENCY_CONFLICT`: stale `rowVersion`
  - `DEFAULT_ENVIRONMENT_CONFLICT`: xung dot khi dam bao default unique

## 6) Checklist xac minh

- FE-04-01:
  - create/update/list/get/archive hoat dong dung
  - validate endpoint subset qua `IApiEndpointMetadataService` dung
  - suite da archive khong update duoc
  - FE-05A fallback dung persisted scope khi request endpoint list rong
- FE-04-02:
  - create/update/list/get/delete hoat dong dung
  - secret auth duoc mask trong response
  - chi con 1 default environment/project sau transaction thanh cong
  - stale `rowVersion` tra `409`

## 7) Lenh test

```powershell
dotnet test "ClassifiedAds.UnitTests/ClassifiedAds.UnitTests.csproj" --filter "FullyQualifiedName~ClassifiedAds.UnitTests.TestGeneration|FullyQualifiedName~ClassifiedAds.UnitTests.TestExecution"
```

Ket qua hien tai (2026-02-19): Passed `61/61` test FE-04 related da duoc filter.
