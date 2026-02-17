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

