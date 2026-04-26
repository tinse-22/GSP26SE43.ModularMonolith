# AGENTS.md

## Purpose

This file defines mandatory repo rules for any AI agent working in `D:\GSP26SE43.ModularMonolith`.

The two main goals are:

1. Never leave EF Core migrations out of date.
2. Never add a module, host dependency, or runtime service without updating Docker and compose wiring.

If an agent skips these checks, the task is not considered complete.

---

## 1) Mandatory Start-Of-Task Gate

Before making changes, the agent must classify the task into one or more of these buckets:

- `Docs only`
- `Application code only`
- `Touches EF model / DbContext / migration / seed / connection settings`
- `Touches module registration / project references`
- `Touches Docker / compose / runtime wiring`

If the task touches any item except `Docs only`, the agent must actively check whether migration verification and Docker registration checks are required.

The agent must not assume:

- migrations are already current
- Dockerfiles are already updated
- `docker-compose.yml` already contains the needed env vars, health checks, or dependencies

The agent must verify.

---

## 2) Mandatory Completion Gate For Every Code Task

Before the agent finishes any code task, it must answer all of these:

1. Are EF Core migrations still up to date?
2. If a new module, new project reference, or new runtime dependency was added, was Docker updated everywhere it needs to be?
3. If compose wiring changed, does `docker-compose.yml` still parse correctly?

If any answer is "I do not know", the task is not done.

---

## 3) Mandatory Migration Freshness Check

Scope: required whenever a task changes any of the following:

- `Entities/`
- `DbConfigurations/`
- any `DbContext`
- EF mapping
- seed data
- migration files
- module options affecting a DbContext
- service registration for a module that owns a DbContext

### Required commands

Run these commands unless the user explicitly forbids running them:

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
```

```powershell
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

Notes:

- `--verify-migrations` is the repo-standard migration freshness gate.
- If the build output path changes, the agent may use the equivalent compiled DLL path or `dotnet run --project ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj -- --verify-migrations`.
- If verification fails, the agent must either:
  - add the required migration, or
  - explain exactly why the model was not expected to change and what blocked verification

### Hard rule

Do not claim a DB-related task is complete if migration verification was not run or failed, unless you explicitly report the blocker.

---

## 4) Single Source Of Truth For Target DB

Scope: required for any task that reads or writes DB schema, migrations, seed data, or connection settings.

- Use `ConnectionStrings__Default` as the target connection string.
- Treat `.env` or `.env.docker` as primary for local/dev runs.
- Do not introduce or keep unrelated hardcoded DB names in host `appsettings.json`.
- If a host loads `.env` (WebAPI, Background, Migrator), keep behavior consistent across hosts.
- Do not validate DB state in mixed runtime modes. Use one mode only: `AppHost` or standalone Docker/local host mode.

---

## 5) Mandatory Preflight Before Applying Migrations To A Live DB

These steps are required before running migrations or seed changes against a real database:

1. Confirm runtime mode: use one mode only.
2. Confirm the exact target DB from `ConnectionStrings__Default`.
3. Run and record:

```sql
SELECT current_database();
SELECT current_schema();
SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;
```

### Hard prohibitions

- Do not run migrations when the target DB cannot be identified with certainty.
- Do not query only `public` for module tables.
- Do not assume module tables live in `public`.
- Always query module data using schema-qualified names.

---

## 6) Mandatory Post-Change DB Verification

After applying migrations or seed changes to a live DB:

1. Run the migrator against the intended DB only.
2. Verify the expected migration ID:

```sql
SELECT "MigrationId" FROM public."__EFMigrationsHistory"
WHERE "MigrationId" = '<ExpectedMigrationId>';
```

3. Verify expected seed/data in the correct schema.
4. If expected data is missing, first check:
   - wrong database
   - wrong runtime mode
   - wrong `.env` or `.env.docker`

### Required reporting in the final response

- exact target DB name used
- exact schemas checked
- migration IDs applied or missing
- any step that was not executed

---

## 7) New Module Checklist

Scope: required whenever creating `ClassifiedAds.Modules.Xxx`.

If the module owns a `DbContext`, all items below are mandatory.

### A. Required module structure

Create:

```text
ClassifiedAds.Modules.Xxx/
|-- ConfigurationOptions/
|   |-- ConnectionStringsOptions.cs
|   `-- XxxModuleOptions.cs
|-- Entities/
|-- DbConfigurations/
|-- Persistence/
|   |-- XxxDbContext.cs
|   `-- Repository.cs
`-- ServiceCollectionExtensions.cs
```

Requirements:

- `XxxDbContext` must use `HasDefaultSchema("xxx")`
- `ServiceCollectionExtensions.cs` must expose:
  - `AddXxxModule(Action<XxxModuleOptions> configureOptions)`
  - `MigrateXxxDb(this IHost host)`

### B. Required migrator updates

Update all of these:

1. `ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj`
2. `ClassifiedAds.Migrator/Program.cs` DI registration block
3. `ClassifiedAds.Migrator/Program.cs` migration execution block
4. `ClassifiedAds.Migrator/appsettings.json` under `"Modules"`

Use this DI pattern:

```csharp
.AddXxxModule(opt =>
{
    configuration.GetSection("Modules:Xxx").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Xxx.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
    opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
})
```

### C. Required initial migration

Generate the initial migration in `ClassifiedAds.Migrator` only:

```powershell
dotnet ef migrations add InitialXxx `
  --context XxxDbContext `
  --project ClassifiedAds.Migrator `
  --startup-project ClassifiedAds.Migrator `
  --output-dir Migrations/Xxx
```

Verify:

- `<Timestamp>_InitialXxx.cs`
- `<Timestamp>_InitialXxx.Designer.cs`
- `XxxDbContextModelSnapshot.cs`

### D. Required Docker updates

Every module with a DbContext must be wired into Docker restore layers correctly.

Mandatory Dockerfile updates:

- `ClassifiedAds.Migrator/Dockerfile` always
- `ClassifiedAds.WebAPI/Dockerfile` if WebAPI references the module
- `ClassifiedAds.Background/Dockerfile` if Background references the module

Add the module copy line before `dotnet restore`:

```dockerfile
COPY ./ClassifiedAds.Modules.Xxx/*.csproj ./ClassifiedAds.Modules.Xxx/
```

### E. Required host registration if consumed by WebAPI or Background

If the module exposes controllers, services, hosted workers, repositories, handlers, or contracts used by a host, update:

1. host `.csproj`
2. host `Program.cs` MVC chain
3. host `Program.cs` service chain
4. host `appsettings.json` under `"Modules"`
5. host `Dockerfile`
6. `docker-compose.yml` if new env vars or runtime deps are needed

For WebAPI, both chains are mandatory:

```csharp
services.AddControllers(...)
    .AddXxxModule();

services
    .AddXxxModule(opt =>
    {
        configuration.GetSection("Modules:Xxx").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Xxx.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    });
```

### Hard rule

Do not add a module reference in a host without also checking whether its Dockerfile restore layer and compose env need updates.

---

## 8) Existing Module Model Change Checklist

Scope: required when changing entities, EF configuration, `DbSet`, or `DbContext` behavior.

Required steps:

1. Make code changes.
2. Generate a new migration in `ClassifiedAds.Migrator`.
3. Review the generated migration for correctness and data loss risk.
4. Build the migrator.
5. Run migration freshness verification.

Command pattern:

```powershell
dotnet ef migrations add <MigrationName> `
  --context XxxDbContext `
  --project ClassifiedAds.Migrator `
  --startup-project ClassifiedAds.Migrator `
  --output-dir Migrations/Xxx
```

Migration naming should be descriptive:

- `AddTestCaseDependencies`
- `UpdateUserProfileFields`
- `RemoveDeprecatedColumns`

---

## 9) Mandatory Docker Registration Check

Scope: required whenever any of these happen:

- a new module is added
- a host adds a new project reference
- a host starts consuming a different module
- a new executable service or container is added
- a runtime dependency changes env vars, volumes, ports, healthchecks, or startup order

### Always inspect these files

- `ClassifiedAds.Migrator/Dockerfile`
- `ClassifiedAds.WebAPI/Dockerfile`
- `ClassifiedAds.Background/Dockerfile`
- `docker-compose.yml`

### Required checks

#### A. Dockerfile restore layer completeness

If a project references `ClassifiedAds.Modules.Xxx`, its Dockerfile must include:

```dockerfile
COPY ./ClassifiedAds.Modules.Xxx/*.csproj ./ClassifiedAds.Modules.Xxx/
```

before the relevant `dotnet restore`.

#### B. Compose runtime wiring completeness

If a service is added or its runtime dependencies change, verify:

- service exists in `docker-compose.yml`
- required env vars are present
- ports are present if externally needed
- volumes are present if persistence is needed
- `depends_on` is correct
- healthcheck exists where startup ordering depends on readiness

#### C. DB-aware startup ordering

If a service needs the database, it must depend on `db` health:

```yaml
depends_on:
  db:
    condition: service_healthy
```

If a service needs migrated schema before startup, it must wait for migrator success:

```yaml
depends_on:
  migrator:
    condition: service_completed_successfully
```

#### D. Preserve existing repo guarantees

Do not remove these guarantees without replacing them with an equivalent or stronger mechanism:

- `db` healthcheck
- `migrator` waiting for healthy DB
- `webapi` waiting for successful migrator completion
- `background` waiting for successful migrator completion
- migrator Docker build running migration verification

---

## 10) Mandatory Verification Before Finishing A Module / Docker / DB Task

Run the checks that apply:

### Migration verification

```powershell
dotnet build 'ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj' --no-restore
```

```powershell
dotnet 'ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll' --verify-migrations
```

### Compose verification

```powershell
docker compose config
```

### Docker builds

If Docker-related files changed, run the relevant builds:

```powershell
docker compose build migrator
docker compose build webapi
docker compose build background
```

If Docker daemon is unavailable, say so explicitly in the final response.

---

## 11) Fast Search Commands Agents Should Use

Use these when checking completeness:

### List module extension files

```powershell
rg --files | rg "ClassifiedAds\.Modules\..*ServiceCollectionExtensions\.cs$"
```

### Check whether a module is wired into hosts and Docker

```powershell
rg -n "Xxx" ClassifiedAds.Migrator ClassifiedAds.WebAPI ClassifiedAds.Background docker-compose.yml
```

### Check Dockerfile module copy lines

```powershell
rg -n "ClassifiedAds\.Modules\.Xxx" ClassifiedAds.Migrator/Dockerfile ClassifiedAds.WebAPI/Dockerfile ClassifiedAds.Background/Dockerfile
```

---

## 12) Required Final Response Format For Relevant Tasks

If the task touched DB, module wiring, project references, or Docker, the agent must explicitly state:

- whether migration verification was run
- exact migration verification command used
- whether Docker registration was checked
- exact Docker or compose commands used
- whether Docker daemon was available
- whether any step was skipped

For live DB work, also state:

- exact target DB name
- exact schemas checked
- migration IDs applied or missing

---

## 13) Hard Prohibitions

- Do not create or modify EF models without checking migration freshness.
- Do not create a module with a DbContext without generating its initial migration.
- Do not modify entities or EF configuration without generating a migration when the model changed.
- Do not add a host project reference without checking that the host Dockerfile restore layer includes the referenced project/module.
- Do not add a module to a host in code and forget to inspect Docker and compose.
- Do not register a module in only one DI chain when both are required.
- Do not claim "done" if migration verification or Docker registration verification was not addressed.
- Do not silently skip `docker-compose.yml` when runtime dependencies changed.

---

## 14) Repo-Specific Rule Summary

In this repo, every AI agent must treat these as default gates:

1. `ClassifiedAds.Migrator` is the only place migrations are created.
2. `ConnectionStrings__Default` is the DB source of truth.
3. `ClassifiedAds.Migrator` must stay able to run `--verify-migrations`.
4. Docker restore layers must include every referenced project/module.
5. `docker-compose.yml` must preserve DB health and migrator-before-host startup ordering.

If a change threatens any of those rules, stop and fix the wiring before finishing.

---

## 15) Mandatory GitNexus Usage Gate

Scope: required for any task except `Docs only`, whenever GitNexus MCP is available in the current agent/session.

- The GitNexus instruction block below must be treated as mandatory guidance, not optional reference material.
- Before changing code, the agent must use GitNexus to load repo context relevant to the task. At minimum, use one of these:
  - `gitnexus://repo/GSP26SE43.ModularMonolith/context`
  - `gitnexus_query({query: "..."})`
  - `gitnexus_context({name: "..."})`
- Before editing an existing symbol or refactoring behavior, the agent must run `gitnexus_impact({target: "...", direction: "upstream"})`.
- Before finishing a code task or creating a commit, the agent must run `gitnexus_detect_changes(...)` on the relevant scope.
- If GitNexus reports that the index is stale, the agent must refresh it with `npx gitnexus analyze` before relying on the result.
- If GitNexus is unavailable in the current client/session, the agent must say so explicitly, fall back to manual inspection, and mention that GitNexus could not be used.

### Hard rule

Do not claim a non-docs code task is fully analyzed if GitNexus was available but never consulted.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **GSP26SE43.ModularMonolith** (2492 symbols, 2577 relationships, 0 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## When Debugging

1. `gitnexus_query({query: "<error or symptom>"})` — find execution flows related to the issue
2. `gitnexus_context({name: "<suspect function>"})` — see all callers, callees, and process participation
3. `READ gitnexus://repo/GSP26SE43.ModularMonolith/process/{processName}` — trace the full execution flow step by step
4. For regressions: `gitnexus_detect_changes({scope: "compare", base_ref: "main"})` — see what your branch changed

## When Refactoring

- **Renaming**: MUST use `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` first. Review the preview — graph edits are safe, text_search edits need manual review. Then run with `dry_run: false`.
- **Extracting/Splitting**: MUST run `gitnexus_context({name: "target"})` to see all incoming/outgoing refs, then `gitnexus_impact({target: "target", direction: "upstream"})` to find all external callers before moving code.
- After any refactor: run `gitnexus_detect_changes({scope: "all"})` to verify only expected files changed.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Tools Quick Reference

| Tool | When to use | Command |
|------|-------------|---------|
| `query` | Find code by concept | `gitnexus_query({query: "auth validation"})` |
| `context` | 360-degree view of one symbol | `gitnexus_context({name: "validateUser"})` |
| `impact` | Blast radius before editing | `gitnexus_impact({target: "X", direction: "upstream"})` |
| `detect_changes` | Pre-commit scope check | `gitnexus_detect_changes({scope: "staged"})` |
| `rename` | Safe multi-file rename | `gitnexus_rename({symbol_name: "old", new_name: "new", dry_run: true})` |
| `cypher` | Custom graph queries | `gitnexus_cypher({query: "MATCH ..."})` |

## Impact Risk Levels

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers/importers | MUST update these |
| d=2 | LIKELY AFFECTED — indirect deps | Should test |
| d=3 | MAY NEED TESTING — transitive | Test if critical path |

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/GSP26SE43.ModularMonolith/context` | Codebase overview, check index freshness |
| `gitnexus://repo/GSP26SE43.ModularMonolith/clusters` | All functional areas |
| `gitnexus://repo/GSP26SE43.ModularMonolith/processes` | All execution flows |
| `gitnexus://repo/GSP26SE43.ModularMonolith/process/{name}` | Step-by-step execution trace |

## Self-Check Before Finishing

Before completing any code modification task, verify:
1. `gitnexus_impact` was run for all modified symbols
2. No HIGH/CRITICAL risk warnings were ignored
3. `gitnexus_detect_changes()` confirms changes match expected scope
4. All d=1 (WILL BREAK) dependents were updated

## Keeping the Index Fresh

After committing code changes, the GitNexus index becomes stale. Re-run analyze to update it:

```bash
npx gitnexus analyze
```

If the index previously included embeddings, preserve them by adding `--embeddings`:

```bash
npx gitnexus analyze --embeddings
```

To check whether embeddings exist, inspect `.gitnexus/meta.json` — the `stats.embeddings` field shows the count (0 means no embeddings). **Running analyze without `--embeddings` will delete any previously generated embeddings.**

> Claude Code users: A PostToolUse hook handles this automatically after `git commit` and `git merge`.

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
