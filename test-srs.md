# SRS for ClassifiedAds System

## Functional Requirements

### FR-01: User Authentication
Users must be able to login with email and password.
- Constraints: Password must be at least 8 characters.
- Mapped Endpoint: POST /api/auth/login

### FR-02: Product Management
Admins must be able to create, update, and delete products.
- Constraints: Product name is required.
- Mapped Endpoint: POST /api/products

### FR-03: SRS Analysis
The system must support uploading SRS documents and analyzing them using LLM to extract requirements and generate test cases.
- Mapped Endpoint: POST /api/projects/{projectId}/srs-documents