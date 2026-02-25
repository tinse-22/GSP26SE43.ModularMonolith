# UML Package Diagram Source (Structural Analysis)

## Static Analysis Method
- Scope scanned: all production `ClassifiedAds.*` projects (`*.csproj`), focused on `ClassifiedAds.Modules.*`, core layers, and hosts.
- Dependency sources:
  - Project reference graph from `ProjectReference` in `*.csproj`
  - Root namespace clustering by `ClassifiedAds.Modules.<Context>`
  - Cross-context coupling from `using ClassifiedAds.Contracts.*`
  - Direct cross-module import check from `using ClassifiedAds.Modules.*`
- Filtering applied for source-import scan:
  - Excluded DTOs, enums, utility/helper classes, configuration classes, mapper classes, tests, docs, generated files, scripts, and `bin/obj`.

---

## STEP 1 - Package Inventory

### Core Layers
- `ClassifiedAds.CrossCuttingConcerns`
- `ClassifiedAds.Domain`
- `ClassifiedAds.Application`
- `ClassifiedAds.Infrastructure`
- `ClassifiedAds.Persistence.PostgreSQL`
- `ClassifiedAds.Contracts`

### Feature Modules (Bounded Context Candidates)
- API Quality Lifecycle:
  - `ClassifiedAds.Modules.ApiDocumentation`
  - `ClassifiedAds.Modules.TestGeneration`
  - `ClassifiedAds.Modules.TestExecution`
  - `ClassifiedAds.Modules.TestReporting`
- Identity and Collaboration:
  - `ClassifiedAds.Modules.Identity`
  - `ClassifiedAds.Modules.AuditLog`
  - `ClassifiedAds.Modules.Notification`
- Monetization and Assets:
  - `ClassifiedAds.Modules.Subscription`
  - `ClassifiedAds.Modules.Storage`
- Support:
  - `ClassifiedAds.Modules.Configuration`
  - `ClassifiedAds.Modules.LlmAssistant`

### Shared / Cross-Cutting Components
- `ClassifiedAds.ServiceDefaults`
- `ClassifiedAds.Contracts`
- `ClassifiedAds.CrossCuttingConcerns`

### Hosts
- `ClassifiedAds.WebAPI`
- `ClassifiedAds.Background`
- `ClassifiedAds.Migrator`
- `ClassifiedAds.AppHost`

### Client Applications (Planned — Unimplemented)
- `WebApp (SPA)` — Single-page application for all user roles (Admin, Developer, Tester, Viewer). Tech stack TBD. Communicates exclusively with WebAPI host via REST API.

---

## STEP 2 - Dependency Matrix

### Core Dependency Directions
- `Application -> Domain`
- `Domain -> CrossCuttingConcerns`
- `Infrastructure -> Application, Domain`
- `Persistence.PostgreSQL -> Domain, CrossCuttingConcerns`

### Module-to-Core Directions
- All module projects depend on:
  - `Application`
  - `Domain`
  - `Infrastructure`
  - `Persistence.PostgreSQL`
  - `CrossCuttingConcerns`
- Most module projects also depend on `Contracts` (except `Configuration` at project-reference level).

### Host Composition Directions
- `AppHost -> WebAPI, Background, Migrator, ServiceDefaults`
- `WebAPI -> ServiceDefaults + {ApiDocumentation, AuditLog, Configuration, Identity, Notification, Storage, Subscription, TestGeneration, TestExecution} + {Application, Domain, Infrastructure}`
- `Background -> ServiceDefaults + {AuditLog, Identity, Notification, Storage, Subscription} + {Application, Infrastructure}`
- `Migrator -> ServiceDefaults + {ApiDocumentation, AuditLog, Configuration, Identity, Notification, Storage, Subscription, TestGeneration, TestExecution, TestReporting} + Infrastructure`

### Client-to-Host Direction (Planned)
- `WebApp (SPA) --> WebAPI` (HTTP/REST — sole entry point)

### Planned Host Composition Changes (When FEs Are Implemented)
- `WebAPI --> TestReporting` (FE-10: report API endpoints)
- `WebAPI --> LlmAssistant` (FE-09: failure explanations, FE-15/16/17: suggestion review APIs)
- `Migrator --> LlmAssistant` (LlmAssistant đã có DbContext nhưng Migrator chưa reference — cần thêm ProjectReference)
- `Background --> TestExecution` (FE-07: possible background test runners)

### Cross-Context Coupling via Contracts (Dotted)
- `ApiDocumentation ..> Identity, Subscription, Storage, AuditLog`
- `TestGeneration ..> ApiDocumentation, Identity`
- `TestExecution ..> Identity`
- `Subscription ..> Identity, Notification, AuditLog`
- `Storage ..> Identity, AuditLog`
- `Identity ..> Notification`
- `AuditLog ..> Identity`

### Planned Cross-Context Coupling via Contracts (When FEs Are Implemented)
- `TestExecution ..> TestGeneration` (FE-07: fetch test cases to execute)
- `TestExecution ..> ApiDocumentation` (FE-08: schema validation against API specs)
- `TestReporting ..> TestExecution` (FE-10: fetch execution results for reports)
- `TestReporting ..> Identity` (FE-10: user context for report ownership)
- `LlmAssistant ..> TestExecution` (FE-09: fetch failed test results for explanation)
- `LlmAssistant ..> ApiDocumentation` (FE-09: API context for failure analysis)
- `LlmAssistant ..> Identity` (FE-09/15/16/17: user context)
- `TestGeneration ..> LlmAssistant` (FE-06: LLM scenario suggestions for boundary/negative)

### Coupling Path Summary
- Client application communicates exclusively with WebAPI host via REST API. No direct module or Core access from client layer.
- Composition roots (`WebAPI`, `Background`, `Migrator`) orchestrate module activation directly.
- Cross-context communication is predominantly contract-based (`ClassifiedAds.Contracts.*`), not direct module project references.
- Planned couplings above are projected based on PROJECT_REQUIREMENTS.md feature flow (FE-05B → FE-10, FE-15/16/17). Actual Contracts interfaces will be created when FEs are implemented.

---

## STEP 3 - Architectural Observations

### Architecture Type
- Current structure is a **modular monolith with shared layered core** (hybrid of layered architecture + bounded module packaging).

### Cyclic Dependency Check
- No project-reference cycles detected.
- No module-to-module direct compile-time project references detected.

### Layer Violation Check
- No detected violations such as `Domain -> Infrastructure`.
- Core dependency direction remains inward and consistent.

### Cross-Context Boundary Check
- No direct cross-module imports inside module projects (`using ClassifiedAds.Modules.<OtherContext>`) were found.
- Cross-context coupling is mostly mediated by `Contracts`, consistent with anti-corruption intent.

### Shared Kernel Candidates
- High fan-in indicates shared kernel role:
  - `Domain`
  - `Application`
  - `Infrastructure`
  - `CrossCuttingConcerns`
  - `Persistence.PostgreSQL`
  - `Contracts`

### Clean Architecture / DDD Alignment Assessment
- Strengths:
  - Clear inward core dependencies.
  - No cyclic references.
  - Contract-mediated cross-context interactions.
- Structural smells:
  - `Infrastructure` and `Persistence.PostgreSQL` are referenced by all modules (tight shared technical coupling).
  - Single global `Domain` may blur strict bounded-context autonomy.
  - Single global `Contracts` can become a coupling hotspot.
- Suggested improvements:
  - Split contracts by context (e.g., `Contracts.Identity`, `Contracts.Subscription`, ...).
  - Move module-facing abstractions to application/contract boundaries and minimize direct module reliance on infrastructure assembly.
  - Introduce explicit `SharedKernel` package (or per-context domain slices) to reduce over-centralized domain model.
  - Add architecture tests to enforce dependency rules continuously.

---

## STEP 4 - UML Package Diagrams (PlantUML)

Chia thành 4 diagram riêng biệt để dễ đọc, dễ render, và AI có thể vẽ chuẩn.

---

### Diagram 1 — High-Level Architecture Overview

Tổng quan kiến trúc phân tầng: Hosts → Modules → Core.

```plantuml
@startuml diagram1_overview
top to bottom direction
skinparam packageStyle rectangle
skinparam componentStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 12
skinparam padding 4

skinparam package {
  BackgroundColor<<host>> #E3F2FD
  BackgroundColor<<module>> #FFF3E0
  BackgroundColor<<core>> #E8F5E9
  BackgroundColor<<shared>> #F3E5F5
  BackgroundColor<<planned>> #F5F5F5
  BorderColor<<planned>> #9E9E9E
}
skinparam component {
  BackgroundColor<<planned>> #BDBDBD
  BorderColor<<planned>> #9E9E9E
}

package "Client Applications" <<planned>> {
  component "WebApp (SPA)" as WebApp <<planned>>
}

package "Hosts" <<host>> {
  component AppHost
  component WebAPI
  component Background
  component Migrator
}

package "Shared Platform" <<shared>> {
  component ServiceDefaults
}

package "Feature Modules" <<module>> {
  package "API Quality Lifecycle" {
    component ApiDocumentation
    component TestGeneration
    component TestExecution
    component TestReporting
  }
  package "Identity & Collaboration" {
    component Identity
    component AuditLog
    component Notification
  }
  package "Monetization & Assets" {
    component Subscription
    component Storage
  }
  package "Support" {
    component Configuration
    component LlmAssistant
  }
}

package "Core Layers" <<core>> {
  component Application
  component Domain
  component Infrastructure
  component "Persistence.PostgreSQL" as Postgres
  component Contracts
  component CrossCuttingConcerns
}

' === Client Application to Host ===
WebApp ..> WebAPI : <<planned>>

' === Host orchestration (simplified) ===
AppHost --> WebAPI
AppHost --> Background
AppHost --> Migrator

' === All hosts use ServiceDefaults ===
WebAPI --> ServiceDefaults
Background --> ServiceDefaults
Migrator --> ServiceDefaults

' === Hosts compose modules (grouped edges) ===
WebAPI -[#336699]-> "Feature Modules" : composes
Background -[#336699]-> "Identity & Collaboration" : composes
Background -[#336699]-> "Monetization & Assets" : composes
Migrator -[#336699]-> "Feature Modules" : composes

' === All modules depend on Core (single grouped edge) ===
"Feature Modules" -[#2E7D32]-> "Core Layers" : depends on

' === Core internal layering ===
Application --> Domain
Domain --> CrossCuttingConcerns
Infrastructure --> Application
Infrastructure --> Domain
Postgres --> Domain
Postgres --> CrossCuttingConcerns

legend right
  Dashed border / grey = planned, not yet implemented
end legend
@enduml
```

---

### Diagram 2 — Core Layers Internal Dependencies

Chi tiết dependency giữa các layer trong Core.

```plantuml
@startuml diagram2_core
top to bottom direction
skinparam packageStyle rectangle
skinparam componentStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 13

skinparam component {
  BackgroundColor #E8F5E9
  BorderColor #2E7D32
}

component CrossCuttingConcerns as CCC
component Domain
component Application as App
component Contracts
component Infrastructure as Infra
component "Persistence.PostgreSQL" as PG

App --> Domain : depends
Domain --> CCC : depends
Infra --> App : depends
Infra --> Domain : depends
PG --> Domain : depends
PG --> CCC : depends

note right of Contracts
  Contract-based interface
  for cross-module coupling
end note

note bottom of CCC
  Logging, Validation,
  Exception handling
end note
@enduml
```

---

### Diagram 3 — Host-to-Module Composition

Chi tiết từng Host compose những module nào.

```plantuml
@startuml diagram3_host_composition
left to right direction
skinparam packageStyle rectangle
skinparam componentStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 11

skinparam component {
  BackgroundColor<<host>> #E3F2FD
  BackgroundColor<<api>> #FFF9C4
  BackgroundColor<<id>> #F3E5F5
  BackgroundColor<<mon>> #FFCCBC
  BackgroundColor<<sup>> #E0F7FA
  BackgroundColor<<planned>> #BDBDBD
  BorderColor<<planned>> #9E9E9E
}

component "WebApp (SPA)" as WebApp <<planned>>

component AppHost <<host>>
component WebAPI <<host>>
component Background <<host>>
component Migrator <<host>>

component ApiDocumentation <<api>>
component TestGeneration <<api>>
component TestExecution <<api>>
component TestReporting <<api>>

component Identity <<id>>
component AuditLog <<id>>
component Notification <<id>>

component Subscription <<mon>>
component Storage <<mon>>

component Configuration <<sup>>
component LlmAssistant <<sup>>

' === Client Application to Host ===
WebApp ..> WebAPI : <<planned>>

' === AppHost ===
AppHost --> WebAPI
AppHost --> Background
AppHost --> Migrator

' === WebAPI composes ===
WebAPI --> ApiDocumentation
WebAPI --> TestGeneration
WebAPI --> TestExecution
WebAPI --> Identity
WebAPI --> AuditLog
WebAPI --> Notification
WebAPI --> Subscription
WebAPI --> Storage
WebAPI --> Configuration
WebAPI ..> TestReporting : <<planned>>
WebAPI ..> LlmAssistant : <<planned>>

' === Background composes ===
Background --> Identity
Background --> AuditLog
Background --> Notification
Background --> Subscription
Background --> Storage
Background ..> TestExecution : <<planned>>

' === Migrator composes ===
Migrator --> ApiDocumentation
Migrator --> TestGeneration
Migrator --> TestExecution
Migrator --> TestReporting
Migrator --> Identity
Migrator --> AuditLog
Migrator --> Notification
Migrator --> Subscription
Migrator --> Storage
Migrator --> Configuration
Migrator ..> LlmAssistant : <<planned>>

legend right
  Dashed border / grey = planned, not yet implemented
end legend
@enduml
```

---

### Diagram 4 — Cross-Context Coupling via Contracts

Quan trọng nhất: coupling giữa các bounded context qua Contracts (đường nét đứt).

```plantuml
@startuml diagram4_cross_context
top to bottom direction
skinparam packageStyle rectangle
skinparam componentStyle rectangle
skinparam shadowing false
skinparam defaultFontSize 12
skinparam linetype polyline

skinparam component {
  BackgroundColor<<api>> #FFF9C4
  BorderColor<<api>> #F9A825
  BackgroundColor<<id>> #F3E5F5
  BorderColor<<id>> #7B1FA2
  BackgroundColor<<mon>> #FFCCBC
  BorderColor<<mon>> #E64A19
  BackgroundColor<<sup>> #E0F7FA
  BorderColor<<sup>> #00838F
}

package "API Quality Lifecycle" {
  component ApiDocumentation <<api>>
  component TestGeneration <<api>>
  component TestExecution <<api>>
  component TestReporting <<api>>
}

package "Identity & Collaboration" {
  component Identity <<id>>
  component AuditLog <<id>>
  component Notification <<id>>
}

package "Monetization & Assets" {
  component Subscription <<mon>>
  component Storage <<mon>>
}

package "Support" {
  component LlmAssistant <<sup>>
}

' === Cross-context via Contracts ===

ApiDocumentation ..[#E65100]..> Identity : <<Contracts.Identity>>
ApiDocumentation ..[#E65100]..> Subscription : <<Contracts.Subscription>>
ApiDocumentation ..[#E65100]..> Storage : <<Contracts.Storage>>
ApiDocumentation ..[#E65100]..> AuditLog : <<Contracts.AuditLog>>

TestGeneration ..[#E65100]..> ApiDocumentation : <<Contracts.ApiDoc>>
TestGeneration ..[#E65100]..> Identity : <<Contracts.Identity>>

TestExecution ..[#E65100]..> Identity : <<Contracts.Identity>>

Subscription ..[#1565C0]..> Identity : <<Contracts.Identity>>
Subscription ..[#1565C0]..> Notification : <<Contracts.Notification>>
Subscription ..[#1565C0]..> AuditLog : <<Contracts.AuditLog>>

Storage ..[#1565C0]..> Identity : <<Contracts.Identity>>
Storage ..[#1565C0]..> AuditLog : <<Contracts.AuditLog>>

Identity ..[#7B1FA2]..> Notification : <<Contracts.Notification>>

AuditLog ..[#7B1FA2]..> Identity : <<Contracts.Identity>>

' === Planned Cross-Context via Contracts ===
TestExecution ..[#9E9E9E]..> TestGeneration : <<planned>>
TestExecution ..[#9E9E9E]..> ApiDocumentation : <<planned>>
TestReporting ..[#9E9E9E]..> TestExecution : <<planned>>
TestReporting ..[#9E9E9E]..> Identity : <<planned>>
LlmAssistant ..[#9E9E9E]..> TestExecution : <<planned>>
LlmAssistant ..[#9E9E9E]..> ApiDocumentation : <<planned>>
LlmAssistant ..[#9E9E9E]..> Identity : <<planned>>
TestGeneration ..[#9E9E9E]..> LlmAssistant : <<planned>>

legend right
  |= Color |= Meaning |
  | <#E65100> | API Quality → other context |
  | <#1565C0> | Monetization → other context |
  | <#7B1FA2> | Identity/Collab internal |
  | <#9E9E9E> | Planned coupling |
  -- -- --
  Dotted lines = contract-based coupling
  No direct module-to-module references
  Dashed border / grey / <<planned>> = not yet implemented
end legend
@enduml
```

---

### Diagram 5A — Mermaid: High-Level Overview

Tổng quan kiến trúc: Hosts → Modules (grouped by BC) → Core Layers.

```mermaid
graph TB
    subgraph ClientApps["Client Applications «planned»"]
        WebApp["WebApp (SPA)"]
    end

    subgraph Hosts["Hosts"]
        AppHost
        WebAPI
        Background
        Migrator
    end

    subgraph SharedPlatform["Shared Platform"]
        ServiceDefaults
    end

    subgraph Modules["Feature Modules"]
        subgraph BC_API["API Quality Lifecycle"]
            ApiDoc["ApiDocumentation"]
            TestGen["TestGeneration"]
            TestExec["TestExecution"]
            TestRep["TestReporting"]
        end
        subgraph BC_ID["Identity & Collaboration"]
            Identity
            AuditLog
            Notification
        end
        subgraph BC_MON["Monetization & Assets"]
            Subscription
            Storage
        end
        subgraph BC_SUP["Support"]
            Configuration
            LlmAssistant
        end
    end

    subgraph Core["Core Layers"]
        Application
        Domain
        Infrastructure
        Postgres["Persistence.PostgreSQL"]
        Contracts
        CCC["CrossCuttingConcerns"]
    end

    %% Client Application to Host
    WebApp -.->|«planned»| WebAPI

    %% Host orchestration
    AppHost --> WebAPI
    AppHost --> Background
    AppHost --> Migrator
    AppHost --> ServiceDefaults
    WebAPI --> ServiceDefaults
    Background --> ServiceDefaults
    Migrator --> ServiceDefaults

    %% Hosts → Modules (simplified)
    WebAPI --> Modules
    Background --> BC_ID
    Background --> BC_MON
    Migrator --> Modules

    %% Modules → Core (all modules share same deps)
    Modules --> Core

    %% Core internal
    Application --> Domain
    Domain --> CCC
    Infrastructure --> Application
    Infrastructure --> Domain
    Postgres --> Domain
    Postgres --> CCC

    classDef host fill:#E3F2FD,stroke:#1565C0,color:#000
    classDef module fill:#FFF3E0,stroke:#E65100,color:#000
    classDef core fill:#E8F5E9,stroke:#2E7D32,color:#000
    classDef shared fill:#F3E5F5,stroke:#7B1FA2,color:#000
    classDef planned fill:#F5F5F5,stroke:#9E9E9E,stroke-dasharray: 5 5,color:#000

    class WebApp planned
    class AppHost,WebAPI,Background,Migrator host
    class ApiDoc,TestGen,TestExec,TestRep,Identity,AuditLog,Notification,Subscription,Storage,Configuration,LlmAssistant module
    class Application,Domain,Infrastructure,Postgres,Contracts,CCC core
    class ServiceDefaults shared
```

---

### Diagram 5B — Mermaid: Host → Module Composition

Chi tiết từng Host compose những module nào + Host → Core trực tiếp.

```mermaid
graph LR
    subgraph ClientApps["Client Applications «planned»"]
        WebApp["WebApp (SPA)"]
    end

    subgraph Hosts["Hosts"]
        AppHost
        WebAPI
        Background
        Migrator
    end

    subgraph BC_API["API Quality Lifecycle"]
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
    end

    subgraph BC_ID["Identity & Collaboration"]
        Identity
        AuditLog
        Notification
    end

    subgraph BC_MON["Monetization & Assets"]
        Subscription
        Storage
    end

    subgraph BC_SUP["Support"]
        Configuration
        LlmAssistant
    end

    subgraph Core["Core (direct refs)"]
        Application
        Infrastructure
        Domain
    end

    ServiceDefaults

    %% Client Application to Host
    WebApp -.->|«planned»| WebAPI

    %% AppHost orchestration
    AppHost --> WebAPI
    AppHost --> Background
    AppHost --> Migrator
    AppHost --> ServiceDefaults

    %% All hosts → ServiceDefaults
    WebAPI --> ServiceDefaults
    Background --> ServiceDefaults
    Migrator --> ServiceDefaults

    %% WebAPI → Modules
    WebAPI --> ApiDoc
    WebAPI --> TestGen
    WebAPI --> TestExec
    WebAPI --> Identity
    WebAPI --> AuditLog
    WebAPI --> Notification
    WebAPI --> Subscription
    WebAPI --> Storage
    WebAPI --> Configuration
    WebAPI -.->|«planned»| TestRep
    WebAPI -.->|«planned»| LlmAssistant

    %% Background → Modules
    Background --> AuditLog
    Background --> Identity
    Background --> Notification
    Background --> Subscription
    Background --> Storage
    Background -.->|«planned»| TestExec

    %% Migrator → Modules
    Migrator --> ApiDoc
    Migrator --> TestGen
    Migrator --> TestExec
    Migrator --> TestRep
    Migrator --> Identity
    Migrator --> AuditLog
    Migrator --> Notification
    Migrator --> Subscription
    Migrator --> Storage
    Migrator --> Configuration
    Migrator -.->|«planned»| LlmAssistant

    %% Host → Core (direct project refs)
    WebAPI --> Application
    WebAPI --> Domain
    WebAPI --> Infrastructure
    Background --> Application
    Background --> Infrastructure
    Migrator --> Infrastructure

    classDef host fill:#E3F2FD,stroke:#1565C0,color:#000
    classDef api fill:#FFF9C4,stroke:#F9A825,color:#000
    classDef id fill:#F3E5F5,stroke:#7B1FA2,color:#000
    classDef mon fill:#FFCCBC,stroke:#E64A19,color:#000
    classDef sup fill:#E0F7FA,stroke:#00838F,color:#000
    classDef core fill:#E8F5E9,stroke:#2E7D32,color:#000
    classDef planned fill:#F5F5F5,stroke:#9E9E9E,stroke-dasharray: 5 5,color:#000

    class WebApp planned
    class AppHost,WebAPI,Background,Migrator host
    class ApiDoc,TestGen,TestExec,TestRep api
    class Identity,AuditLog,Notification id
    class Subscription,Storage mon
    class Configuration,LlmAssistant sup
    class Application,Infrastructure,Domain core
```

---

### Diagram 5C — Mermaid: Module → Core Dependencies

Tất cả module depend on cùng 5 core layers + Contracts (trừ Configuration).

```mermaid
graph TB
    subgraph Modules["All Feature Modules"]
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
        Identity
        AuditLog
        Notification
        Subscription
        Storage
        Configuration
        LlmAssistant
    end

    subgraph Core["Core Layers"]
        Application
        Domain
        Infrastructure
        Postgres["Persistence.PostgreSQL"]
        CCC["CrossCuttingConcerns"]
        Contracts
    end

    %% Every module → 5 core layers (solid)
    ApiDoc --> Application & Domain & Infrastructure & Postgres & CCC
    TestGen --> Application & Domain & Infrastructure & Postgres & CCC
    TestExec --> Application & Domain & Infrastructure & Postgres & CCC
    TestRep --> Application & Domain & Infrastructure & Postgres & CCC
    Identity --> Application & Domain & Infrastructure & Postgres & CCC
    AuditLog --> Application & Domain & Infrastructure & Postgres & CCC
    Notification --> Application & Domain & Infrastructure & Postgres & CCC
    Subscription --> Application & Domain & Infrastructure & Postgres & CCC
    Storage --> Application & Domain & Infrastructure & Postgres & CCC
    Configuration --> Application & Domain & Infrastructure & Postgres & CCC
    LlmAssistant --> Application & Domain & Infrastructure & Postgres & CCC

    %% Module → Contracts (dotted, Configuration excluded)
    ApiDoc -.-> Contracts
    TestGen -.-> Contracts
    TestExec -.-> Contracts
    TestRep -.-> Contracts
    Identity -.-> Contracts
    AuditLog -.-> Contracts
    Notification -.-> Contracts
    Subscription -.-> Contracts
    Storage -.-> Contracts
    LlmAssistant -.-> Contracts

    %% Core internal
    Application --> Domain
    Domain --> CCC
    Infrastructure --> Application
    Infrastructure --> Domain
    Postgres --> Domain
    Postgres --> CCC

    classDef module fill:#FFF3E0,stroke:#E65100,color:#000
    classDef core fill:#E8F5E9,stroke:#2E7D32,color:#000

    class ApiDoc,TestGen,TestExec,TestRep,Identity,AuditLog,Notification,Subscription,Storage,Configuration,LlmAssistant module
    class Application,Domain,Infrastructure,Postgres,CCC,Contracts core
```

---

### Diagram 5D — Mermaid: Cross-Context Coupling via Contracts

Coupling giữa các bounded context qua Contracts (đường nét đứt). Đây là diagram quan trọng nhất.

```mermaid
graph TB
    subgraph BC_API["API Quality Lifecycle"]
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
    end

    subgraph BC_ID["Identity & Collaboration"]
        Identity
        AuditLog
        Notification
    end

    subgraph BC_MON["Monetization & Assets"]
        Subscription
        Storage
    end

    subgraph BC_SUP["Support"]
        LlmAssistant
    end

    %% API Quality → other contexts
    ApiDoc -.->|Contracts.Identity| Identity
    ApiDoc -.->|Contracts.Subscription| Subscription
    ApiDoc -.->|Contracts.Storage| Storage
    ApiDoc -.->|Contracts.AuditLog| AuditLog

    TestGen -.->|Contracts.ApiDoc| ApiDoc
    TestGen -.->|Contracts.Identity| Identity

    TestExec -.->|Contracts.Identity| Identity

    %% Monetization → other contexts
    Subscription -.->|Contracts.Identity| Identity
    Subscription -.->|Contracts.Notification| Notification
    Subscription -.->|Contracts.AuditLog| AuditLog

    Storage -.->|Contracts.Identity| Identity
    Storage -.->|Contracts.AuditLog| AuditLog

    %% Identity & Collaboration internal
    Identity -.->|Contracts.Notification| Notification
    AuditLog -.->|Contracts.Identity| Identity

    %% Planned Cross-Context Couplings
    TestExec -.->|Contracts.TestGen «planned»| TestGen
    TestExec -.->|Contracts.ApiDoc «planned»| ApiDoc
    TestRep -.->|Contracts.TestExec «planned»| TestExec
    TestRep -.->|Contracts.Identity «planned»| Identity
    LlmAssistant -.->|Contracts.TestExec «planned»| TestExec
    LlmAssistant -.->|Contracts.ApiDoc «planned»| ApiDoc
    LlmAssistant -.->|Contracts.Identity «planned»| Identity
    TestGen -.->|Contracts.LlmAssistant «planned»| LlmAssistant

    classDef api fill:#FFF9C4,stroke:#F9A825,color:#000
    classDef id fill:#F3E5F5,stroke:#7B1FA2,color:#000
    classDef mon fill:#FFCCBC,stroke:#E64A19,color:#000
    classDef sup fill:#E0F7FA,stroke:#00838F,color:#000

    class ApiDoc,TestGen,TestExec,TestRep api
    class Identity,AuditLog,Notification id
    class Subscription,Storage mon
    class LlmAssistant sup
```
