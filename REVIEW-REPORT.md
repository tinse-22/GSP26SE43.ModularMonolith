# Code Review Report

**Branch:** `feature/FE-03-json-parsing-endpoints`
**Date:** 2026-03-11
**Scope:** 9 classes requested for review

---

## Summary

| # | File | Module | Verdict |
|---|------|--------|---------|
| 1 | `UploadApiSpecificationCommand.cs` | ApiDocumentation | 1 Warning |
| 2 | `TestSuiteConfiguration.cs` | TestGeneration | 2 Issues |
| 3 | `CreateTestSuiteScopeRequest.cs` | TestGeneration | OK |
| 4 | `UpdateTestSuiteScopeRequest.cs` | TestGeneration | OK |
| 5 | `ProposeApiTestOrderRequest.cs` | TestGeneration | 1 Warning |
| 6 | `ApiTestOrderProposalModel.cs` | TestGeneration | 1 Issue |
| 7 | `GenerateTestCasesCommand.cs` | TestGeneration | FILE MISSING |
| 8 | `TestOrderController.cs` | TestGeneration | 1 Warning |
| 9 | `Permissions.cs` (TestGeneration) | TestGeneration | 1 Warning |
| 10 | `Permissions.cs` (ApiDocumentation) | ApiDocumentation | OK |

---

## 1. UploadApiSpecificationCommand.cs

**Path:** `ClassifiedAds.Modules.ApiDocumentation\Commands\UploadApiSpecificationCommand.cs`

### Result: 1 Warning

#### [W-01] IFormFile stream consumed twice without reset (WARNING)

```csharp
// Line 114-116: First read
using (var reader = new StreamReader(command.File.OpenReadStream()))
{
    fileContent = await reader.ReadToEndAsync(cancellationToken);
}

// Line 164: Second read
using var stream = command.File.OpenReadStream();
```

`IFormFile.OpenReadStream()` creates a new stream each time, so this is technically fine.
However, two separate reads of the upload stream can be inefficient for large files.
Consider reading the content once and reusing the byte array for both validation and upload.

#### Notes (no issues):
- Subscription limit check with `TryConsumeLimitAsync` is done atomically before upload -- good.
- Transaction around spec creation and activation -- good.
- Validates file extension, size, content format -- good.
- `SavedSpecId` is set as output property on the command -- acceptable pattern in this codebase.

---

## 2. TestSuiteConfiguration.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\DbConfigurations\TestSuiteConfiguration.cs`

### Result: 2 Issues

#### [I-01] RowVersion configured with `ValueGeneratedNever()` -- potential conflict with base Entity (ISSUE)

```csharp
// Line 38-42
builder.Property(x => x.RowVersion)
    .HasColumnType("bytea")
    .IsConcurrencyToken()
    .ValueGeneratedNever()   // <--- ISSUE
    .IsRequired();
```

The base class `Entity<TKey>` (in `ClassifiedAds.Domain\Entities\Entity.cs:10-11`) declares:

```csharp
[Timestamp]
public byte[] RowVersion { get; set; }
```

The `[Timestamp]` attribute tells EF Core that the value is store-generated on add/update.
But `TestSuiteConfiguration` overrides this with `ValueGeneratedNever()`, meaning the application must explicitly set `RowVersion` on every insert and update.

**Risk:** If the application code does not manually set `RowVersion` before saving, it will be inserted as `null` or empty, which will:
- Fail because `.IsRequired()` is set.
- Or if somehow bypassed, break all subsequent concurrency checks.

**Action needed:** Verify that every command handler that creates or updates a `TestSuite` entity explicitly sets `RowVersion` (e.g., `suite.RowVersion = Guid.NewGuid().ToByteArray()`). If not, this is a **runtime bug**. Otherwise, consider switching to `ValueGeneratedOnAddOrUpdate()` to let PostgreSQL handle it.

#### [I-02] Missing EF configuration for several TestSuite properties (INFO)

The following `TestSuite` entity properties have no explicit EF configuration:

| Property | Entity Type | Missing Config |
|----------|-------------|---------------|
| `ProjectId` | `Guid` | No FK relationship configured (only index) |
| `ApiSpecId` | `Guid?` | No FK relationship configured (only index) |
| `CreatedById` | `Guid` | No FK relationship configured (only index) |
| `ApprovedById` | `Guid?` | No FK relationship configured (only index) |
| `LastModifiedById` | `Guid?` | No FK relationship configured (only index) |
| `ApprovedAt` | `DateTimeOffset?` | No config |

These are cross-module foreign keys (likely referencing entities in other modules like ApiDocumentation and Identity), so **not configuring FK relationships is intentional** in a modular monolith. However:
- `ProjectId` and `ApiSpecId` reference entities in the ApiDocumentation module. If those records are deleted, `TestSuite` rows will hold orphaned references with no cascade delete protection at the DB level.
- Consider adding a note in comments or documentation if this is by design.

**Verdict:** Informational only -- no action required if cross-module FK avoidance is intentional.

---

## 3. CreateTestSuiteScopeRequest.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\Models\Requests\CreateTestSuiteScopeRequest.cs`

### Result: OK

- `[Required]` and `[MaxLength]` annotations are appropriate.
- `[MinLength(1)]` on `SelectedEndpointIds` ensures at least one endpoint is selected.
- `EndpointBusinessContexts` defaults to empty dictionary -- good.
- `GlobalBusinessRules` with `[MaxLength(8000)]` is reasonable for free text.

No issues found.

---

## 4. UpdateTestSuiteScopeRequest.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\Models\Requests\UpdateTestSuiteScopeRequest.cs`

### Result: OK

- Includes `RowVersion` for optimistic concurrency -- good.
- Same structure as `CreateTestSuiteScopeRequest` with the addition of `RowVersion`.
- All required annotations are present.

No issues found.

---

## 5. ProposeApiTestOrderRequest.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\Models\Requests\ProposeApiTestOrderRequest.cs`

### Result: 1 Warning

#### [W-02] Type mismatch between Request and Command for `SelectedEndpointIds`

```
Request:  List<Guid> SelectedEndpointIds    (ProposeApiTestOrderRequest.cs:13)
Command:  IReadOnlyCollection<Guid> SelectedEndpointIds  (ProposeApiTestOrderCommand.cs:24)
```

In `TestOrderController.cs:52`, the assignment is:
```csharp
SelectedEndpointIds = request.SelectedEndpointIds,
```

This works because `List<Guid>` implements `IReadOnlyCollection<Guid>`, so no compilation error. But:
- The command receives a **mutable** `List<Guid>` disguised as `IReadOnlyCollection<Guid>`.
- If any handler code casts it back to `List<Guid>` and modifies it, the original request object is mutated.

**Recommendation:** Either make both `List<Guid>`, or convert to an immutable collection in the controller: `SelectedEndpointIds = request.SelectedEndpointIds.AsReadOnly()`.

---

## 6. ApiTestOrderProposalModel.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\Models\ApiTestOrderProposalModel.cs`

### Result: 1 Issue

#### [I-03] `ConsideredFactors` typed as `object` -- loose typing

```csharp
// Line 27
public object ConsideredFactors { get; set; }
```

Meanwhile, in the entity (`TestOrderProposal.cs:45`):
```csharp
public string ConsideredFactors { get; set; }  // stored as JSON string
```

The model uses `object` while the entity uses `string`. This means the command handler must deserialize the JSON string into some object when mapping entity -> model. The `object` type provides no type safety and will serialize differently depending on the actual runtime type.

**Recommendation:** Define a concrete type (e.g., `List<string>` or a DTO like `ConsideredFactorsDto`) instead of `object`. If the structure is truly dynamic, use `JsonElement` or `Dictionary<string, object>` for more predictable serialization.

---

## 7. GenerateTestCasesCommand.cs

### Result: FILE DOES NOT EXIST

**This file was requested for review but does not exist anywhere in the repository.**

Investigation reveals:
- The `TestCasesController.cs` uses `GenerateHappyPathTestCasesCommand` (exists at `Commands\GenerateHappyPathTestCasesCommand.cs`)
  and `GenerateBoundaryNegativeTestCasesCommand` (exists at `Commands\GenerateBoundaryNegativeTestCasesCommand.cs`).
- There is **no** class named `GenerateTestCasesCommand`.
- The rule template file `rules/fe-completion-tracking.md` mentions "GenerateTestCasesCommand" in an **example** block, but this is a template illustration, not actual tracking data. The real tracker `docs/tracking/FE_COMPLETION_TRACKER.md` already uses the correct class names.

**Conclusion:** The file was either:
1. Renamed to `GenerateHappyPathTestCasesCommand.cs` and `GenerateBoundaryNegativeTestCasesCommand.cs` during implementation.
2. Never created -- the design was split into two separate commands from the start.

**Action needed:** None. The actual tracking document (`docs/tracking/FE_COMPLETION_TRACKER.md`) already references the correct class names. The rule template at `rules/fe-completion-tracking.md` is marked as "DO NOT modify" and its example is purely illustrative.

---

## 8. TestOrderController.cs

**Path:** `ClassifiedAds.Modules.TestGeneration\Controllers\TestOrderController.cs`

### Result: 1 Warning

#### [W-03] Reject endpoint reuses `Permissions.ApproveTestOrder` permission

```csharp
// Lines 134-135
[Authorize(Permissions.ApproveTestOrder)]        // <--- Same as Approve
[HttpPost("order-proposals/{proposalId:guid}/reject")]
```

Both the **Approve** action (line 109) and the **Reject** action (lines 134-135) use `Permissions.ApproveTestOrder`.

The current `Permissions.cs` does NOT define a separate `RejectTestOrder` permission.

**Impact:** Any user with the "approve" permission can also reject proposals. This may be intentional (approve/reject as the same privilege level), but if the business requires separate permissions for approve vs. reject, a dedicated `Permissions.RejectTestOrder` constant should be created.

**Recommendation:** Clarify business requirements. If approve and reject should be gated independently, add:
```csharp
public const string RejectTestOrder = "Permission:RejectTestOrder";
```

#### Notes (no issues):
- All endpoints return proper HTTP status codes (201 Created, 200 OK, 404 Not Found, 409 Conflict).
- Logging includes structured parameters with `TestSuiteId`, `ProposalId`, `ActorUserId`.
- Uses `Dispatcher` pattern consistently.
- Route pattern `api/test-suites/{suiteId:guid}` with route constraint is correct.

---

## 9. Permissions.cs (TestGeneration)

**Path:** `ClassifiedAds.Modules.TestGeneration\Authorization\Permissions.cs`

### Result: 1 Warning

#### [W-04] No dedicated `RejectTestOrder` permission (related to W-03)

```csharp
// Test Order (FE-05A) section
public const string ProposeTestOrder = "Permission:ProposeTestOrder";
public const string GetTestOrderProposal = "Permission:GetTestOrderProposal";
public const string ReorderTestOrder = "Permission:ReorderTestOrder";
public const string ApproveTestOrder = "Permission:ApproveTestOrder";
// Missing: RejectTestOrder
```

Same issue as W-03. The Reject action in `TestOrderController` uses `ApproveTestOrder` permission because no `RejectTestOrder` is defined.

---

## 10. Permissions.cs (ApiDocumentation)

**Path:** `ClassifiedAds.Modules.ApiDocumentation\Authorization\Permissions.cs`

### Result: OK

- Clean permission hierarchy: Projects -> Specifications -> Endpoints.
- Each entity has standard CRUD permissions.
- `ActivateSpecification` is a separate permission from `UpdateSpecification` -- good separation.

No issues found.

---

## Overall Assessment

### Critical Issues (0)
None. No compile errors or critical bugs found.

### Issues Requiring Attention (2)
| ID | File | Description |
|----|------|-------------|
| I-01 | `TestSuiteConfiguration.cs` | `ValueGeneratedNever()` on `RowVersion` may cause runtime failures if handlers don't manually set RowVersion |
| I-03 | `ApiTestOrderProposalModel.cs` | `ConsideredFactors` typed as `object` -- loose typing, unpredictable serialization |

### Warnings / Design Concerns (4)
| ID | File | Description |
|----|------|-------------|
| W-01 | `UploadApiSpecificationCommand.cs` | File stream read twice (inefficiency, not a bug) |
| W-02 | `ProposeApiTestOrderRequest.cs` | `List<Guid>` assigned to `IReadOnlyCollection<Guid>` without defensive copy |
| W-03 | `TestOrderController.cs` | Reject endpoint reuses `ApproveTestOrder` permission |
| W-04 | `Permissions.cs` | No dedicated `RejectTestOrder` permission defined |

### Missing File (1)
| File | Description |
|------|-------------|
| `GenerateTestCasesCommand.cs` | Does not exist. Actual implementations are `GenerateHappyPathTestCasesCommand.cs` and `GenerateBoundaryNegativeTestCasesCommand.cs` |

### VS Code Diagnostics
IDE reports only **hints** (style suggestions) in `ILlmScenarioSuggester.cs` -- no errors or warnings in any of the reviewed files.
