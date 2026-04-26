---
name: init
description: "Use when the user wants to bootstrap this .NET modular monolith repository, set up a new feature/module foundation, or initialize a fresh working area that follows the project's conventions."
---

# Init Skill

## When to Use

Use this skill when the user asks to:

- initialize a new project or module in this repository
- create the first production-ready skeleton for a feature
- set up folders, files, configuration, and conventions for new work
- bootstrap a new integration point, host, or shared component
- establish the minimum structure needed before implementing business logic

This repository is a .NET 10 modular monolith with multiple modules, shared building blocks, background workers, migrations, and Aspire orchestration. The goal of `/init` here is not to create a generic app scaffold, but to create a repo-aligned starting point that fits existing naming, layering, and infrastructure patterns.

## Project Context

The current codebase is an API testing automation platform organized around these areas:

- shared building blocks such as `ClassifiedAds.Application`, `ClassifiedAds.Domain`, and `ClassifiedAds.Infrastructure`
- contracts in `ClassifiedAds.Contracts`
- host projects such as `ClassifiedAds.WebAPI`, `ClassifiedAds.Background`, `ClassifiedAds.Migrator`, and `ClassifiedAds.AppHost`
- feature modules such as `ClassifiedAds.Modules.Identity`, `ClassifiedAds.Modules.Storage`, `ClassifiedAds.Modules.Notification`, `ClassifiedAds.Modules.AuditLog`, and `ClassifiedAds.Modules.Configuration`
- supporting tests and documentation in `ClassifiedAds.UnitTests`, `ClassifiedAds.IntegrationTests`, and `docs/`

When initializing something new, follow the same modular monolith rules:

- keep business logic inside the appropriate module
- use shared layers only for truly cross-cutting concerns
- prefer explicit contracts between modules
- preserve clean architecture boundaries
- align file names, namespaces, and folder structure with existing projects

## Workflow

1. Clarify the scope of the initialization
   - Is this a new module, a new host integration, a new service, or a shared building block?
   - Identify the minimum deliverable for the first working version.

2. Inspect the repository conventions before creating anything
   - Look for nearby modules or similar implementations.
   - Match folder naming, namespace naming, and project references.
   - Reuse existing abstractions where possible.

3. Define the bootstrap boundary
   - Decide which project owns the new code.
   - Decide which layers are needed now and which can wait.
   - Avoid creating folders or files that have no immediate purpose.

4. Create the initial structure
   - Add the required folders and source files.
   - Add project references only where needed.
   - Add interfaces, DTOs, services, handlers, or minimal endpoints as appropriate.
   - Add configuration placeholders if the feature depends on settings.

5. Wire the feature into the host or module composition
   - Register services in the correct dependency injection entry point.
   - Add module bootstrapping if the repository uses it.
   - Add message consumers, background jobs, or API endpoints only if the initial scope needs them.

6. Add supporting tests and documentation
   - Add at least one unit or integration test for the initialized surface when practical.
   - Update docs or README files if the new structure introduces usage or setup changes.
   - Keep docs short and actionable.

7. Validate the bootstrap
   - Ensure the solution still builds.
   - Run the most targeted tests available.
   - Check that the new files follow repository conventions.

## Checklist

- [ ] Confirm the target scope and owning project
- [ ] Review an existing similar module or component
- [ ] Identify required project references
- [ ] Create the minimum folder structure
- [ ] Add the initial code surface
- [ ] Register dependency injection and composition hooks
- [ ] Add configuration, if needed
- [ ] Add tests for the new surface
- [ ] Update documentation if initialization changes usage
- [ ] Verify build and targeted tests

## Bootstrap Guidelines for This Repository

### For a new module

- create the module under `ClassifiedAds.Modules.<Name>`
- keep the module self-contained whenever possible
- expose only necessary contracts through `ClassifiedAds.Contracts` or module interfaces
- add module-specific services, handlers, and persistence in that module
- connect the module to hosts only through explicit composition

### For a new host integration

- identify whether the integration belongs in `WebAPI`, `Background`, `AppHost`, or `Migrator`
- keep orchestration logic out of the domain
- register the integration in the host startup/composition layer
- add config keys to the appropriate settings source

### For shared infrastructure

- put cross-cutting code in the shared building blocks only if it is truly reusable
- avoid moving domain-specific logic into shared layers
- keep abstractions small and intentional

### For database or migration initialization

- follow the existing PostgreSQL and EF Core conventions
- create schema or migration artifacts in the appropriate persistence/migrator project
- keep seed data and schema setup minimal unless the feature requires more

## Common Actions

- create a new module skeleton
- add a service interface and implementation pair
- create DTOs and contracts
- add initial API endpoint(s)
- register a background worker or message consumer
- set up configuration sections and options classes
- add migration scaffolding for a new schema
- create test fixtures and initial coverage

## Quality Bar

A good `/init` result in this repository should be:

- minimal but usable
- consistent with the existing modular monolith structure
- easy to extend without rework
- testable from the start
- documented enough for the next implementation step

## Notes

- Prefer the smallest useful scaffold over a full feature dump.
- Reuse existing patterns from nearby modules before inventing new ones.
- If the request is ambiguous, ask whether the user wants a module, host integration, shared library, or test/bootstrap setup.
- Do not initialize files or projects that are not clearly part of the requested scope.
