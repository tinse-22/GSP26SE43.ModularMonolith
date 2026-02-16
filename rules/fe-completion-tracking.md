# Rule: FE Completion Tracking

> **Priority:** HIGH  
> **Applies to:** All AI Agents working on this codebase  

---

## Purpose

Ensure that every completed Feature (FE) is properly tracked in the FE Completion Tracker file located at `docs/FE_COMPLETION_TRACKER.md`. This provides project visibility and prevents duplicate work.

---

## Rule

### When an AI Agent completes implementing a Feature (FE):

The agent **MUST** update `docs/FE_COMPLETION_TRACKER.md` as the **final step** before committing code.

### Required Actions:

1. **Update the Feature row** in the corresponding section:
   - Set **Status** to `✅ Completed`
   - Set **Branch** to the current git branch name
   - Set **Completed Date** to the current date (YYYY-MM-DD format)
   - Add **Notes** describing what was implemented (key components, controllers, services, etc.)

2. **Update the Summary counts** at the top of the file:
   - Adjust counts for ✅ Completed, 🔨 In Progress, 📋 Skeleton Only, ❌ Not Started

3. **Update the Module Implementation Summary** table if the module completeness changed

4. **Add a Change Log entry** at the bottom:
   ```
   | YYYY-MM-DD | FE-XX | Description of what was completed | AI Agent |
   ```

### When starting work on a Feature (FE):

1. Update the Feature row **Status** to `🔨 In Progress`
2. Fill in the **Branch** name
3. Update the Summary counts

### Status Definitions:

| Icon | Status | When to use |
|------|--------|-------------|
| ✅ | Completed | Feature fully implemented with controllers, commands, queries, services, and tests |
| 🔨 | In Progress | Currently being developed (only ONE agent should work on a FE at a time) |
| 📋 | Skeleton Only | Module has entities/DbContext but no business logic, controllers, or services |
| ❌ | Not Started | No implementation exists for this feature |

---

## Example Update

When completing FE-05 (Happy-path test generation):

```markdown
<!-- Before -->
| **FE-05** | Auto-generate happy-path test cases | TestGeneration | 📋 Skeleton Only | — | — | Entities defined but no logic |

<!-- After -->
| **FE-05** | Auto-generate happy-path test cases | TestGeneration | ✅ Completed | `feature/fe-05-test-generation` | 2026-03-01 | Implemented HappyPathGenerator service, TestCasesController, GenerateTestCasesCommand. Supports schema-compliant valid input generation |
```

---

## Validation Checklist

Before marking a FE as ✅ Completed, verify:

- [ ] All required API endpoints are implemented (Controllers)
- [ ] Business logic is in place (Commands/Queries/Services)
- [ ] Database entities and configurations are complete (DbConfigurations)
- [ ] Authorization policies are configured (if applicable)
- [ ] The feature builds without errors (`dotnet build`)
- [ ] Basic tests exist or have been verified
- [ ] `docs/FE_COMPLETION_TRACKER.md` is updated

---

## File Location

```
docs/FE_COMPLETION_TRACKER.md    ← Main tracking file (UPDATE this)
rules/fe-completion-tracking.md  ← This rule file (DO NOT modify)
```

---

## Why This Matters

- **Prevents duplicate work** — Agents can check what's already done before starting
- **Provides project visibility** — Team can see overall progress at a glance
- **Enables dependency tracking** — Some FEs depend on others being completed first
- **Maintains accountability** — Change log shows who did what and when
