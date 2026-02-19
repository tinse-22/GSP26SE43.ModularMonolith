# 📚 Documentation Index

Tài liệu dự án **GSP26SE43.ModularMonolith** — được tổ chức theo chức năng.

## 📁 Cấu trúc thư mục

### `_templates/` — JSON Templates chuẩn cho AI Agent
Template để AI Agent tạo docs đúng format. **Bắt buộc** đọc trước khi tạo docs mới.

| Template | Schema | Mục đích |
|----------|--------|----------|
| [requirement-template.json](_templates/requirement-template.json) | `ai-agent-requirement-spec` | Định nghĩa requirement (FE-xx) |
| [contract-template.json](_templates/contract-template.json) | `ai-agent-contract-spec` | Implementation guide & code contracts |
| [workflow-template.json](_templates/workflow-template.json) | `ai-agent-workflow-spec` | Workflow flows & checklists |
| [feature-spec-template.json](_templates/feature-spec-template.json) | `feature-specification` | Feature specs (architecture, entities, DTOs) |

---

### `architecture/` — Kiến trúc tổng quan

#### Tổng quan & Cấu trúc
- [README.md](architecture/README.md) — Index tài liệu kiến trúc
- [CODEBASE_STRUCTURE_AND_FEATURE_GUIDE.md](architecture/CODEBASE_STRUCTURE_AND_FEATURE_GUIDE.md) — Hướng dẫn cấu trúc codebase
- [ENTITIES.md](architecture/ENTITIES.md) — Mô tả entities
- [ERD_ANALYSIS.md](architecture/ERD_ANALYSIS.md) — Phân tích ER Diagram
- [ARCHITECTURE-INVENTORY.md](architecture/ARCHITECTURE-INVENTORY.md) — Inventory kiến trúc

#### Kiến trúc chi tiết (11 chương)
- [01-solution-structure.md](architecture/01-solution-structure.md) — Cấu trúc solution
- [02-architecture-overview.md](architecture/02-architecture-overview.md) — Tổng quan kiến trúc
- [03-request-lifecycle.md](architecture/03-request-lifecycle.md) — Vòng đời request
- [04-cqrs-and-mediator.md](architecture/04-cqrs-and-mediator.md) — CQRS & Mediator pattern
- [05-persistence-and-transactions.md](architecture/05-persistence-and-transactions.md) — Persistence & Transactions
- [06-events-and-outbox.md](architecture/06-events-and-outbox.md) — Events & Outbox pattern
- [07-modules.md](architecture/07-modules.md) — Modules
- [08-authentication-authorization.md](architecture/08-authentication-authorization.md) — Authentication & Authorization
- [09-observability-and-crosscutting.md](architecture/09-observability-and-crosscutting.md) — Observability & Cross-cutting
- [10-devops-and-local-development.md](architecture/10-devops-and-local-development.md) — DevOps & Local Development
- [11-extension-playbook.md](architecture/11-extension-playbook.md) — Extension Playbook

#### Patterns & Validation
- [VALIDATION-REPORT.md](architecture/VALIDATION-REPORT.md) — Báo cáo validation
- [VALIDATION_AND_RESULT_PATTERN.md](architecture/VALIDATION_AND_RESULT_PATTERN.md) — Pattern validation & result
- [appendix-glossary.md](architecture/appendix-glossary.md) — Thuật ngữ

#### Audit & Migration
- [AUDIT_FOUNDATIONS.md](architecture/AUDIT_FOUNDATIONS.md) — Nền tảng audit
- [MIGRATION_PLAN.md](architecture/MIGRATION_PLAN.md) — Kế hoạch migration

#### Yêu cầu dự án & Module Reports
- [PROJECT_REQUIREMENTS.md](architecture/PROJECT_REQUIREMENTS.md) — Yêu cầu tổng quan dự án
- [USER_MODULE_AUDIT_REPORT.md](architecture/USER_MODULE_AUDIT_REPORT.md) — Báo cáo audit User module
- [USER_MODULE_IMPLEMENTATION_SUMMARY.md](architecture/USER_MODULE_IMPLEMENTATION_SUMMARY.md) — Tóm tắt implement User module

#### Testing
- [testing.md](architecture/testing.md) — Hướng dẫn testing

---

### `features/` — Docs theo Feature (FE-xx)

#### FE-02: API Documentation Management
- [requirement.json](features/FE-02-api-documentation/requirement.json) — Requirement tổng
- [workflow.json](features/FE-02-api-documentation/workflow.json) — Workflow flows
- **FE-02-01 (Project CRUD)**: [requirement](features/FE-02-api-documentation/FE-02-01/requirement.json) · [contracts](features/FE-02-api-documentation/FE-02-01/contracts.json)
- **FE-02-02 (Spec Upload)**: [requirement](features/FE-02-api-documentation/FE-02-02/requirement.json) · [contracts](features/FE-02-api-documentation/FE-02-02/contracts.json)

#### FE-03: Parser Flow
- [parser-flow-design.json](features/FE-03-parser-flow/parser-flow-design.json)

#### FE-04: Test Configuration
- [requirement.json](features/FE-04-test-configuration/requirement.json) — Requirement tong
- [workflow.json](features/FE-04-test-configuration/workflow.json) — Workflow flows
- [implementation-map.json](features/FE-04-test-configuration/implementation-map.json) — File map file-level cho AI Agent implement dung codebase
- [OPERATIONS.md](features/FE-04-test-configuration/OPERATIONS.md) — Runbook van hanh FE-04 (scope, environment, conflict handling, checklist)
- **FE-04-01**: [requirement](features/FE-04-test-configuration/FE-04-01/requirement.json) · [contracts](features/FE-04-test-configuration/FE-04-01/contracts.json)
- **FE-04-02**: [requirement](features/FE-04-test-configuration/FE-04-02/requirement.json) · [contracts](features/FE-04-test-configuration/FE-04-02/contracts.json)

#### FE-09: Acceptance Criteria
- **FE-09-01**: [requirement](features/FE-09-acceptance-criteria/FE-09-01/requirement.json) · [contracts](features/FE-09-acceptance-criteria/FE-09-01/contracts.json) · [workflow](features/FE-09-acceptance-criteria/FE-09-01/workflow.json)

#### FE-12: Path Parameter Templating
- [requirement.json](features/FE-12-path-parameter-templating/requirement.json) — Requirement tổng
- [workflow.json](features/FE-12-path-parameter-templating/workflow.json) — Workflow flows
- **FE-12-01**: [requirement](features/FE-12-path-parameter-templating/FE-12-01/requirement.json) · [contracts](features/FE-12-path-parameter-templating/FE-12-01/contracts.json)
- **FE-12-02**: [requirement](features/FE-12-path-parameter-templating/FE-12-02/requirement.json) · [contracts](features/FE-12-path-parameter-templating/FE-12-02/contracts.json)
- **FE-12-03**: [requirement](features/FE-12-path-parameter-templating/FE-12-03/requirement.json) · [contracts](features/FE-12-path-parameter-templating/FE-12-03/contracts.json)

#### FE-14: Subscription & Billing
- **FE-14-01**: [admin-plan-management.md](features/FE-14-subscription-billing/FE-14-01/admin-plan-management.md)
- **FE-14-02**: [acceptance-criteria.json](features/FE-14-subscription-billing/FE-14-02/acceptance-criteria.json)

---

### `api-reference/` — API Specs & Guides
- [webapi-v1.json](api-reference/webapi-v1.json) — OpenAPI spec
- [api-projects-specifications-endpoints-param-guide.json](api-reference/api-projects-specifications-endpoints-param-guide.json) — API endpoints guide
- [USER_API_REFERENCE.md](api-reference/USER_API_REFERENCE.md) — User module API reference

---

### `payment/` — Payment Feature Specs
- [architecture.json](payment/architecture.json) — Part 1: Architecture & Entities
- [dtos-repos-config.json](payment/dtos-repos-config.json) — Part 2: DTOs, Repos, Config
- [services-controllers.json](payment/services-controllers.json) — Part 3: Services & Controllers

---

### `guides/` — Workflow Guides & Hướng dẫn
- [AI_AGENT_WORKFLOW.md](guides/AI_AGENT_WORKFLOW.md) — Quy trình AI Agent
- [APIDOC_WORKFLOW_GUIDE.md](guides/APIDOC_WORKFLOW_GUIDE.md) — Workflow API Documentation
- [SUBSCRIPTION_WORKFLOW_GUIDE.md](guides/SUBSCRIPTION_WORKFLOW_GUIDE.md) — Workflow Subscription
- [HOW_TO_RUN.md](guides/HOW_TO_RUN.md) — Hướng dẫn chạy project
- [ENVIRONMENT_VARIABLES.md](guides/ENVIRONMENT_VARIABLES.md) — Biến môi trường
- [PROJECT_GUIDE.md](guides/PROJECT_GUIDE.md) — Hướng dẫn tổng quan project
- [TESTING_IMPLEMENTATION_SUMMARY.md](guides/TESTING_IMPLEMENTATION_SUMMARY.md) — Tóm tắt testing implementation

---

### `ci-cd/` — CI/CD
- [CI_CD.md](ci-cd/CI_CD.md) — CI/CD pipeline
- [DOCKER_WARMUP_REPORT.md](ci-cd/DOCKER_WARMUP_REPORT.md) — Báo cáo Docker warmup

---

### `tracking/` — Progress Tracking
- [FE_COMPLETION_TRACKER.md](tracking/FE_COMPLETION_TRACKER.md)
- [docs_requirements.txt](tracking/docs_requirements.txt)

---

## 📝 Quy tắc tạo docs mới cho AI Agent

1. **File format**: JSON
2. **Chọn template** phù hợp từ `_templates/`
3. **Đặt file** vào đúng folder theo category:
   - Feature mới → `features/FE-XX-[tên]/`
   - Sub-requirement → `features/FE-XX-[tên]/FE-XX-YY/`
   - Architecture docs → `architecture/`
   - Guides & How-to → `guides/`
   - API specs → `api-reference/`
   - CI/CD → `ci-cd/`
4. **Naming convention**:
   - Requirement: `requirement.json`
   - Contracts: `contracts.json`
   - Workflow: `workflow.json`
   - Feature spec: `[tên-ngắn].json`
5. **Luôn cập nhật** file này (README.md) khi thêm docs mới
