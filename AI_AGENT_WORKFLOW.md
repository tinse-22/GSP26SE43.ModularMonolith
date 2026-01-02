# AI Agent Workflow Guidelines

## ⚠️ CRITICAL: Pre-Implementation Checklist

Before implementing **ANY** new feature, bug fix, or code change, AI Agents **MUST** follow these steps:

## 1. Fetch/Pull Latest Code

Always start by ensuring you have the latest codebase:

```powershell
# Fetch all remote changes
git fetch origin

# Pull the latest changes from the current branch
git pull origin <current-branch-name>
```

### Why This Matters:
- Prevents merge conflicts
- Ensures you're working with the most recent code
- Avoids duplicate work or overwriting recent changes
- Maintains code synchronization across the team

## 2. Branch Strategy

### For New Features:
```powershell
# Create a new feature branch from the latest main/develop
git checkout -b feature/<feature-name>
```

### For Bug Fixes:
```powershell
# Create a new bugfix branch
git checkout -b bugfix/<bug-description>
```

### For Hot Fixes:
```powershell
# Create a new hotfix branch
git checkout -b hotfix/<issue-description>
```

### Branch Naming Conventions:
- Use lowercase with hyphens
- Be descriptive and concise
- Include issue/ticket numbers if applicable
- Examples:
  - `feature/add-user-authentication`
  - `bugfix/fix-null-reference-error`
  - `hotfix/critical-security-patch`

## 3. Verification Steps

Before starting implementation, verify:

```powershell
# Check current branch
git branch --show-current

# Check status for any uncommitted changes
git status

# View recent commits to confirm you're up-to-date
git log --oneline -5
```

## 4. Implementation Workflow

1. ✅ **Fetch/Pull** latest code
2. ✅ **Create or Switch** to appropriate branch
3. ✅ **Verify** you're on the correct branch
4. ✅ **Implement** the feature/fix
5. ✅ **Test** your changes
6. ✅ **Commit** with descriptive messages
7. ✅ **Push** to remote repository

## 5. DO NOT Proceed If:

- ❌ You haven't fetched/pulled the latest code
- ❌ You're working directly on `main`, `master`, or `develop` branch (unless explicitly instructed)
- ❌ There are uncommitted changes that might conflict
- ❌ The branch name doesn't follow conventions

## 6. Exception Handling

If git operations fail:

```powershell
# If pull fails due to conflicts, stash changes first
git stash
git pull origin <branch-name>
git stash pop

# If branch creation fails, check if it already exists
git branch -a | grep <branch-name>
```

## 7. Best Practices

- **Always** communicate branch changes in commit messages
- **Never** force push unless absolutely necessary and approved
- **Keep** branches short-lived and focused
- **Sync** frequently with the remote repository
- **Clean up** merged branches after completion

---

## Quick Reference Command Sequence

```powershell
# Standard workflow for new feature
git fetch origin
git pull origin develop
git checkout -b feature/my-new-feature
# ... make your changes ...
git add .
git commit -m "feat: implement my new feature"
git push origin feature/my-new-feature
```

---

**Remember**: This workflow is **MANDATORY** for all AI Agent implementations. Skipping these steps can lead to lost work, merge conflicts, and code inconsistencies.
