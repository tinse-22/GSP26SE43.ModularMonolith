# FE-12 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-27

Thu muc nay duoc viet rieng cho Frontend de noi FE-12 theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.WebAPI`

Muc tieu:

- chot dung route, request, response, va runtime behavior lien quan den path-parameter templating
- cover du cac entry point FE-12 dang dung that: `manual`, `curl-import`, `create/update endpoint`, `endpoint detail`, `resolved-url`, va `path-param-mutations`
- tranh de FE phai tu rap logic tu planning docs cu trong `docs/features/FE-12-path-parameter-templating`

## 1. Pham vi FE-12 runtime

FE-12 khong phai mot controller rieng. Runtime hien tai trai tren 2 nhom route:

- `SpecificationsController`
  - `POST /api/projects/{projectId}/specifications/manual`
  - `POST /api/projects/{projectId}/specifications/curl-import`
- `EndpointsController`
  - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
  - `POST /api/projects/{projectId}/specifications/{specId}/endpoints`
  - `PUT /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
  - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/resolved-url`
  - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}/path-param-mutations`

## 2. Files trong thu muc nay

- `path-parameter-templating-api.json`: contract FE-12 gom model, route, runtime rule, sample request/response, va gotcha can luu y

## 3. Auth va permission

- Tat ca endpoint trong handoff nay deu yeu cau Bearer token.
- Permission runtime:
  - `POST /manual`, `POST /curl-import`: `Permission:AddSpecification`
  - `GET endpoint detail`, `GET resolved-url`, `GET path-param-mutations`: `Permission:GetEndpoints`
  - `POST endpoint`: `Permission:AddEndpoint`
  - `PUT endpoint`: `Permission:UpdateEndpoint`

## 4. Flow FE nen bam

1. Neu user tao specification bang `manual` hoac `curl-import`, goi route create truoc.
2. Sau `201 Created`, neu FE can hien path params auto-detect hay `resolvedUrl`, goi tiep:
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints`
   - `GET /api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}`
3. Neu user can preview URL voi gia tri thay the tam thoi, goi:
   - `GET .../resolved-url?paramA=valueA&paramB=valueB`
4. Neu FE can render danh sach negative/boundary variants cho path params, goi:
   - `GET .../path-param-mutations`

## 5. Nhung diem FE de noi sai

1. `POST /manual` va `POST /curl-import` chi tra `SpecificationDetailModel`, khong tra nested `EndpointDetailModel`.
2. `POST /endpoints` va `PUT /endpoints/{endpointId}` moi tra lai `EndpointDetailModel` co `parameters` va `resolvedUrl`.
3. Backend tu dong tao path params con thieu tu placeholder trong `path`. FE khong bat buoc phai gui day du `parameters` neu chi moi co template.
4. Path param da ton tai trong request se duoc giu lai `dataType`, `format`, `defaultValue`, `examples`; chi auto-create nhung placeholder con thieu.
5. Moi path param deu bi ep `isRequired = true`, ke ca khi FE gui `false`.
6. Placeholder name chi chap nhan regex `^\\w+$`. Ten co dau gach ngang nhu `{user-id}` se bi `400`.
7. Placeholder trung lap bi reject case-insensitive. `{Id}` va `{id}` cung bi xem la trung.
8. Moi endpoint chi ho tro toi da 10 path params.
9. `dataType` trong request la string enum lowercase: `string | integer | number | boolean | object | array | uuid`. Numeric enum khong duoc chap nhan.
10. `location` la string tu do, backend map case-insensitive. Gia tri la, thieu, hoac khong map duoc se bi normalize thanh `Query`.
11. `EndpointDetailModel.tags` la chuoi JSON array, khong phai `string[]`.
12. `ParameterModel.examples` cung la chuoi JSON, khong phai mang string typed.
13. `EndpointDetailModel.parameters` duoc sort theo `name asc`, khong theo thu tu xuat hien trong path.
14. `GetEndpointDetail.resolvedUrl` chi fallback `DefaultValue` roi toi `Examples[0]`. Neu `Examples[0]` rong hoac JSON loi, `resolvedUrl` co the la `null`.
15. `GET /resolved-url` co logic fallback khac: `explicit query value > DefaultValue > first non-empty example`.
16. `GET /resolved-url` doc query string key/value raw, match ten param theo `case-insensitive`, bo qua key du thua, va URI-encode value trong `resolvedUrl`.
17. Query value rong trong `GET /resolved-url` khong override fallback. Neu FE gui `?userId=` va DB co `DefaultValue=42` thi backend van dung `42`.
18. `resolvedParameters` tra gia tri raw chua encode; `resolvedUrl` moi la chuoi da encode.
19. `GET /path-param-mutations` chi dung `DefaultValue` cua cac sibling path params khi build `resolvedUrl`; route nay khong fallback sang `Examples`.
20. Vi ly do tren, mutation `resolvedUrl` co the la `null` du endpoint detail van co `resolvedUrl`, neu sibling params chi co `Examples` ma khong co `DefaultValue`.
21. `GET /path-param-mutations` tra mutation list dong theo `dataType/format`; FE khong nen hardcode mot danh sach mutation co dinh.
22. `empty` mutation thuong co `resolvedUrl = null` vi current param duoc thay bang chuoi rong.
23. Khong co preview API cho endpoint draft chua luu. `resolved-url` va `path-param-mutations` deu can `endpointId` da ton tai.
24. `PUT /endpoints/{endpointId}` xoa va tao lai toan bo `parameters` va `responses`; FE phai gui full authoritative payload.
25. `Import cURL` tu detect path params nhung tao mac dinh `dataType = "string"`, `isRequired = true`, va khong co `defaultValue/examples`. Vi vay `resolvedUrl` sau import thuong la `null` cho den khi FE/user bo sung sample values.
26. Validation error runtime duoc map thanh `400 application/problem+json` co `message` va `traceId`.
27. `CreateManualSpecificationCommand` prefix validation theo vi tri endpoint: vd `Endpoint #2: Path parameter 'wrongName' khong ton tai trong path ...`

## 6. Khuyen nghi su dung

- Neu FE muon render thu tu path params theo thu tu xuat hien trong URL, hay parse `path` o client thay vi tin vao thu tu `parameters`.
- Dung `GET endpoint detail` de lay snapshot luu tru, va dung `GET resolved-url` khi user nhap override values trong UI.
- Parse `tags` va `examples` bang `JSON.parse` co try/catch; backend co the luu `null`, chuoi rong, hoac JSON loi trong du lieu cu.
- Khi render mutations, coi `mutationType`, `label`, `description`, `expectedStatusCode`, `resolvedUrl` la du lieu runtime; khong can tu sinh lai o FE.
- Neu UI cho phep cURL import roi preview ngay, hay thong bao ro rang rang user can bo sung sample values cho path params sau import de co `resolvedUrl`.

## 7. Lien ket voi handoff khac

- `docs/frontend/FE-02-api-documentation-frontend` van la handoff tong quan cho `SpecificationsController`
- `docs/frontend/FE-03-json-parsing-endpoints-frontend` van la handoff tong quan cho `EndpointsController`
- Thu muc FE-12 nay la handoff tap trung rieng cho path-parameter templating va uu tien runtime rules cua FE-12
