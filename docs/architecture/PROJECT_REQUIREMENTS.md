# API Testing Automation System - Project Requirements

> **Hệ thống kiểm thử API tự động với LLM hỗ trợ**

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Problem Context](#2-problem-context)
3. [Proposed Solution Architecture](#3-proposed-solution-architecture)
4. [User Roles & Responsibilities](#4-user-roles--responsibilities)
5. [Functional Requirements](#5-functional-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Project Scope & Limitations](#7-project-scope--limitations)
8. [Technical Architecture](#8-technical-architecture)
9. [Module Mapping](#9-module-mapping)

---

## 1. Project Overview

### Project Information

| Attribute | Value |
|-----------|-------|
| **Project Name (EN)** | API Testing Automation System |
| **Project Name (VN)** | Hệ thống kiểm thử API tự động |
| **Project Type** | Capstone Project |
| **Base Architecture** | Modular Monolith (.NET 10) with LLM Integration |

### Executive Summary

This project proposes an **automated API testing system** that combines:
- **Rule-based testing** for deterministic validation
- **Schema-based test generation** for contract conformance
- **Large Language Model assistance** for scenario suggestions and failure explanations

The system aims to:
- ✅ Automate API testing from documentation (OpenAPI/Swagger, Postman, Manual Entry)
- ✅ Generate comprehensive test cases (happy-path, boundary, negative)
- ✅ Execute tests with dependency-aware chaining
- ✅ Provide deterministic pass/fail evaluation with rule-based validation
- ✅ Offer LLM-assisted explanations for failed tests
- ✅ Generate detailed execution reports

---

## 2. Problem Context

### Current Challenges in API Testing

| Challenge | Description |
|-----------|-------------|
| **Manual Test Creation** | Time-consuming process of writing test cases for each endpoint |
| **Documentation Drift** | API behavior diverges from documentation over time |
| **Incomplete Coverage** | Difficulty ensuring all edge cases are tested |
| **Test Maintenance** | Keeping tests updated as APIs evolve |
| **Dependency Management** | Complex chained workflows (auth → protected endpoints) |
| **Result Interpretation** | Understanding why tests fail and what to fix |

### What This System Addresses

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    API TESTING AUTOMATION SYSTEM                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  ✅ Automated Test Generation    │  ✅ LLM-Assisted Explanations           │
│  ✅ Schema-Based Validation      │  ✅ Boundary & Negative Cases           │
│  ✅ Dependency-Aware Execution   │  ✅ Contract Conformance Checks         │
│  ✅ Multi-Source Input Support   │  ✅ Detailed Execution Reports          │
│  ✅ Deterministic Pass/Fail      │  ✅ Variable Extraction & Reuse         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Proposed Solution Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              USER INTERFACE                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │   Upload    │  │   Manual    │  │    cURL     │  │  Dashboard  │        │
│  │  OpenAPI/   │  │   Entry     │  │   Import    │  │  & Reports  │        │
│  │  Postman    │  │             │  │             │  │             │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PROCESSING LAYER                                   │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                    Documentation Parser                               │  │
│  │  • OpenAPI/Swagger Parser                                            │  │
│  │  • Postman Collection Parser                                         │  │
│  │  • cURL Command Parser                                               │  │
│  │  • Manual Entry Normalizer                                           │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                      │                                      │
│                                      ▼                                      │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                    Unified Internal Model                             │  │
│  │  • Endpoints, Methods, Parameters                                    │  │
│  │  • Request/Response Schemas                                          │  │
│  │  • Security Requirements                                             │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        TEST GENERATION ENGINE                               │
│  ┌─────────────────────────────┐  ┌─────────────────────────────────────┐  │
│  │   Rule-Based Generator      │  │   LLM-Assisted Scenario Suggester   │  │
│  │  • Happy-path cases         │  │  • Boundary case suggestions        │  │
│  │  • Schema-derived inputs    │  │  • Edge case identification        │  │
│  │  • Required param tests     │  │  • Domain-specific scenarios       │  │
│  └─────────────────────────────┘  └─────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Mutation Engine                                   │   │
│  │  • Missing required fields  • Out-of-range values  • Invalid types  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        TEST EXECUTION ENGINE                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                 Dependency-Aware Executor                            │   │
│  │  • Authentication flow handling                                      │   │
│  │  • Variable extraction (tokens, IDs)                                │   │
│  │  • Request chaining and reuse                                       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │               Rule-Based Validator (Deterministic)                   │   │
│  │  • HTTP status code verification                                    │   │
│  │  • Response schema validation                                       │   │
│  │  • Contract conformance checks                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         REPORTING & INSIGHTS                                │
│  ┌─────────────────────────────┐  ┌─────────────────────────────────────┐  │
│  │   Execution Reports         │  │   LLM Failure Explanations          │  │
│  │  • Coverage summaries       │  │  • Mismatch analysis               │  │
│  │  • Run history              │  │  • Plausible cause suggestions     │  │
│  │  • Failure details & logs   │  │  • Documentation comparison        │  │
│  │  • PDF/CSV export           │  │  (Does NOT affect pass/fail)       │  │
│  └─────────────────────────────┘  └─────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. User Roles & Responsibilities

| Role | Responsibilities |
|------|------------------|
| **Admin** | System configuration, user management, subscription management |
| **Developer** | Upload API docs, configure tests, execute tests, view reports |
| **Tester** | Execute tests, review results, export reports |
| **Viewer** | View test results and reports (read-only) |

---

## 5. Functional Requirements

### 5.1 Authentication & Authorization
| ID | Requirement |
|----|-------------|
| **FE-01** | Provide secure user authentication and role-based access control to manage access to API documentation, test configurations, test execution, and execution results |

### 5.2 API Input Management
| ID | Requirement |
|----|-------------|
| **FE-02** | Allow users to upload, store, and manage API input sources as primary inputs for test generation, including OpenAPI/Swagger specifications, Postman collection files, and manual API definitions with optional cURL import |
| **FE-03** | Parse and normalize uploaded API inputs into a unified internal model by extracting endpoints, HTTP methods, parameters, request and response schemas, and documented security requirements |

### 5.3 Test Configuration
| ID | Requirement |
|----|-------------|
| **FE-04** | Enable configuration of test scope and execution settings, including selection of target endpoints, definition of execution environments (e.g., base URLs), and specification of authentication mechanisms and request headers |

### 5.4 Test Generation
| ID | Requirement |
|----|-------------|
| **FE-05** | Automatically generate happy-path API test cases for each endpoint using schema-compliant valid inputs and required parameters |
| **FE-06** | Generate boundary and negative API test cases using deterministic, rule-based mutations (such as missing required fields, invalid data types, and out-of-range values), supplemented by LLM-assisted test scenario suggestions grounded in the provided API documentation |

### 5.4.1 LLM Suggestion Review
| ID | Requirement |
|----|-------------|
| **FE-15** | Provide a review interface for LLM-suggested test cases that allows users to preview, approve, reject, or modify individual suggestions before adding them to the test suite |
| **FE-16** | Allow users to provide feedback on LLM-generated test case suggestions, such as marking them as helpful or not helpful and adding optional notes, to improve future suggestion quality through prompt refinement |
| **FE-17** | Support bulk approval or rejection of LLM-suggested test cases, with filtering options by suggestion type, including boundary, negative, and edge case scenarios |

### 5.5 Test Execution
| ID | Requirement |
|----|-------------|
| **FE-07** | Execute API tests in a dependency-aware manner, supporting chained workflows such as authentication or token acquisition followed by protected API calls, with support for variable extraction and reuse across requests |
| **FE-08** | Perform deterministic test execution and pass/fail evaluation using rule-based validation, including HTTP status code verification, response structure and schema validation, and contract conformance checks |

### 5.6 LLM Assistance
| ID | Requirement |
|----|-------------|
| **FE-09** | Provide LLM-assisted explanations for failed test cases by summarizing observed discrepancies between actual responses and documented expectations, and suggesting plausible causes, without influencing deterministic pass/fail outcomes |

### 5.7 Reporting
| ID | Requirement |
|----|-------------|
| **FE-10** | Generate comprehensive test execution reports, including coverage summaries, execution history, failure details, and logs, and support exporting results in PDF and CSV formats |

### 5.8 Manual Entry Mode
| ID | Requirement |
|----|-------------|
| **FE-11** | Provide a Manual Entry (Enter API Details) mode that allows users to define API requests by specifying HTTP method, endpoint URL, headers, path parameters, query parameters, and request body, enabling test generation without OpenAPI or Postman artifacts |
| **FE-12** | Support path-parameter templating in manually defined endpoints (for example, `/resource/{id}`) and bind placeholders to user-provided sample values through structured Path Parameters input for deterministic test generation and execution |
| **FE-13** | Provide a cURL import capability that parses pasted cURL commands into HTTP method, URL, headers, request body, and related parameters to quickly bootstrap API definitions and test generation |

### 5.9 Subscription & Billing
| ID | Requirement |
|----|-------------|
| **FE-14** | Provide subscription and billing management capabilities, including plan lifecycle operations (trial, upgrade, downgrade, cancellation), plan-based limits and usage tracking (such as projects, endpoints, test runs, and concurrency), and integration with a third-party payment provider for checkout, recurring billing, and invoicing, without storing raw cardholder data

---

## 6. Non-Functional Requirements

| ID | Category | Requirement |
|----|----------|-------------|
| **NFR-01** | Performance | Test execution should process at least 100 test cases per minute |
| **NFR-02** | Scalability | Support concurrent test execution for multiple projects |
| **NFR-03** | Reliability | 99.5% uptime for the testing platform |
| **NFR-04** | Security | Secure storage of API credentials and tokens |
| **NFR-05** | Usability | Intuitive UI for test configuration and result review |
| **NFR-06** | Maintainability | Modular architecture for easy feature extension |

---

## 7. Project Scope & Limitations

### 7.1 Major Features (In Scope)

| ID | Feature |
|----|---------|
| **FE-01** | User authentication and role-based access control |
| **FE-02** | OpenAPI/Swagger, Postman, Manual Entry, cURL import support |
| **FE-03** | Documentation parsing into unified internal model |
| **FE-04** | Test scope and execution configuration |
| **FE-05** | Happy-path test case generation |
| **FE-06** | Boundary and negative test case generation |
| **FE-07** | Dependency-aware test execution with variable extraction |
| **FE-08** | Deterministic rule-based validation |
| **FE-09** | LLM-assisted failure explanations |
| **FE-10** | Execution reports with PDF/CSV export |
| **FE-11** | Manual Entry mode for API definition |
| **FE-12** | Path-parameter templating support |
| **FE-13** | cURL import option |
| **FE-14** | Subscription and billing management |
| **FE-15** | LLM suggestion review interface (preview, approve, reject, modify) |
| **FE-16** | User feedback on LLM suggestions for quality improvement |
| **FE-17** | Bulk approval/rejection with filtering by suggestion type |

### 7.2 Limitations & Exclusions

| ID | Limitation |
|----|------------|
| **LI-01** | The system supports API inputs via OpenAPI/Swagger specifications, Postman collections, and manual API definition entry (including optional cURL import); arbitrary free-form documents (e.g., unstructured PDF/Word specifications) are out of scope |
| **LI-02** | The system focuses on API-level testing and does not include UI testing or end-to-end testing of front-end applications |
| **LI-03** | Deep performance testing (load, stress, endurance) is excluded; the project prioritizes functional and contract-based validation |
| **LI-04** | Advanced security testing (penetration testing, vulnerability scanning, fuzzing) is excluded; security handling is limited to executing documented authentication/authorization mechanisms |
| **LI-05** | The system does not infer domain-specific business rules or complex data dependencies beyond what is explicitly described in provided schemas/examples or user-entered inputs; domain-correct test data may require user configuration |
| **LI-06** | The system does not automatically fix bugs, modify API implementations, or generate code patches; it reports detected issues and test outcomes only |
| **LI-07** | The LLM component is not used to determine pass/fail outcomes and does not override rule-based validation; its use is limited to documentation interpretation, scenario suggestion, and failure explanation |
| **LI-08** | The system does not aim to fully replicate advanced custom scripting capabilities found in existing tools; only predefined mechanisms for variable extraction and dependency chaining are supported |
| **LI-09** | Test coverage and accuracy depend on the completeness and correctness of the provided documentation or manual inputs and the availability of the target environment; the system does not guarantee exhaustive coverage when specifications are incomplete or inconsistent |
| **LI-10** | Manual Entry supports JSON, form-data, and URL-encoded request bodies; XML support (if enabled) is limited, and non-HTTP API styles such as gRPC/GraphQL are out of scope for the initial release |

---

## 8. Technical Architecture

### 8.1 Technology Stack

| Layer | Technology |
|-------|------------|
| **Backend** | .NET 10, C#, ASP.NET Core |
| **Database** | PostgreSQL |
| **Message Broker** | RabbitMQ |
| **Caching** | Redis |
| **LLM Integration** | OpenAI API / Azure OpenAI |
| **Payment** | Stripe / Third-party payment provider |
| **Container** | Docker, Docker Compose |
| **Observability** | .NET Aspire, OpenTelemetry |

### 8.2 Architecture Patterns

| Pattern | Usage |
|---------|-------|
| **Modular Monolith** | Single deployment with well-defined module boundaries |
| **CQRS** | Separate read/write operations via Dispatcher |
| **Clean Architecture** | Domain → Application → Infrastructure layering |
| **Outbox Pattern** | Reliable event publishing |
| **Repository Pattern** | Data access abstraction |

---

## 9. Module Mapping

### 9.1 Core Business Modules (New)

| Module | Purpose | Features Covered |
|--------|---------|------------------|
| **ApiDocumentation** | API input management | FE-02, FE-03, FE-11, FE-12, FE-13 |
| **TestGeneration** | Test case generation | FE-05, FE-06, FE-15, FE-16, FE-17 |
| **TestExecution** | Test execution engine | FE-07, FE-08 |
| **TestReporting** | Reports and exports | FE-10 |
| **LlmAssistant** | LLM integration | FE-06 (partial), FE-09 |
| **Subscription** | Billing management | FE-14 |

### 9.2 Existing Modules (Reused)

| Existing Module | Purpose in API Testing System |
|-----------------|-------------------------------|
| **Identity** | User authentication, roles, permissions (FE-01) |
| **Storage** | Store uploaded API documentation files |
| **Notification** | Send test completion notifications, alerts |
| **AuditLog** | Track all user actions and test executions |
| **Configuration** | Application settings management |

### 9.3 Module Dependencies

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MODULE DEPENDENCIES                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐                                                           │
│   │  Identity   │◄──────────────────────────────────────────────────────┐   │
│   └─────────────┘                                                       │   │
│          │                                                              │   │
│          ▼                                                              │   │
│   ┌─────────────────┐      ┌─────────────────┐                         │   │
│   │ ApiDocumentation│─────►│  TestGeneration │                         │   │
│   │ (Upload/Parse)  │      │  (Generate)     │                         │   │
│   └─────────────────┘      └─────────────────┘                         │   │
│          │                         │                                    │   │
│          │                         ▼                                    │   │
│          │                 ┌─────────────────┐      ┌─────────────┐    │   │
│          │                 │  TestExecution  │─────►│ LlmAssistant│    │   │
│          │                 │  (Execute)      │      │ (Explain)   │    │   │
│          │                 └─────────────────┘      └─────────────┘    │   │
│          │                         │                                    │   │
│          ▼                         ▼                                    │   │
│   ┌─────────────┐          ┌─────────────────┐                         │   │
│   │   Storage   │          │  TestReporting  │                         │   │
│   │ (Files)     │          │  (Reports)      │                         │   │
│   └─────────────┘          └─────────────────┘                         │   │
│          │                         │                                    │   │
│          └──────────┬──────────────┘                                   │   │
│                     ▼                                                   │   │
│              ┌─────────────┐      ┌─────────────┐                      │   │
│              │  AuditLog   │      │Subscription │◄─────────────────────┘   │
│              └─────────────┘      │  (Billing)  │                          │
│                                   └─────────────┘                          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 10. Glossary

| Term | Definition |
|------|------------|
| **OpenAPI/Swagger** | Standard specification for describing RESTful APIs |
| **Postman Collection** | JSON file containing API requests and test configurations |
| **Happy-Path Test** | Test case using valid inputs expecting successful response |
| **Boundary Test** | Test case using edge-case values (min, max, limits) |
| **Negative Test** | Test case using invalid inputs expecting error response |
| **Contract Conformance** | Validation that API behavior matches documented specification |
| **Variable Extraction** | Capturing values from responses for use in subsequent requests |
| **Dependency Chaining** | Executing tests in order where later tests depend on earlier results |
