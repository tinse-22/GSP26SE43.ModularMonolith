# FE-05A - API Test Order Proposal + User Verify/Reorder Gate

Tai lieu nay la ban huong dan de doc nhanh bo JSON cho FE-05A.

## 1) Muc tieu

FE-05A la mandatory gate truoc FE-05B:

1. He thong de xuat thu tu API/endpoint can test.
2. User review va co the reorder.
3. User approve (hoac reject).
4. He thong luu order da xac nhan.
5. FE-05B chi duoc generate khi gate pass.

## 2) Diem quan trong ve kien truc (bam codebase that)

- Reuse entity san co:
  - `TestOrderProposal`
  - `TestSuite`
- Schema su dung: `testgen`
- Khong tao model gia lap lech codebase neu chua can.
- Khong truy cap truc tiep repository/module cua `ApiDocumentation`; consume qua API/contract boundary.
- Dung optimistic concurrency qua `RowVersion`.

## 3) Bo file JSON can dung

### A. Cap FE-05 tong

- `docs/features/FE-05-test-generation/requirement.json`
  - Requirement tong FE-05 va tach phase FE-05A -> FE-05B.
- `docs/features/FE-05-test-generation/workflow.json`
  - Workflow tong FE-05 (scope -> gate -> generation).
- `docs/features/FE-05-test-generation/FE-05B/requirement.json`
  - Placeholder requirement cho FE-05B de bo spec lien ket day du.

### B. Cap FE-05A chi tiet

- `docs/features/FE-05-test-generation/FE-05A/requirement.json`
  - Scope, dependency, acceptance criteria, implementation order.
- `docs/features/FE-05-test-generation/FE-05A/contracts.json`
  - API contracts, command/query/service contracts, validation va state transition.
- `docs/features/FE-05-test-generation/FE-05A/workflow.json`
  - Luong xu ly chi tiet theo tung buoc propose/reorder/approve/reject/gate-check.

## 4) Rule bat buoc cho Agent khi implement

1. Khong duoc bo qua FE-05A gate.
2. Khong duoc cho FE-05B generate neu chua co approved/applied order.
3. Reorder/approve/reject phai check `RowVersion`.
4. Approve/reject phai cap nhat proposal + suite trong cung transaction.
5. Moi endpoint FE-05A phai co authorize + permission policy.

## 5) Definition gate pass

Gate pass khi:

- ton tai proposal active cua suite, va
- `Status` thuoc `[Approved, ModifiedAndApproved, Applied]`, va
- `AppliedOrder` khac null.

Neu khong dat, FE-05B tra `409` voi reason code `ORDER_CONFIRMATION_REQUIRED`.

## 6) Cach doc nhanh theo muc dich

1. Neu ban can business scope: doc `requirement.json`.
2. Neu ban can API + data contract de code: doc `contracts.json`.
3. Neu ban can sequence implement chi tiet: doc `workflow.json`.

