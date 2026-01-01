# Git Workflow Rules

> **Purpose:** Ensure a consistent, auditable, and high-quality development process for all contributors. These rules govern branching, commits, pull requests, and releases.

---

## 1. Branch Naming

### Rules

- **[GIT-001]** Branch names MUST follow this format:
  ```
  {type}/{ticket-or-description}
  ```

- **[GIT-002]** Allowed branch types:
  | Type | Purpose | Example |
  |------|---------|---------|
  | `feature/` | New features | `feature/add-product-export` |
  | `bugfix/` | Bug fixes | `bugfix/fix-product-validation` |
  | `hotfix/` | Urgent production fixes | `hotfix/security-patch-auth` |
  | `release/` | Release preparation | `release/1.2.0` |
  | `chore/` | Maintenance, refactoring | `chore/update-dependencies` |
  | `docs/` | Documentation only | `docs/update-api-readme` |

- **[GIT-003]** Branch names MUST:
  - Use lowercase letters, numbers, and hyphens only
  - Be descriptive but concise (max 50 characters after type/)
  - Include ticket number if applicable (e.g., `feature/ABC-123-add-product-export`)

- **[GIT-004]** MUST NOT use:
  - Spaces or special characters
  - Personal names
  - Generic names like `feature/update`, `bugfix/fix`

```bash
# GOOD
feature/add-product-csv-export
bugfix/ABC-456-fix-null-product-name
hotfix/security-jwt-validation
release/2.0.0

# BAD
feature/my changes          # Spaces, vague
Feature/AddExport           # Wrong case
bugfix/fix                  # Too generic
john/product-fix            # Personal name
```

---

## 2. Commit Messages

### Rules

- **[GIT-010]** Commit messages MUST follow Conventional Commits format:
  ```
  <type>(<scope>): <subject>

  [optional body]

  [optional footer(s)]
  ```

- **[GIT-011]** Commit types:
  | Type | Purpose |
  |------|---------|
  | `feat` | New feature |
  | `fix` | Bug fix |
  | `docs` | Documentation changes |
  | `style` | Code style (formatting, no logic change) |
  | `refactor` | Code refactoring (no feature/fix) |
  | `perf` | Performance improvement |
  | `test` | Adding or fixing tests |
  | `build` | Build system or dependencies |
  | `ci` | CI/CD changes |
  | `chore` | Other maintenance |
  | `revert` | Revert previous commit |

- **[GIT-012]** Scope MUST be the module or area affected:
  - `product`, `identity`, `storage`, `notification`, `auditlog`, `config`
  - `api`, `background`, `migrator`
  - `infra`, `domain`, `contracts`

- **[GIT-013]** Subject line:
  - MUST be imperative mood ("add" not "added")
  - MUST NOT exceed 72 characters
  - MUST NOT end with a period
  - MUST start with lowercase

- **[GIT-014]** Body (optional):
  - MUST be separated from subject by blank line
  - SHOULD explain "what" and "why", not "how"
  - MUST wrap at 72 characters

- **[GIT-015]** Footer (optional):
  - Breaking changes: `BREAKING CHANGE: <description>`
  - Issue references: `Fixes #123`, `Closes #456`

```bash
# GOOD
feat(product): add CSV export endpoint

Add new endpoint GET /api/products/export/csv that exports
all products to CSV format. Uses ICsvWriter for generation.

Closes #123

# GOOD - Simple fix
fix(identity): correct null check in user validation

# BAD
Fixed stuff                          # No type, vague
feat: Add new feature                # Missing scope, capitalized
feat(product): Added the export.     # Past tense, period
```

---

## 3. Pull Requests

### 3.1 PR Requirements

- **[GIT-020]** All changes MUST go through pull requests (no direct commits to `main`).
- **[GIT-021]** PRs MUST target `main` branch (or release branch for hotfixes).
- **[GIT-022]** PRs MUST have a descriptive title following commit message format.
- **[GIT-023]** PRs MUST use the PR template (see below).
- **[GIT-024]** PRs MUST have at least one approval before merge.

### 3.2 PR Template

PRs MUST include this template:

```markdown
## Description
<!-- What does this PR do? Why is it needed? -->

## Type of Change
- [ ] feat: New feature
- [ ] fix: Bug fix
- [ ] docs: Documentation
- [ ] refactor: Code refactoring
- [ ] test: Test changes
- [ ] chore: Maintenance

## Related Issues
<!-- Link to issues: Fixes #123, Closes #456 -->

## Checklist

### Security (rules/security.md)
- [ ] No hardcoded secrets
- [ ] All endpoints have proper authorization
- [ ] Input validation implemented
- [ ] No PII in logs

### Architecture (rules/architecture.md)
- [ ] Module boundaries respected
- [ ] Controllers are thin (logic in handlers)
- [ ] CQRS pattern followed
- [ ] DTO mapping is explicit

### Testing (rules/testing.md)
- [ ] Unit tests written
- [ ] Integration tests written (if API changes)
- [ ] Tests follow naming convention
- [ ] Coverage >= 80%

### Coding (rules/coding.md)
- [ ] Naming conventions followed
- [ ] Async/await with CancellationToken
- [ ] No sync-over-async
- [ ] Code formatted with `dotnet format`

### Git (rules/git-workflow.md)
- [ ] Branch name follows convention
- [ ] Commit messages follow convention
- [ ] PR title is descriptive

## Screenshots (if UI changes)
<!-- Add screenshots here -->

## Testing Instructions
<!-- How can reviewers test this change? -->
```

### 3.3 PR Size

- **[GIT-030]** PRs SHOULD be small and focused (< 400 lines changed).
- **[GIT-031]** Large changes SHOULD be split into multiple PRs.
- **[GIT-032]** PRs MUST NOT mix features with refactoring.

---

## 4. Code Review

### Rules

- **[GIT-040]** All PRs MUST be reviewed by at least one team member.
- **[GIT-041]** Reviewers MUST check against rules checklists.
- **[GIT-042]** Review comments MUST be constructive and specific.
- **[GIT-043]** All review comments MUST be resolved before merge.
- **[GIT-044]** Author MUST respond to all comments (resolve or discuss).

### Review Focus Areas

1. **Security** — Check for vulnerabilities, secrets exposure
2. **Architecture** — Verify module boundaries, CQRS compliance
3. **Testing** — Ensure adequate test coverage
4. **Performance** — Look for obvious performance issues
5. **Code Quality** — Naming, readability, maintainability

---

## 5. CI Requirements

### Rules

- **[GIT-050]** All CI checks MUST pass before merge:
  - Build (`dotnet build`)
  - Tests (`dotnet test`)
  - Format check (`dotnet format --verify-no-changes`)
  - Security scan (if configured)

- **[GIT-051]** Branch protection MUST require:
  - Passing CI checks
  - At least 1 approval
  - Up-to-date branch

- **[GIT-052]** MUST NOT bypass CI checks or force merge.

---

## 6. Merge Strategy

### Rules

- **[GIT-060]** MUST use **Squash and Merge** for feature/bugfix branches.
- **[GIT-061]** Squash commit message MUST follow commit format.
- **[GIT-062]** MAY use **Merge Commit** for release branches.
- **[GIT-063]** MUST NOT use **Rebase and Merge** (preserves all commits, messy history).

---

## 7. Releases and Versioning

### 7.1 Semantic Versioning

- **[GIT-070]** Versions MUST follow Semantic Versioning (SemVer): `MAJOR.MINOR.PATCH`
  - **MAJOR**: Breaking changes
  - **MINOR**: New features (backward compatible)
  - **PATCH**: Bug fixes (backward compatible)

- **[GIT-071]** Pre-release versions: `1.0.0-alpha.1`, `1.0.0-beta.2`, `1.0.0-rc.1`

### 7.2 Release Process

- **[GIT-080]** Releases MUST be tagged from `main` branch.
- **[GIT-081]** Tag format: `v{MAJOR}.{MINOR}.{PATCH}` (e.g., `v1.2.3`).
- **[GIT-082]** Release notes MUST list all changes since last release.
- **[GIT-083]** CHANGELOG.md SHOULD be updated with each release.

```bash
# Create release tag
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0
```

### 7.3 Hotfix Process

- **[GIT-090]** Hotfixes MUST branch from `main` (or latest release tag).
- **[GIT-091]** Hotfix branch: `hotfix/{description}`
- **[GIT-092]** After merge to `main`, hotfix MUST be merged to active release branches.
- **[GIT-093]** Hotfix creates a PATCH version bump.

```bash
# Hotfix workflow
git checkout main
git checkout -b hotfix/security-fix
# ... make fix ...
git commit -m "fix(identity): patch JWT validation vulnerability"
# Create PR to main
# After merge, tag as v1.2.1
```

---

## 8. Branch Protection

### Rules (Repository Configuration)

- **[GIT-100]** `main` branch MUST be protected with:
  - Require pull request reviews (minimum 1)
  - Require status checks to pass
  - Require branches to be up to date
  - Include administrators in restrictions

- **[GIT-101]** MUST NOT force push to `main`.
- **[GIT-102]** MUST NOT delete `main` branch.

---

## 9. Git Hygiene

### Rules

- **[GIT-110]** MUST NOT commit:
  - Build artifacts (`bin/`, `obj/`)
  - IDE settings (`.vs/`, `.idea/`)
  - User secrets, local configs
  - Large binary files

- **[GIT-111]** `.gitignore` MUST be maintained to exclude:
  ```
  bin/
  obj/
  .vs/
  *.user
  appsettings.*.local.json
  ```

- **[GIT-112]** SHOULD regularly clean up merged branches.
- **[GIT-113]** MUST pull latest `main` before creating new branches.

---

## Conflict Resolution

If a git workflow rule conflicts with a higher-priority rule (Security, Architecture, Testing, Coding), follow the higher-priority rule as defined in `00-priority.md`.

---

## Checklist (Complete Before PR)

- [ ] Branch name follows `{type}/{description}` format
- [ ] All commits follow Conventional Commits format
- [ ] PR uses template with all sections filled
- [ ] All rule checklists in PR template completed
- [ ] CI checks pass (build, test, format)
- [ ] At least one approval received
- [ ] All review comments resolved
- [ ] Branch is up-to-date with `main`

---

## Good Example: Complete Workflow

```bash
# 1. Start from updated main
git checkout main
git pull origin main

# 2. Create feature branch
git checkout -b feature/ABC-123-add-product-csv-export

# 3. Make changes with proper commits
git add .
git commit -m "feat(product): add CSV export endpoint

Implement GET /api/products/export/csv using ICsvWriter.
Includes unit tests and integration tests.

Closes #123"

# 4. Push and create PR
git push -u origin feature/ABC-123-add-product-csv-export
# Create PR using template in GitHub/Azure DevOps

# 5. Address review comments
git add .
git commit -m "fix(product): address review feedback on CSV export"
git push

# 6. After approval, squash and merge via UI

# 7. Clean up local branch
git checkout main
git pull origin main
git branch -d feature/ABC-123-add-product-csv-export
```

---

## Quick Reference

```
┌─────────────────────────────────────────────────────────────┐
│                     BRANCH NAMING                           │
├─────────────────────────────────────────────────────────────┤
│  feature/description      New features                      │
│  bugfix/description       Bug fixes                         │
│  hotfix/description       Urgent production fixes           │
│  release/x.y.z            Release preparation               │
├─────────────────────────────────────────────────────────────┤
│                     COMMIT FORMAT                           │
├─────────────────────────────────────────────────────────────┤
│  feat(scope): subject     New feature                       │
│  fix(scope): subject      Bug fix                           │
│  docs(scope): subject     Documentation                     │
│  test(scope): subject     Tests                             │
├─────────────────────────────────────────────────────────────┤
│                     VERSION FORMAT                          │
├─────────────────────────────────────────────────────────────┤
│  vMAJOR.MINOR.PATCH       v1.2.3                           │
│  Breaking = MAJOR++       v2.0.0                           │
│  Feature = MINOR++        v1.3.0                           │
│  Fix = PATCH++            v1.2.4                           │
└─────────────────────────────────────────────────────────────┘
```
