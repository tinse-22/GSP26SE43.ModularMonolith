# Identity Register ManualResetEventSlim Analysis Report

Date: 2026-04-11
Scope: Register crash analysis and implementation graph review
Task classification: Docs only
GitNexus status: available and up-to-date in this session

GitNexus runtime status snapshot:
- Repository: D:\GSP26SE43.ModularMonolith
- Indexed commit: ec12d07
- Current commit: ec12d07
- Index state: up-to-date

## 1) Executive summary

The Register crash is confirmed and reproducible in logs.

Primary observation:
- The exception is System.ObjectDisposedException with object name System.Threading.ManualResetEventSlim, thrown while EF Core is querying identity.Users during UserManager.CreateAsync validation flow.

Most likely root cause:
- Connection/provider-layer instability on Supabase pooler path (Npgsql connector lifecycle issue), not classic DI scope misuse in AuthController.

Why this conclusion is strong:
- Same exception signature appears in multiple modules, not only Identity.
- Logs show runtime using Supabase pooler host.
- GitNexus symbol/process graph confirms Register and token-flow implementation boundaries and blast radius.

## 2) GitNexus implementation + `implements` analysis

### 2.1 Register implementation graph

- Symbol confirmed by GitNexus: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L93)
- Method-level impact from GitNexus: `LOW` risk, `impactedCount=2` (mainly test + one low-confidence call edge)
- Incoming caller validated in tests: [ClassifiedAds.UnitTests/Identity/AuthControllerTests.cs](ClassifiedAds.UnitTests/Identity/AuthControllerTests.cs#L90)
- Outgoing implementation anchors in Register:
  - Execution strategy start: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L123)
  - User creation: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L130)
  - Role assignment: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L152)
  - Profile creation path: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L159)

GitNexus caveat:
- `StartAsync -> Register` relation from [ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs](ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs#L28) is likely a name-collision edge (`ApplicationStarted.Register(...)`) at [ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs](ClassifiedAds.Infrastructure/HostedServices/HostApplicationLifetimeEventsHostedService.cs#L36), not a real runtime invocation of `AuthController.Register`.

### 2.2 `implements` chain for token flow

- Contract: [ClassifiedAds.Modules.Identity/Services/IJwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/IJwtTokenService.cs#L9)
- Main implementation: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L20)
- DI binding (`IJwtTokenService -> JwtTokenService`): [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L57)

GitNexus call-graph highlights:
- `GenerateTokensAsync` implementation: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L45)
- Called by Login: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L272)
- Also reached from refresh rotation path via `ValidateAndRotateRefreshTokenAsync`:
  - Service method: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L242)
  - Controller call: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L316)
- Method-level impact for `GenerateTokensAsync`: `LOW` risk with affected processes in Login + RefreshToken.
- Class-level impact for `JwtTokenService`: `MEDIUM` risk (`impactedCount=8`) due broader usage/import spread.

## 3) Critical findings (ordered by severity)

1. Critical: Register fails during query path, not only save path
- Evidence query: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L13600)
- Evidence query source table: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L13601)
- Exception and stack to UserManager.CreateAsync in Register: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L13657)
- Register execution strategy entry in controller: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L123)

2. Critical: Same ManualResetEventSlim ObjectDisposedException exists outside Register/Identity
- Subscription module query failure with same signature: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L11349)
- Stack crosses Subscription command handler, proving systemic connector issue: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L11396)

3. High: Runtime is observed on Supabase pooler host
- Connection error references pooler host: [ClassifiedAds.WebAPI/logs/log_001.txt](ClassifiedAds.WebAPI/logs/log_001.txt#L12896)
- AppHost warning already documents pooler risk behavior: [ClassifiedAds.AppHost/Program.cs](ClassifiedAds.AppHost/Program.cs#L67)

4. High: AppHost has local-auto capability, but launch profiles still force external DB mode
- Auto/local intent is explicit in resolver comment: [ClassifiedAds.AppHost/Program.cs](ClassifiedAds.AppHost/Program.cs#L251)
- Resolver entry point: [ClassifiedAds.AppHost/Program.cs](ClassifiedAds.AppHost/Program.cs#L239)
- However launch profiles hard-set external mode in both profiles:
  - [ClassifiedAds.AppHost/Properties/launchSettings.json](ClassifiedAds.AppHost/Properties/launchSettings.json#L15)
  - [ClassifiedAds.AppHost/Properties/launchSettings.json](ClassifiedAds.AppHost/Properties/launchSettings.json#L29)

5. Medium: Pooler hardening is present but still partial
- Identity module hardens connection string + retry behavior:
  - BuildIdentityConnectionString: [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L220)
  - Disable pooling on Supabase pooler host: [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L235)
  - Retry/max-batch tuning: [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L46)
- JwtTokenService adds explicit ManualResetEvent retry + dedicated non-pooled connection:
  - Retry wrapper: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L65)
  - Exception classifier: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L181)
  - Dedicated connection factory: [ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs](ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs#L410)
- Other DbContext modules still use direct `settings.ConnectionStrings.Default` in `UseNpgsql(...)` without shared pooler-normalization pattern:
  - [ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs#L33)
  - [ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs#L27)
  - [ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs#L27)
  - [ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs#L25)
  - [ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs#L29)

## 4) DI scope and async audit result

Checked hypotheses from common failure patterns:

- Singleton holding scoped DbContext in Register path:
  - Not found in Identity registration path for AuthController dependencies.
  - IdentityDbContext is added via AddDbContext (scoped) in module registration: [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L33)

- Missing await in Register flow:
  - Register path awaits all key async calls in sequence: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L130), [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L152), [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L159)

- Manual DbContext dispose in Register flow:
  - Not found in Register path.

Conclusion:
- The incident is not primarily explained by obvious DI lifetime misconfiguration in AuthController.Register.

## 5) Root cause assessment

Primary root cause (highest confidence):
- Npgsql connector-level instability under current DB endpoint/runtime conditions (Supabase pooler path), leading to ManualResetEventSlim ObjectDisposedException during EF query enumeration.

Secondary contributing factors:
- AppHost launch profiles still force external mode, increasing chance of hitting unstable remote path during normal local runs.
- Mitigation is not uniformly applied across all modules and code paths.
- Existing token-flow hardening reduces risk for refresh-token persistence but does not fully eliminate Register query-path failures.

## 6) Latent risks still present

1. Cross-module blast radius remains
- Evidence already exists in Subscription and Identity logs.

2. Operational fragility when external DB mode is implicit in developer profile
- Local troubleshooting can accidentally target remote pooler and produce non-deterministic failures.

3. Register still uses shared scoped context + UserManager path
- Not proven as root cause, but the path is still exposed to provider-level connector failures during pre-insert validation query.
- Reference location: [ClassifiedAds.Modules.Identity/Controllers/AuthController.cs](ClassifiedAds.Modules.Identity/Controllers/AuthController.cs#L123)

4. Test coverage gap for provider-level failure mode
- AuthController tests run with InMemory provider, not real Npgsql pooler behavior: [ClassifiedAds.UnitTests/Identity/AuthControllerTests.cs](ClassifiedAds.UnitTests/Identity/AuthControllerTests.cs#L44)
- Current JwtTokenService tests validate options/config primitives only (no integration assertions for retry/pooler faults): [ClassifiedAds.UnitTests/Identity/JwtTokenServiceTests.cs](ClassifiedAds.UnitTests/Identity/JwtTokenServiceTests.cs#L15)

## 7) Recommended fix plan

P0 (Immediate, operational)
- Stop using Supabase pooler endpoint for runtime paths that are currently unstable in this repo context.
- Prefer local DB mode for AppHost by default or explicit direct DB endpoint mode.

P1 (Immediate, configuration safety)
- Remove forced APPHOST_DATABASE_MODE external from local launch profiles unless explicitly needed.
- Keep external mode opt-in per session.

P2 (Short-term, code consistency)
- Centralize connection-string normalization for all modules (not only Identity), or centralize endpoint policy so all module DbContexts follow the same safe behavior.

P3 (Short-term, resilience/observability)
- Add structured logging at entry of Register/Login/RefreshToken with safe metadata about DB mode and host category (local, direct, pooler).
- Keep user-facing message generic, but internal logs should classify connector-level failures explicitly.

P4 (Medium-term, validation)
- Add targeted integration test matrix for Register and Login under selected DB modes:
  - local postgres
  - external direct endpoint
  - external pooler endpoint (if still supported)

## 8) GitNexus commands executed in this analysis

Executed successfully in this session:
- `npx gitnexus status`
- `npx gitnexus query "ManualResetEventSlim register identity"`
- `npx gitnexus query "AuthController Register UserManager CreateAsync"`
- `npx gitnexus context AuthController --file ClassifiedAds.Modules.Identity/Controllers/AuthController.cs`
- `npx gitnexus context Register --file ClassifiedAds.Modules.Identity/Controllers/AuthController.cs`
- `npx gitnexus impact Register --direction upstream --depth 3 --include-tests`
- `npx gitnexus context IJwtTokenService --file ClassifiedAds.Modules.Identity/Services/IJwtTokenService.cs`
- `npx gitnexus context JwtTokenService --file ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs`
- `npx gitnexus context GenerateTokensAsync --file ClassifiedAds.Modules.Identity/Services/JwtTokenService.cs`
- `npx gitnexus impact GenerateTokensAsync --direction upstream --depth 3 --include-tests`
- `npx gitnexus impact JwtTokenService --direction upstream --depth 3 --include-tests`

## 9) What was checked and what was not

Checked:
- AuthController Register flow and its transaction/retry pattern.
- AuthController/Login/RefreshToken call edges via GitNexus context/impact.
- IJwtTokenService -> JwtTokenService implementation binding and DI registration.
- Identity DI registration lifetimes.
- Runtime logs for stack traces and DB endpoint evidence.
- Cross-module occurrence of same exception signature.

Not executed in the initial docs-only analysis phase:
- No application/runtime code changes.
- No migration verification command run.
- No docker compose build/config command run.

## 10) Implementation status (updated)

Implemented in code after this analysis:
- Added shared connection-string normalizer for Supabase pooler: [ClassifiedAds.Persistence.PostgreSQL/PostgresConnectionStringNormalizer.cs](ClassifiedAds.Persistence.PostgreSQL/PostgresConnectionStringNormalizer.cs#L6)
- Wired module DbContexts to use shared normalizer in:
  - [ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Configuration/ServiceCollectionExtensions.cs#L23)
  - [ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.AuditLog/ServiceCollectionExtensions.cs#L24)
  - [ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Notification/ServiceCollectionExtensions.cs#L24)
  - [ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.ApiDocumentation/ServiceCollectionExtensions.cs#L25)
  - [ClassifiedAds.Modules.LlmAssistant/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.LlmAssistant/ServiceCollectionExtensions.cs#L23)
  - [ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.TestExecution/ServiceCollectionExtensions.cs#L24)
  - [ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.TestReporting/ServiceCollectionExtensions.cs#L23)
  - [ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Subscription/ServiceCollectionExtensions.cs#L34)
  - [ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Storage/ServiceCollectionExtensions.cs#L27)
  - [ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.TestGeneration/ServiceCollectionExtensions.cs#L28)
  - [ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs](ClassifiedAds.Modules.Identity/ServiceCollectionExtensions.cs#L29)
- Removed forced external DB mode from AppHost launch profiles: [ClassifiedAds.AppHost/Properties/launchSettings.json](ClassifiedAds.AppHost/Properties/launchSettings.json#L9)

Verification executed after implementation:
- `dotnet build "ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj" --no-restore` -> succeeded
- `dotnet "ClassifiedAds.Migrator/bin/Debug/net10.0/ClassifiedAds.Migrator.dll" --verify-migrations` -> `EF Core migration snapshots are up to date.`
