# UML Package Diagram Source - Mermaid Compatible

Tài liệu này được viết lại để **render được trực tiếp trong Mermaid/draw.io**, tránh lỗi `UnknownDiagramError` do dùng cú pháp PlantUML trong môi trường chỉ hỗ trợ Mermaid.

## Quy ước
- `-->` = dependency/project reference thực tế
- `-.->` = dependency qua contracts hoặc planned coupling
- `==>` = dependency fan-out mạnh
- `PLANNED` = chưa có đầy đủ ở composition root / feature flow tương lai

---

## 1. Package Hierarchy

### 1.1 Core Packages
- `ClassifiedAds.Application`
- `ClassifiedAds.Domain`
- `ClassifiedAds.Infrastructure`
- `ClassifiedAds.Persistence.PostgreSQL`
- `ClassifiedAds.Contracts`
- `ClassifiedAds.CrossCuttingConcerns`

### 1.2 Feature Packages
- `ClassifiedAds.Modules.ApiDocumentation`
- `ClassifiedAds.Modules.TestGeneration`
- `ClassifiedAds.Modules.TestExecution`
- `ClassifiedAds.Modules.TestReporting`
- `ClassifiedAds.Modules.Identity`
- `ClassifiedAds.Modules.AuditLog`
- `ClassifiedAds.Modules.Notification`
- `ClassifiedAds.Modules.Subscription`
- `ClassifiedAds.Modules.Storage`
- `ClassifiedAds.Modules.Configuration`
- `ClassifiedAds.Modules.LlmAssistant`

### 1.3 Host Packages
- `ClassifiedAds.AppHost`
- `ClassifiedAds.WebAPI`
- `ClassifiedAds.Background`
- `ClassifiedAds.Migrator`
- `ClassifiedAds.ServiceDefaults`

---

## 2. Mermaid Package Diagrams

### Diagram 1 — Overall Architecture

```mermaid
flowchart TB
    subgraph ClientApps["Client Applications"]
        WebApp["WebApp SPA\nPLANNED"]
    end

    subgraph Hosts["Hosts"]
        AppHost["ClassifiedAds.AppHost"]
        WebAPI["ClassifiedAds.WebAPI"]
        Background["ClassifiedAds.Background"]
        Migrator["ClassifiedAds.Migrator"]
        ServiceDefaults["ClassifiedAds.ServiceDefaults"]
    end

    subgraph FeatureModules["Feature Modules"]
        direction TB
        subgraph BC1["API Quality Lifecycle"]
            ApiDoc["ClassifiedAds.Modules.ApiDocumentation"]
            TestGen["ClassifiedAds.Modules.TestGeneration"]
            TestExec["ClassifiedAds.Modules.TestExecution"]
            TestRep["ClassifiedAds.Modules.TestReporting"]
        end
        subgraph BC2["Identity & Collaboration"]
            Identity["ClassifiedAds.Modules.Identity"]
            AuditLog["ClassifiedAds.Modules.AuditLog"]
            Notification["ClassifiedAds.Modules.Notification"]
        end
        subgraph BC3["Monetization & Assets"]
            Subscription["ClassifiedAds.Modules.Subscription"]
            Storage["ClassifiedAds.Modules.Storage"]
        end
        subgraph BC4["Support"]
            Configuration["ClassifiedAds.Modules.Configuration"]
            LlmAssistant["ClassifiedAds.Modules.LlmAssistant"]
        end
    end

    subgraph Core["Core Layers"]
        Application["ClassifiedAds.Application"]
        Domain["ClassifiedAds.Domain"]
        Infrastructure["ClassifiedAds.Infrastructure"]
        Postgres["ClassifiedAds.Persistence.PostgreSQL"]
        Contracts["ClassifiedAds.Contracts"]
        CCC["ClassifiedAds.CrossCuttingConcerns"]
    end

    WebApp -.->|PLANNED| WebAPI

    AppHost --> WebAPI
    AppHost --> Background
    AppHost --> Migrator
    AppHost --> ServiceDefaults

    WebAPI --> ServiceDefaults
    Background --> ServiceDefaults
    Migrator --> ServiceDefaults

    WebAPI --> ApiDoc
    WebAPI --> TestGen
    WebAPI --> TestExec
    WebAPI --> TestRep
    WebAPI --> Identity
    WebAPI --> AuditLog
    WebAPI --> Notification
    WebAPI --> Subscription
    WebAPI --> Storage
    WebAPI --> Configuration
    WebAPI --> LlmAssistant

    Background --> ApiDoc
    Background --> TestGen
    Background --> Identity
    Background --> AuditLog
    Background --> Notification
    Background --> Subscription
    Background --> Storage
    Background --> LlmAssistant

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
    Migrator --> LlmAssistant

    FeatureModules --> Core
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
    classDef planned fill:#F5F5F5,stroke:#9E9E9E,stroke-dasharray:5 5,color:#616161

    class WebApp planned
    class AppHost,WebAPI,Background,Migrator host
    class ApiDoc,TestGen,TestExec,TestRep,Identity,AuditLog,Notification,Subscription,Storage,Configuration,LlmAssistant module
    class Application,Domain,Infrastructure,Postgres,Contracts,CCC core
    class ServiceDefaults shared
```

---

### Diagram 2 — Core Dependencies

```mermaid
flowchart TB
    Application["ClassifiedAds.Application"]
    Domain["ClassifiedAds.Domain"]
    CCC["ClassifiedAds.CrossCuttingConcerns"]
    Infrastructure["ClassifiedAds.Infrastructure"]
    Postgres["ClassifiedAds.Persistence.PostgreSQL"]
    Contracts["ClassifiedAds.Contracts"]

    Application --> Domain
    Domain --> CCC
    Infrastructure --> Application
    Infrastructure --> Domain
    Postgres --> Domain
    Postgres --> CCC

    Contracts -.->|contracts / abstractions| Application
```

---

### Diagram 3 — Host Composition

```mermaid
flowchart LR
    WebApp["WebApp SPA\nPLANNED"]

    subgraph Hosts["Hosts"]
        AppHost["ClassifiedAds.AppHost"]
        WebAPI["ClassifiedAds.WebAPI"]
        Background["ClassifiedAds.Background"]
        Migrator["ClassifiedAds.Migrator"]
    end

    ServiceDefaults["ClassifiedAds.ServiceDefaults"]

    subgraph APIQuality["API Quality Lifecycle"]
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
    end

    subgraph IdentityCollab["Identity & Collaboration"]
        Identity["Identity"]
        AuditLog["AuditLog"]
        Notification["Notification"]
    end

    subgraph MonetizationAssets["Monetization & Assets"]
        Subscription["Subscription"]
        Storage["Storage"]
    end

    subgraph Support["Support"]
        Configuration["Configuration"]
        LlmAssistant["LlmAssistant"]
    end

    subgraph Core["Core"]
        Application["Application"]
        Domain["Domain"]
        Infrastructure["Infrastructure"]
    end

    WebApp -.->|PLANNED| WebAPI

    AppHost --> WebAPI
    AppHost --> Background
    AppHost --> Migrator
    AppHost --> ServiceDefaults

    WebAPI --> ServiceDefaults
    Background --> ServiceDefaults
    Migrator --> ServiceDefaults

    WebAPI --> ApiDoc
    WebAPI --> TestGen
    WebAPI --> TestExec
    WebAPI --> TestRep
    WebAPI --> Identity
    WebAPI --> AuditLog
    WebAPI --> Notification
    WebAPI --> Subscription
    WebAPI --> Storage
    WebAPI --> Configuration
    WebAPI --> LlmAssistant

    Background --> ApiDoc
    Background --> TestGen
    Background --> Identity
    Background --> AuditLog
    Background --> Notification
    Background --> Subscription
    Background --> Storage
    Background --> LlmAssistant

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
    Migrator --> LlmAssistant

    WebAPI --> Application
    WebAPI --> Domain
    WebAPI --> Infrastructure
    Background --> Application
    Background --> Infrastructure
    Migrator --> Infrastructure
```

---

### Diagram 4 — Module to Core Dependencies

```mermaid
flowchart TB
    subgraph Modules["All Feature Modules"]
        direction LR
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
        Identity["Identity"]
        AuditLog["AuditLog"]
        Notification["Notification"]
        Subscription["Subscription"]
        Storage["Storage"]
        Configuration["Configuration"]
        LlmAssistant["LlmAssistant"]
    end

    subgraph Core["Core Layers"]
        Application["Application"]
        Domain["Domain"]
        Infrastructure["Infrastructure"]
        Postgres["Persistence.PostgreSQL"]
        CCC["CrossCuttingConcerns"]
        Contracts["Contracts"]
    end

    Modules ==> Application
    Modules ==> Domain
    Modules ==> Infrastructure
    Modules ==> Postgres
    Modules ==> CCC

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

    Application --> Domain
    Domain --> CCC
    Infrastructure --> Application
    Infrastructure --> Domain
    Postgres --> Domain
    Postgres --> CCC
```

---

### Diagram 5 — Cross-Context Coupling

```mermaid
flowchart TB
    subgraph APIQuality["API Quality Lifecycle"]
        ApiDoc["ApiDocumentation"]
        TestGen["TestGeneration"]
        TestExec["TestExecution"]
        TestRep["TestReporting"]
    end

    subgraph IdentityCollab["Identity & Collaboration"]
        Identity["Identity"]
        AuditLog["AuditLog"]
        Notification["Notification"]
    end

    subgraph MonetizationAssets["Monetization & Assets"]
        Subscription["Subscription"]
        Storage["Storage"]
    end

    subgraph Support["Support"]
        LlmAssistant["LlmAssistant"]
    end

    ApiDoc -.->|Contracts.Identity| Identity
    ApiDoc -.->|Contracts.Subscription| Subscription
    ApiDoc -.->|Contracts.Storage| Storage
    ApiDoc -.->|Contracts.AuditLog| AuditLog

    TestGen -.->|Contracts.ApiDoc| ApiDoc
    TestGen -.->|Contracts.Identity| Identity

    TestExec -.->|Contracts.Identity| Identity

    Subscription -.->|Contracts.Identity| Identity
    Subscription -.->|Contracts.Notification| Notification
    Subscription -.->|Contracts.AuditLog| AuditLog

    Storage -.->|Contracts.Identity| Identity
    Storage -.->|Contracts.AuditLog| AuditLog

    Identity -.->|Contracts.Notification| Notification
    AuditLog -.->|Contracts.Identity| Identity

    TestExec -.->|PLANNED Contracts.TestGen| TestGen
    TestExec -.->|PLANNED Contracts.ApiDoc| ApiDoc
    TestRep -.->|PLANNED Contracts.TestExec| TestExec
    TestRep -.->|PLANNED Contracts.Identity| Identity
    LlmAssistant -.->|PLANNED Contracts.TestExec| TestExec
    LlmAssistant -.->|PLANNED Contracts.ApiDoc| ApiDoc
    LlmAssistant -.->|PLANNED Contracts.Identity| Identity
    TestGen -.->|PLANNED Contracts.LlmAssistant| LlmAssistant
    TestExec -.->|PLANNED Contracts.Subscription| Subscription
    TestRep -.->|PLANNED Contracts.ApiDoc| ApiDoc
```

---

## 3. Important Note

Nếu bạn đang render trong **Mermaid**, thì **không được dùng**:
- `@startuml`
- `@enduml`
- `skinparam`
- `package` của PlantUML

Các block ở trên đã đổi hoàn toàn sang Mermaid để tránh lỗi `UnknownDiagramError`.

Nếu bạn thật sự muốn dùng **PlantUML strict package diagram**, thì cần đổi tool/rendering sang PlantUML, không dùng Mermaid parser.
