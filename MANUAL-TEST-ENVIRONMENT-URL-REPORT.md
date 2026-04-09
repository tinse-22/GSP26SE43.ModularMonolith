# Manual Test Report: Missing Environment / Missing Base URL

## 1. Scope

- Task classification: `Docs only`
- Goal: verify whether manual test execution can still run when the request does not provide `environmentId`, and whether the system already protects against the missing-URL scenario.
- GitNexus status: unavailable in this session, so this report is based on manual code inspection.

## 2. Executive Summary

### Conclusion

The backend **already supports** omitting `environmentId`, but it does **not** support running without any resolved execution environment.

Current behavior is:

1. If the request sends `environmentId`, backend loads that environment.
2. If the request omits `environmentId`, backend falls back to the project's **default execution environment**.
3. If the project has **no default execution environment**, the run fails immediately.

So the real answer is:

- "Không nhập environment" is **allowed by API contract**.
- "Không có URL để chạy" is **still a real failure mode** when the project has no default environment.
- The gap is **not** in `StartTestRun` fallback logic.
- The gap is that the system does **not enforce the invariant** that a project must always have a usable default environment when UI/back-end want `environmentId` to be optional.

## 3. Evidence From Code

### 3.1 Start test run already supports missing `environmentId`

`StartTestRunCommandHandler` explicitly falls back to default environment when `EnvironmentId` is null:

- `ClassifiedAds.Modules.TestExecution/Commands/StartTestRunCommand.cs`
  - if `EnvironmentId` exists, load that environment
  - else load `x.ProjectId == suiteContext.ProjectId && x.IsDefault`
  - if not found, throw `"Không tìm thấy execution environment mặc định cho project này."`

This means missing `environmentId` is intentionally supported, but only if a default environment exists.

### 3.2 Base URL is mandatory at environment level

`ExecutionEnvironment` requires `BaseUrl`, and create/update validation enforces:

- `BaseUrl` is required
- `BaseUrl` must be an absolute `http` or `https` URL

Therefore, once an execution environment is resolved, URL composition has the data it needs.

### 3.3 Runtime URL building depends on resolved environment

`VariableResolver.BuildFinalUrl(...)` combines relative request URLs with `environment.BaseUrl`.

That confirms the user's concern is valid in principle: without a resolved environment, the runtime has no canonical base URL for relative test-case URLs.

### 3.4 Current implementation allows project state with no default environment

The main operational gap is here:

- update flow allows setting `IsDefault = false`
- delete flow allows deleting the current default environment
- no replacement default is auto-selected
- no invariant guarantees "at least one default environment per project"

This is also explicitly documented in:

- `docs/frontend/FE-04-test-configuration-frontend/execution-environments-api.json`
  - current implementation allows deleting default environment without replacement
  - backend enforces at most one default environment, but not at least one

## 4. Existing Test Coverage

There are already unit tests showing the intended current behavior:

- `HandleAsync_WithoutEnvironmentId_ShouldFallbackToDefault`
- `HandleAsync_NoDefaultEnvironmentFound_ShouldThrowNotFound`

So the fallback logic already exists and is covered at unit-test level.

## 5. Manual-Test Result

### Result matrix

| Scenario | Current behavior | Status |
|---|---|---|
| Request provides valid `environmentId` | Run can proceed | OK |
| Request omits `environmentId`, project has default environment | Backend falls back to default and can proceed | OK |
| Request omits `environmentId`, project has no default environment | Backend fails before execution | GAP |
| Current default environment is unset/deleted | Project can become un-runnable unless caller always sends explicit `environmentId` | GAP |

## 6. Root Cause

The issue is **not** "API cannot support missing environment."

The real root cause is:

> `environmentId` is optional, but the system does not guarantee a fallback environment always exists.

That creates a configuration hole:

- the API contract says omission is allowed
- runtime behavior expects a default environment
- environment management flows allow the default to disappear

## 7. Best-Practice Solution

### Recommended direction

Adopt this invariant:

> For every project that has execution environments, there must be exactly one default execution environment.

### Concrete solution

#### P0. Enforce default-environment invariant in write flows

Update `AddUpdateExecutionEnvironmentCommandHandler` and `DeleteExecutionEnvironmentCommandHandler` so that:

1. Creating the **first** environment auto-sets `IsDefault = true`.
2. Unsetting the **current only default** is blocked unless another replacement default is selected in the same transaction.
3. Deleting the **current default** is blocked unless:
   - another environment is promoted atomically, or
   - the user explicitly chooses a replacement environment in the same operation.

This is the most important fix because it removes the invalid project state at the source.

#### P1. Improve API semantics for missing fallback

When `environmentId == null` and no default environment exists, prefer returning:

- `409 Conflict` or `400 BadRequest`
- reason code such as `DEFAULT_EXECUTION_ENVIRONMENT_REQUIRED`
- message telling the user to create/select a valid environment with `BaseUrl`

Why this is better than current `404`:

- the request route is valid
- the problem is project configuration, not resource lookup ambiguity

#### P2. Add frontend preflight/UX guard

If UI allows "Run without choosing environment", then before enabling the action:

1. load execution environments for the project
2. detect whether a default environment exists
3. disable or warn on the Run action if:
   - no environment is selected, and
   - no default environment exists

Recommended UI message:

> Chưa có execution environment mặc định. Hãy chọn một environment hoặc tạo environment có BaseUrl hợp lệ trước khi chạy test.

#### P3. Add explicit integration tests for invariant

Add tests for:

1. first environment becomes default automatically
2. cannot unset the last default without replacement
3. cannot delete the last/default environment without replacement
4. start run without `environmentId` returns domain-specific config error when no default exists

## 8. Recommended Implementation Order

1. Enforce default-environment invariant in create/update/delete flows.
2. Improve `StartTestRun` error reason code/message.
3. Add FE preflight/disable-state.
4. Add integration tests for all four scenarios above.

## 9. Final Assessment

### Already exists

- Optional `environmentId` in start-run API
- fallback to project default environment
- mandatory `BaseUrl` validation on execution environment
- unit tests for fallback and missing-default behavior

### Still missing

- hard guarantee that a project always keeps a default environment
- strong UX/API guard for the "no selected env + no default env" state
- domain-specific error contract for missing fallback environment

## 10. Verification Notes

- Code changes made in this task: documentation only
- Migration verification: not required, no EF/model changes
- Docker/compose verification: not required, no runtime wiring changes
- Test run note: targeted `dotnet test` on `ClassifiedAds.UnitTests` could not complete because the test project currently has unrelated compile errors in:
  - `ClassifiedAds.UnitTests/TestExecution/GetTestRunResultsQueryHandlerTests.cs`
  - `ClassifiedAds.UnitTests/TestExecution/TestResultCollectorTests.cs`

That test-project issue does not change the code-inspection conclusion above.
