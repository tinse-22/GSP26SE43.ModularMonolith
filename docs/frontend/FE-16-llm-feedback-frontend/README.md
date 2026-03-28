# FE-16 Frontend API Handoff

Cap nhat lan cuoi: 2026-03-28

Thu muc nay duoc viet lai rieng cho Frontend de noi API theo implementation runtime hien tai cua:

- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.WebAPI`

Muc tieu:

- de FE co route, request, response, status code, va runtime notes de tich hop nhanh cho FE-16
- khong thay the bo docs planning goc cua backend trong `docs/features/FE-16-llm-feedback`
- uu tien controller, command/query handler, feedback upsert service, list/detail hydration, va exception handling dang chay trong codebase hien tai

## 1. Pham vi FE-16

Runtime frontend-facing hien tai cua FE-16 tap trung vao `LlmSuggestionsController`:

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`

Ngoai mutation write o tren, FE-16 con mo rong read model cua FE-15 tren 2 endpoint da co san:

- `GET /api/test-suites/{suiteId}/llm-suggestions`
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`

Hai endpoint read nay hydrate them:

- `currentUserFeedback`
- `feedbackSummary`

Thu muc nay chi cover FE-16 feedback write + read surface lien quan. Khong cover:

- FE-15 preview generation va review mutation
- FE-17 bulk review
- xoa feedback, undo feedback row, hay mot feedback-history endpoint rieng

## 2. Auth

- Tat ca endpoint lien quan deu yeu cau Bearer token.
- `PUT /feedback` can `Permission:UpdateTestCase`.
- 2 endpoint `GET` cua FE-15 can `Permission:GetTestCases`.
- Ngoai permission, owner check van theo `suite.CreatedById == CurrentUserId`.

## 3. Files trong thu muc nay

- `llm-suggestion-feedback-api.json`: contract frontend-facing cho FE-16 tren `LlmSuggestionsController`

## 4. Nhung diem FE de noi sai

1. FE-16 khong co controller feedback rieng; write endpoint nam chung trong `LlmSuggestionsController` cua FE-15.
2. FE-16 khong co `GET /feedback` rieng. Frontend doc feedback qua `GET /llm-suggestions` va `GET /llm-suggestions/{suggestionId}`.
3. `PUT /feedback` la upsert theo cap `(suggestionId, currentUserId)`. Moi user chi co toi da 1 feedback row cho moi suggestion.
4. Goi lai cung endpoint voi signal/note moi se overwrite row hien co cua user do, khong tao duplicate.
5. Ke ca khi user gui lai cung signal va cung note, backend van treat nhu update: `updatedDateTime` va `rowVersion` cua feedback row se duoc refresh.
6. Request FE-16 khong yeu cau `rowVersion`. Concurrency va transaction locking duoc xu ly noi bo ben server.
7. `signal` chi chap nhan `Helpful` hoac `NotHelpful`, parse case-insensitive va co trim whitespace.
8. `notes` la optional. Backend trim dau/cuoi; neu rong hoac chi co whitespace thi se luu `null`.
9. Gioi han runtime cho `notes` la 4000 ky tu. FE nen chan som client-side de tranh loi 400.
10. `PUT /feedback` chi bi chan theo state khi `suite` da archived hoac `suggestion.reviewStatus = Superseded`.
11. Feedback van duoc phep tren suggestion dang `Pending`, `Approved`, `Rejected`, hoac `ModifiedAndApproved`.
12. `PUT /feedback` tra ve `LlmSuggestionFeedbackModel`, khong tra lai `LlmSuggestionModel`. Neu UI dang hien summary tren card/list, FE nen patch local state hoac refetch list/detail.
13. `rowVersion` trong `LlmSuggestionFeedbackModel` la concurrency token cua feedback row, khong lien quan den `rowVersion` cua suggestion row trong FE-15 review flow.
14. `feedbackSummary` tren list/detail la aggregate cua tat ca feedback rows cho suggestion do. Neu chua co feedback nao, backend van tra object dem `0` thay vi `null`.
15. `currentUserFeedback` tren list/detail se la `null` neu user hien tai chua feedback suggestion do.
16. Generate/review response cua FE-15 hien tai thuong khong hydrate `currentUserFeedback` va `feedbackSummary`, du model van co 2 field nay. Hydration day du chi dang co o list/detail query.
17. Non-owner access hien map thanh `400 ProblemDetails`, khong phai `403`.
18. Khong co delete endpoint de xoa han row feedback. Neu muon doi y kien, FE goi lai cung `PUT /feedback` voi signal/note moi. Neu muon xoa note, gui `notes = null`, chuoi rong, hoac whitespace.
19. Feedback moi khong sua noi dung suggestion hien tai. Tac dung chinh cua FE-16 la cap nhat aggregate metadata va anh huong toi cac lan generate preview FE-15/FE-06 ve sau.
20. Backend dua feedback aggregate vao `FeedbackContext` va `FeedbackFingerprint` de thay doi cache key cua LLM suggestion pipeline. Vi vay feedback co the anh huong generate moi, nhung khong tu dong mutate list suggestion hien co.

## 5. Filter, param, sort hien tai

- `PUT /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}/feedback`
  - khong co query param
  - body: `signal`, `notes`
- `GET /api/test-suites/{suiteId}/llm-suggestions`
  - read surface cua FE-16 nam trong response item
  - query/sort giu nguyen theo FE-15
- `GET /api/test-suites/{suiteId}/llm-suggestions/{suggestionId}`
  - read surface cua FE-16 nam trong response item

## 6. Flow goi API frontend nen bam

1. Khi vao man hinh review suggestions, goi `GET /llm-suggestions` nhu FE-15 de lay danh sach suggestion va feedback metadata hien tai.
2. Render trang thai vote cua user tu `currentUserFeedback.signal` neu field nay khac `null`.
3. Render badge/tong ket tu `feedbackSummary.helpfulCount`, `feedbackSummary.notHelpfulCount`, va `feedbackSummary.lastFeedbackAt`.
4. Khi user bam Helpful/NotHelpful, goi `PUT /feedback` voi `signal` va `notes` hien tai cua form.
5. Sau khi `PUT /feedback` thanh cong, co the cap nhat local `currentUserFeedback` bang response moi.
6. Neu UI dang hien `feedbackSummary`, cach an toan nhat la refetch list/detail vi response write khong tra summary moi.
7. Neu user doi tu Helpful sang NotHelpful hoac sua note, chi can goi lai cung endpoint `PUT /feedback`.
8. Neu sau khi feedback xong FE cho phep regenerate preview, van phai tuan theo gate FE-15 hien tai, vi du `forceRefresh=true` neu suite dang con `Pending` suggestions.

## 7. Khuyen nghi su dung

- Dung optimistic UI neu muon nhanh, nhung can rollback neu `PUT /feedback` tra `400/404`.
- Disable double-click khi dang submit feedback de tranh ghi de state ngoai y muon.
- Gioi han `notes` <= 4000 ky tu ngay tren input.
- Neu chi can thumb up/down, co the gui `notes = null`.
- Neu muon "clear note" nhung giu nguyen signal, gui lai cung signal cu va `notes` rong/whitespace.
- Neu man hinh can summary chinh xac sau write, uu tien refetch list/detail thay vi tu tinh delta tren client.
