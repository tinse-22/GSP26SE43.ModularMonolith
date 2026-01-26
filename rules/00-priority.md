# 00 - Rule Priority and Conflict Resolution

> **Purpose:** Defines the strict priority order for all rules, how to resolve conflicts, and the mandatory process for AI Agents and developers to apply rules. This file is the **single source of truth** for rule precedence.

---

## 1. Rule Priority Order (Highest to Lowest)

| Priority | Category | File | Description |
|----------|----------|------|-------------|
| 1 | **Security** | `security.md` | AuthN/AuthZ, secrets, OWASP, data protection |
| 2 | **Architecture** | `architecture.md` | Module boundaries, CQRS, layering, persistence |
| 3 | **AI Agent Standards** | `ai-agent-coding-standards.md` | **Mandatory patterns for AI Agents** |
| 4 | **Testing** | `testing.md` | Coverage, test patterns, CI validation |
| 5 | **Coding** | `coding.md` | C# conventions, async, logging, DI |
| 6 | **Git Workflow** | `git-workflow.md` | Branching, commits, PRs, releases |

### Priority Rules

- **Security** rules MUST override all other rules. If any rule would weaken security, the security rule takes absolute precedence.
- **Architecture** rules MUST override Testing, Coding, and Git Workflow rules.
- **AI Agent Standards** define mandatory implementation patterns that MUST be followed.
- **Testing** rules MUST override Coding and Git Workflow rules.
- **Coding** rules MUST override Git Workflow rules.

---

## 2. Conflict Resolution Protocol

### 2.1 When Rules Conflict

1. **Identify** the conflicting rules and their categories.
2. **Apply** the rule from the higher-priority category (per table above).
3. **Document** the conflict resolution in code with a comment:
   ```csharp
   // RULE-CONFLICT: security.md > coding.md
   // Reason: Security requires not logging this value, even though coding suggests structured logging.
   ```
4. **If ambiguous**, escalate to the project architect or tech lead before proceeding.

### 2.2 Mandatory Exception Documentation

All exceptions to rules MUST be documented with:
- The rule being bypassed (file + rule ID if applicable)
- The justification (security audit, performance requirement, external constraint)
- Approval from tech lead (for Security/Architecture exceptions)

```csharp
// EXCEPTION: architecture.md - "No business logic in controllers"
// Justification: Simple validation for file size before upload, approved by @lead on 2025-01-01
// Tech Lead: @johndoe
```

---

## 3. AI Agent Instructions (MANDATORY)

### 3.1 Reading Order

AI Agents MUST read rules files in this exact order before generating any code:

1. `rules/00-priority.md` (this file) — Understand priority and conflict resolution
2. `rules/security.md` — Security constraints are non-negotiable
3. `rules/architecture.md` — Module boundaries, CQRS, persistence patterns
4. `rules/testing.md` — Test requirements for any new code
5. `rules/coding.md` — C# conventions, async patterns, DI
6. `rules/git-workflow.md` — Branch naming, commit format

### 3.2 Pre-Code Generation Checklist

Before outputting any code, AI Agents MUST verify:

- [ ] Code does not violate any `MUST NOT` rules in `security.md`
- [ ] Code follows module boundaries defined in `architecture.md`
- [ ] Code includes appropriate test coverage as per `testing.md`
- [ ] Code follows naming conventions and patterns in `coding.md`
- [ ] Any generated branch/commit follows `git-workflow.md`

### 3.3 Post-Code Review Checklist

After generating code, AI Agents MUST self-verify:

- [ ] No hardcoded secrets, connection strings, or API keys
- [ ] All async methods use `CancellationToken`
- [ ] Controllers delegate to Dispatcher (no business logic)
- [ ] DTOs are explicitly mapped (no implicit casting)
- [ ] Structured logging with correlation IDs (no PII)
- [ ] Test files follow naming convention: `Method_ShouldExpected_WhenCondition`

---

## 4. Rule Keywords Definition

All rules use RFC 2119 keywords with the following meanings:

| Keyword | Meaning |
|---------|---------|
| **MUST** | Absolute requirement. Violation is a blocking issue. |
| **MUST NOT** | Absolute prohibition. Violation is a blocking issue. |
| **SHOULD** | Recommended. May be skipped with documented justification. |
| **SHOULD NOT** | Not recommended. May be used with documented justification. |
| **MAY** | Optional. Use at discretion based on context. |

---

## 5. Enforcement Mechanisms

| Rule Category | Enforcement Method |
|---------------|-------------------|
| Security | CI security scanning, code review, secret detection |
| Architecture | Analyzers, ArchUnit tests, code review |
| Testing | CI test gates, coverage thresholds |
| Coding | `.editorconfig`, `dotnet format`, Roslyn analyzers |
| Git Workflow | Branch protection, PR templates, CI status checks |

---

## 6. Checklist (Complete Before Every PR)

- [ ] I have read `00-priority.md` and understand the priority order
- [ ] I have applied rules from all relevant files
- [ ] I have resolved any rule conflicts using the priority order
- [ ] I have documented all exceptions with justification
- [ ] I have completed the checklist in each relevant rule file
- [ ] My code passes all CI checks (build, test, format, security)

---

## 7. Good Example: Conflict Resolution

```csharp
// SCENARIO: Need to log request details for debugging, but request contains user email

// BAD: Violates security.md - logging PII
_logger.LogInformation("Request from {Email} with data {Body}", user.Email, requestBody);

// GOOD: Security rule overrides coding convenience
_logger.LogInformation("Request from UserId={UserId} with CorrelationId={CorrelationId}", 
    user.Id, 
    Activity.Current?.Id);

// RULE-APPLIED: security.md "PII MUST NOT be logged" overrides coding.md "structured logging"
```

---

## 8. Quick Reference Card

```
┌─────────────────────────────────────────────────────────────┐
│                    RULE PRIORITY                            │
├─────────────────────────────────────────────────────────────┤
│  1. SECURITY      → Always wins. No exceptions.             │
│  2. ARCHITECTURE  → Module boundaries, CQRS, persistence    │
│  3. TESTING       → 80% coverage, AAA pattern               │
│  4. CODING        → Conventions, async, DI                  │
│  5. GIT WORKFLOW  → Branch naming, commit format            │
├─────────────────────────────────────────────────────────────┤
│  CONFLICT? → Higher priority wins + document in code        │
│  AMBIGUOUS? → Escalate to tech lead before proceeding       │
└─────────────────────────────────────────────────────────────┘
```
