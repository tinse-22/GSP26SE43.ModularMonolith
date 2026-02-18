# Appendix - Glossary

> **Purpose**: Definitions of key terms, concepts, and abbreviations used throughout the architecture documentation.

---

## Table of Contents

- [A](#a) | [B](#b) | [C](#c) | [D](#d) | [E](#e) | [F](#f) | [G](#g) | [H](#h) | [I](#i) | [J](#j)
- [M](#m) | [O](#o) | [P](#p) | [Q](#q) | [R](#r) | [S](#s) | [T](#t) | [U](#u) | [V](#v) | [W](#w)

---

## A

### Aggregate Root
A pattern from Domain-Driven Design (DDD) where a single entity serves as the entry point for a cluster of related entities. In this codebase, entities that implement `IAggregateRoot` are the root of their aggregate and control access to child entities.

**Where in code?** [ClassifiedAds.Domain/Entities/IAggregateRoot.cs](../ClassifiedAds.Domain/Entities/IAggregateRoot.cs)

### ASP.NET Core
Microsoft's cross-platform, high-performance framework for building web applications and APIs. This codebase uses ASP.NET Core for the WebAPI host.

### Aspire
.NET Aspire is a cloud-ready stack for building observable, production-ready distributed applications. Used for local development orchestration.

**Where in code?** [ClassifiedAds.AspireAppHost/Program.cs](../ClassifiedAds.AspireAppHost/Program.cs)

---

## B

### Background Worker
A long-running service that executes in the background, typically processing queues, publishing outbox events, or performing scheduled tasks. Implemented via `BackgroundService`.

**Where in code?** [ClassifiedAds.Background/Program.cs](../ClassifiedAds.Background/Program.cs)

### Bearer Token
An HTTP authentication scheme where the client presents a JWT token in the `Authorization` header. Format: `Bearer <token>`.

---

## C

### Command
A request to perform an action that changes system state (write operation). Commands do not return data. Part of the CQRS pattern.

**Where in code?** [ClassifiedAds.Application/ICommandHandler.cs](../ClassifiedAds.Application/ICommandHandler.cs)

### Command Handler
A class that processes a specific command type. Implements `ICommandHandler<TCommand>`.

### Composition Root
The single location in an application where the entire object graph is composed. In this codebase, `Program.cs` in each host project serves as the composition root.

**Where in code?** [ClassifiedAds.WebAPI/Program.cs](../ClassifiedAds.WebAPI/Program.cs)

### CQRS (Command Query Responsibility Segregation)
An architectural pattern that separates read and write operations into distinct models. This codebase implements CQRS via the `Dispatcher` class.

**Where in code?** [ClassifiedAds.Application/Common/Dispatcher.cs](../ClassifiedAds.Application/Common/Dispatcher.cs)

### Cross-Cutting Concern
Functionality that affects multiple parts of an application, such as logging, caching, validation, or authentication. Typically implemented using decorators or middleware.

**Where in code?** [ClassifiedAds.CrossCuttingConcerns/](../ClassifiedAds.CrossCuttingConcerns/)

---

## D

### DbContext
Entity Framework Core's representation of a session with the database. Each module has its own DbContext for isolation.

**Where in code?** [ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs](../ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs)

### DbContext-per-Module
An architectural pattern where each module has its own dedicated DbContext, providing isolation and independent migration management.

### Decorator Pattern
A design pattern that allows behavior to be added to individual objects dynamically. Used extensively for command/query handlers to add logging, validation, and other cross-cutting concerns.

**Where in code?** [ClassifiedAds.Application/Decorators/](../ClassifiedAds.Application/Decorators/)

### Dependency Injection (DI)
A technique where objects receive their dependencies from an external source rather than creating them. ASP.NET Core has built-in DI container.

### Dispatcher
The central component that routes commands, queries, and domain events to their handlers. Acts as the mediator in the system.

**Where in code?** [ClassifiedAds.Application/Common/Dispatcher.cs](../ClassifiedAds.Application/Common/Dispatcher.cs)

### Domain Event
An event that represents something significant that happened in the domain. Used for decoupled communication between aggregates and modules.

**Where in code?** [ClassifiedAds.Domain/Events/IDomainEvent.cs](../ClassifiedAds.Domain/Events/IDomainEvent.cs)

### Domain-Driven Design (DDD)
A software development approach that focuses on modeling software based on the business domain. Concepts like Entity, Value Object, Aggregate Root, and Domain Events come from DDD.

---

## E

### Entity
A domain object that has a unique identity that persists over time. In this codebase, entities inherit from `Entity<TId>`.

**Where in code?** [ClassifiedAds.Domain/Entities/Entity.cs](../ClassifiedAds.Domain/Entities/Entity.cs)

### Entity Framework Core (EF Core)
Microsoft's modern object-relational mapper (ORM) for .NET. Version 10.0 is used in this codebase.

### Event Handler
A class that responds to domain events. Implements `IDomainEventHandler<TEvent>`.

**Where in code?** [ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs](../ClassifiedAds.Modules.Product/EventHandlers/ProductCreatedEventHandler.cs)

---

## F

### Feature Toggle
A mechanism to enable or disable features at runtime without deploying new code. Also known as feature flags.

**Where in code?** [ClassifiedAds.Application/FeatureToggles/](../ClassifiedAds.Application/FeatureToggles/)

---

## G

### Generic Host
.NET's hosting infrastructure for building long-running services. Used by the Background host.

### GraphQL
A query language for APIs (not used in this codebase, but supported via module extension).

### Grpc
A high-performance RPC framework (available as an extension point).

---

## H

### Handler
A class responsible for processing a specific type of message (command, query, or event).

### Health Check
An endpoint that reports the health status of an application and its dependencies.

**Where in code?** [ClassifiedAds.Infrastructure/HealthChecks/](../ClassifiedAds.Infrastructure/HealthChecks/)

### Hosted Service
A background service that runs within an ASP.NET Core application. Implements `IHostedService` or `BackgroundService`.

---

## I

### ICurrentUser
An abstraction that provides information about the currently authenticated user.

**Where in code?** [ClassifiedAds.Contracts/Identity/Services/ICurrentUser.cs](../ClassifiedAds.Contracts/Identity/Services/ICurrentUser.cs)

### Integration Event
An event published to external systems via message bus. Distinguished from domain events which are internal.

---

## J

### JWT (JSON Web Token)
A compact, URL-safe means of representing claims between two parties. Used for authentication.

**Where in code?** [ClassifiedAds.WebAPI/Program.cs](../ClassifiedAds.WebAPI/Program.cs) (JWT configuration)

---

## M

### Message Bus
An infrastructure component that enables asynchronous message-based communication. Supports RabbitMQ, Kafka, and Azure Service Bus.

**Where in code?** [ClassifiedAds.Infrastructure/Messaging/](../ClassifiedAds.Infrastructure/Messaging/)

### Middleware
A component that's assembled into an application pipeline to handle requests and responses.

### Migration
A file that describes changes to the database schema. Managed by EF Core Migrations.

**Where in code?** [ClassifiedAds.Migrator/](../ClassifiedAds.Migrator/)

### Modular Monolith
An architectural style where a single deployable unit (monolith) is internally organized into loosely coupled modules with clear boundaries.

### Module
A self-contained, cohesive unit of functionality with its own entities, commands, queries, and persistence. Each module has clear boundaries.

---

## O

### OpenTelemetry
A vendor-neutral observability framework for collecting traces, metrics, and logs.

**Where in code?** [ClassifiedAds.Infrastructure/Monitoring/OpenTelemetry/](../ClassifiedAds.Infrastructure/Monitoring/OpenTelemetry/)

### Outbox Pattern
A reliability pattern where domain events are first written to a database table (outbox) in the same transaction as the business operation, then published asynchronously.

**Where in code?** [ClassifiedAds.Modules.Product/Entities/OutboxMessage.cs](../ClassifiedAds.Modules.Product/Entities/OutboxMessage.cs)

---

## P

### Policy-Based Authorization
ASP.NET Core's authorization model where permissions are defined as policies and applied declaratively.

**Where in code?** [ClassifiedAds.Modules.Product/Authorization/](../ClassifiedAds.Modules.Product/Authorization/)

---

## Q

### Query
A request to retrieve data without changing system state (read operation). Part of the CQRS pattern.

**Where in code?** [ClassifiedAds.Application/Common/Queries/IQuery.cs](../ClassifiedAds.Application/Common/Queries/IQuery.cs)

### Query Handler
A class that processes a specific query type and returns data. Implements `IQueryHandler<TQuery, TResult>`.

**Where in code?** [ClassifiedAds.Application/Common/Queries/IQueryHandler.cs](../ClassifiedAds.Application/Common/Queries/IQueryHandler.cs)

### Query Handler
A class that processes a specific query type and returns data. Implements `IQueryHandler<TQuery, TResult>`.

---

## R

### Repository Pattern
An abstraction that mediates between the domain and data mapping layers, acting like an in-memory collection of domain objects.

**Where in code?** [ClassifiedAds.Domain/Repositories/IRepository.cs](../ClassifiedAds.Domain/Repositories/IRepository.cs)

### Row Version
A property used for optimistic concurrency control. EF Core automatically checks this value during updates.

---

## S

### Scrutor
A library that adds assembly scanning capabilities to Microsoft.Extensions.DependencyInjection.

**Where in code?** Used in `AddMessageHandlers()` for handler registration

### Serilog
A structured logging library for .NET applications.

**Where in code?** [ClassifiedAds.Infrastructure/Logging/](../ClassifiedAds.Infrastructure/Logging/)

### Service Collection
The container for service registrations in .NET's dependency injection system.

### Specification Pattern
A pattern for encapsulating query logic in reusable objects (available via extension).

---

## T

### Trace
A representation of a series of related events in a distributed system. Part of OpenTelemetry observability.

### Transaction
A unit of work that is atomic, consistent, isolated, and durable (ACID). Managed via Unit of Work pattern.

---

## U

### Unit of Work
A pattern that maintains a list of objects affected by a business transaction and coordinates writing out changes.

**Where in code?** [ClassifiedAds.Domain/Repositories/IUnitOfWork.cs](../ClassifiedAds.Domain/Repositories/IUnitOfWork.cs)

---

## V

### Value Object
A domain object that describes a characteristic of something but has no identity. Defined by its attributes.

**Where in code?** [ClassifiedAds.Domain/ValueObjects/](../ClassifiedAds.Domain/ValueObjects/)

### Vertical Slice Architecture
An architectural approach where code is organized by feature/use-case rather than by technical layer. This codebase uses elements of vertical slice within modules.

---

## W

### Worker
See [Background Worker](#background-worker).

---

## Abbreviations Reference

| Abbreviation | Full Term |
|-------------|-----------|
| API | Application Programming Interface |
| CQRS | Command Query Responsibility Segregation |
| DDD | Domain-Driven Design |
| DI | Dependency Injection |
| DTO | Data Transfer Object |
| EF | Entity Framework |
| JWT | JSON Web Token |
| ORM | Object-Relational Mapper |
| RBAC | Role-Based Access Control |
| REST | Representational State Transfer |
| SPA | Single Page Application |
| SQL | Structured Query Language |
| UI | User Interface |
| UUID | Universally Unique Identifier |

---

*Previous: [11 - Extension Playbook](11-extension-playbook.md) | [Back to Index](README.md)*
