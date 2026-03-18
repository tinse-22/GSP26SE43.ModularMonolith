# AGENTS.md

## Mandatory Rules For Any AI Agent Touching Database

Scope: these rules are REQUIRED for any task that reads or writes DB schema, migrations, seed data, or connection settings.

### 1) Single Source Of Truth For Target DB

- Use `ConnectionStrings__Default` as the target connection string.
- Treat `.env` or `.env.docker` as primary for local/dev runs.
- Do not introduce or keep unrelated hardcoded DB names in host `appsettings.json`.
- If a host loads `.env` (WebAPI/Background/Migrator), keep behavior consistent across hosts.

### 2) Mandatory Preflight Before DB Changes

- Confirm runtime mode: use ONE mode only (`AppHost` OR standalone).
- Confirm target DB explicitly before running migration/seed.
- Run and record:
- `SELECT current_database();`
- `SELECT current_schema();`
- `SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;`

### 3) Migration And Seed Rules

- Create EF migrations only in `ClassifiedAds.Migrator`.
- Keep module schemas explicit (`identity`, `subscription`, `configuration`, etc.).
- For data checks, always query schema-qualified table names.
- Never assume module tables are in `public`.

### 4) Mandatory Post-Change Verification

- Run migrator against the intended DB only.
- Verify migration was applied:
- `SELECT "MigrationId" FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '<ExpectedMigrationId>';`
- Verify expected seed/data in correct schema with row counts.
- If expected data is missing, check DB mismatch first (wrong database, wrong runtime mode, wrong env).

### 5) Required Reporting In Agent Response

- Always state exact target DB name used.
- Always state exact schemas checked.
- Always include migration IDs applied or missing.
- If any step was not executed, state it explicitly.

### 6) Hard Prohibitions

- Do not run migrations when target DB cannot be identified with certainty.
- Do not query only `public` schema for module tables.
- Do not run mixed runtime modes concurrently when validating DB state.

---

## Mandatory Rules For Creating Or Modifying A Module

Scope: these rules are REQUIRED whenever you create a new module (`ClassifiedAds.Modules.Xxx`), add/change entities in an existing module's DbContext, or modify any module's project references. Skipping any step **will** break the build, Migrator, or Docker.

### 7) New Module Checklist (All Steps Required)

When creating a new module `ClassifiedAds.Modules.Xxx` that has its own `XxxDbContext`:

#### A. Module Project Structure

Create the module project with these mandatory folders/files:

```
ClassifiedAds.Modules.Xxx/
├── ConfigurationOptions/
│   ├── ConnectionStringsOptions.cs
│   └── XxxModuleOptions.cs
├── Entities/                    # Domain entities
├── DbConfigurations/            # IEntityTypeConfiguration<T> per entity
├── Persistence/
│   ├── XxxDbContext.cs          # Must use HasDefaultSchema("xxx")
│   └── Repository.cs
└── ServiceCollectionExtensions.cs
```

`ServiceCollectionExtensions.cs` must expose:
- `AddXxxModule(Action<XxxModuleOptions> configureOptions)` - DI registration
- `MigrateXxxDb(this IHost host)` - Migration extension for Migrator

#### B. Migrator Project — 4 Mandatory Updates

| # | File | Action |
|---|------|--------|
| 1 | `ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj` | Add `<ProjectReference Include="..\ClassifiedAds.Modules.Xxx\ClassifiedAds.Modules.Xxx.csproj" />` |
| 2 | `ClassifiedAds.Migrator/Program.cs` — DI block | Add `.AddXxxModule(opt => { ... })` following the existing pattern (bind config, set ConnectionStrings.Default, set MigrationsAssembly) |
| 3 | `ClassifiedAds.Migrator/Program.cs` — Migration block | Add `app.MigrateXxxDb();` inside the Polly retry block |
| 4 | `ClassifiedAds.Migrator/appsettings.json` | Add `"Xxx": {}` under `"Modules"` section (even if empty) |

**DI registration pattern** (copy exactly):
```csharp
// Xxx Module
.AddXxxModule(opt =>
{
    configuration.GetSection("Modules:Xxx").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Xxx.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
    opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
})
```

#### C. EF Core Migration — Generate Initial Migration

After completing steps A and B, generate the initial migration:

```bash
dotnet ef migrations add InitialXxx \
  --context XxxDbContext \
  --project ClassifiedAds.Migrator \
  --startup-project ClassifiedAds.Migrator \
  --output-dir Migrations/Xxx
```

This creates files in `ClassifiedAds.Migrator/Migrations/Xxx/`. **Verify**:
- `<Timestamp>_InitialXxx.cs` — Up/Down methods exist
- `<Timestamp>_InitialXxx.Designer.cs` — snapshot metadata
- `XxxDbContextModelSnapshot.cs` — current model state

#### D. Docker — Update ALL Dockerfiles That Reference The Module

Each Dockerfile has a "Copy csproj" layer for `dotnet restore` caching. If the module is referenced by a host project, its `.csproj` **must** be copied in that layer, otherwise `dotnet restore` fails.

| Dockerfile | When to update |
|------------|---------------|
| `ClassifiedAds.Migrator/Dockerfile` | **Always** (every module with a DbContext must be in Migrator) |
| `ClassifiedAds.WebAPI/Dockerfile` | If WebAPI references the module |
| `ClassifiedAds.Background/Dockerfile` | If Background references the module |

**Add this line** in the "Copy csproj" section (before `RUN dotnet restore`):
```dockerfile
COPY ./ClassifiedAds.Modules.Xxx/*.csproj ./ClassifiedAds.Modules.Xxx/
```

#### E. Host Projects — If Module Is Consumed By WebAPI Or Background

If the module exposes controllers, services, or background workers:

| # | File | Action |
|---|------|--------|
| 1 | Host `.csproj` | Add `<ProjectReference>` to the module |
| 2 | Host `Program.cs` — **IMvcBuilder chain** | Add `.AddXxxModule()` to the `AddControllers()` fluent chain (registers controllers via `ApplicationPart`) |
| 3 | Host `Program.cs` — **IServiceCollection chain** | Add `.AddXxxModule(opt => { ... })` to the services chain (registers DbContext, repositories, message handlers) |
| 4 | Host `appsettings.json` | Add `"Xxx": {}` under `"Modules"` section (even if empty — config binding requires the section to exist) |
| 5 | Host `Dockerfile` | Add `COPY` line for the module's `.csproj` (see D above) |
| 6 | `docker-compose.yml` | Add any module-specific environment variables if needed |

**WebAPI DI registration pattern** (both chains are REQUIRED):

```csharp
// 1. IMvcBuilder chain — in AddControllers() fluent call
services.AddControllers(...)
    .AddXxxModule()   // <-- ADD THIS LINE
    // ... other modules

// 2. IServiceCollection chain — after AddControllers block
services
    .AddXxxModule(opt =>
    {
        configuration.GetSection("Modules:Xxx").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Xxx.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    // ... other modules
```

> **CRITICAL**: A module that is referenced in `.csproj` but NOT registered in BOTH chains will compile successfully but **fail at runtime** with `Unable to resolve service` errors. Always register in BOTH the IMvcBuilder chain AND the IServiceCollection chain.

### 8) Existing Module Model Change Checklist

When modifying entities, DbConfigurations, or DbSet properties in an existing module's DbContext:

| # | Action | Command / File |
|---|--------|----------------|
| 1 | Make code changes | Edit entities, DbConfigurations, DbContext |
| 2 | Generate new migration | `dotnet ef migrations add <MigrationName> --context XxxDbContext --project ClassifiedAds.Migrator --startup-project ClassifiedAds.Migrator --output-dir Migrations/Xxx` |
| 3 | Review generated migration | Check the `.cs` file — verify Up/Down are correct, check for data loss warnings |
| 4 | Build Migrator | `dotnet build ClassifiedAds.Migrator` — must succeed with 0 errors |
| 5 | Test migration | Run Migrator against dev DB, verify no `PendingModelChangesWarning` |

**Migration naming convention**: Use descriptive names that reflect the change:
- `AddTestCaseDependencies` — adding new table
- `UpdateUserProfileFields` — modifying columns
- `RemoveDeprecatedColumns` — dropping columns

### 9) Verification Before Committing

Run these checks before committing any module-related changes:

```bash
# 1. Build entire solution
dotnet build

# 2. Verify no pending model changes for any DbContext
dotnet ef migrations has-pending-model-changes \
  --context XxxDbContext \
  --project ClassifiedAds.Migrator \
  --startup-project ClassifiedAds.Migrator

# 3. Docker build (if Dockerfiles were changed)
docker compose build migrator
docker compose build webapi
docker compose build background
```

#### DI Registration Completeness Check (MANDATORY)

Before committing, verify **every** module with a `ServiceCollectionExtensions.cs` is registered in all host projects that reference it:

1. List all modules that have `ServiceCollectionExtensions.cs`:
   ```bash
   find . -path "*/ClassifiedAds.Modules.*/ServiceCollectionExtensions.cs" -type f
   ```

2. For each module found, verify it appears in **ALL THREE** locations in the host `Program.cs`:
   - [ ] `.AddXxxModule()` in the **IMvcBuilder chain** (AddControllers fluent call)
   - [ ] `.AddXxxModule(opt => {...})` in the **IServiceCollection chain**
   - [ ] `"Xxx": {}` section exists in the host's **appsettings.json** under `"Modules"`

3. If any module is missing from any location, **add it immediately** — do not commit with incomplete registrations.

### 10) Common Mistakes — Hard Prohibitions

- **Do not** create a module with a DbContext without generating its initial migration.
- **Do not** modify entities/DbConfigurations without generating a new migration.
- **Do not** add a `<ProjectReference>` to a host `.csproj` without adding the matching `COPY` line in that host's `Dockerfile`.
- **Do not** register a module in `Program.cs` without adding its `<ProjectReference>` in `.csproj`.
- **Do not** forget `appsettings.json` — every module registered in Migrator or WebAPI must have a `"Modules:Xxx"` section.
- **Do not** hardcode connection strings in module options — always use `sharedConnectionString` from `ConnectionStrings:Default`.
- **Do not** set `MigrationsAssembly` to the module project — it must always be `Assembly.GetExecutingAssembly().GetName().Name` (i.e., `ClassifiedAds.Migrator`).
- **Do not** register a module in only ONE chain (IMvcBuilder or IServiceCollection) — BOTH chains are required. Missing IMvcBuilder = controllers not discovered. Missing IServiceCollection = services not resolved at runtime.
- **Do not** create a module with `ServiceCollectionExtensions.cs` without immediately registering it in the host `Program.cs` — unregistered modules are silent runtime failures that compile without errors.

