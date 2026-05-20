# LLM-generated testcase type mismatch analysis

Source report: `report-09306675-7860-4524-9713-4ad11381d449.htm`  
Generated at: `2026-05-20 03:04:17Z`  
Suite: `10 endpoints selected V2`

## Summary

The run executed all scoped endpoints, but `31/110` tests failed. The failures are not all target API defects. A significant group is caused by LLM-generated test cases that contain invalid request payloads, mismatched placeholders, or incorrect expectations.

| Metric | Count |
| --- | ---: |
| Total tests | 110 |
| Passed | 79 |
| Failed | 31 |
| Skipped | 0 |
| Pass rate | 71.8% |

Failure distribution from the report:

| Failure code | Count |
| --- | ---: |
| `JSONPATH_ASSERTION_FAILED` | 19 |
| `STATUS_CODE_MISMATCH` | 17 |
| `BODY_CONTAINS_MISSING` | 3 |

The most actionable defect is the type mismatch pattern where fields that should be `number` or `integer` receive string placeholders such as `{{productId}}`. This makes the test fail before it can validate the intended API behavior.

## Evidence from the report

### Product update payloads use ID placeholders as numeric values

Several `/api/products/{id}` update tests use `{{productId}}` in numeric fields:

| Case | Name | Request problem | Actual result |
| ---: | --- | --- | --- |
| 75 | HappyPath: Update Product Price and Stock | `name` and `categoryId` use `{{productId}}`; `categoryId` should use a category ID | `404 Category not found` |
| 76 | Boundary: Update Product Stock to 0 | `price` is `"{{productId}}"` | `400 Expected number, received string` |
| 77 | Boundary: Update Product Price to 0 | `stock` is `"{{productId}}"` | `400 Expected number, received string` |
| 88 | HappyPath: Update only product Name | `price` and `stock` are `"{{productId}}"`; `categoryId` is also `{{productId}}` | `400 Expected number, received string` |

These are generated-test defects. `productId` is a resource identifier string. It must not be used for `price`, `stock`, or `categoryId`.

### Negative login cases reuse valid credentials

Cases `16`, `17`, `20`, `21`, `23`, `25`, `26`, and `27` are named as negative login scenarios, but the executed request still uses valid registered credentials:

```json
{"email":"{{registeredEmail}}","password":"***MASKED***"}
```

The API correctly returns successful login responses:

```json
{"success":true,"message":"Login successful","data":{"token":"***MASKED***"}}
```

The test then fails because the assertion expects `$.success == false`. These cases are not valid negative tests unless the request actually changes the email or password to an invalid value.

### JavaScript expressions are emitted inside JSON bodies

Some boundary cases contain JavaScript-like expressions instead of serialized JSON values:

```json
{"email": "long_pass_{{tcUniqueId}}@test.com", "password": "***MASKED***".repeat(100)}
```

This is not a valid JSON literal for an API request body. The generator must emit the final string value, or the execution layer must reject the testcase before running it.

### Expected statuses are coerced into the wrong class

Multiple business-validation or security-content tests expect `401` even though the request is authenticated:

| Case | Name | Actual status | Likely issue |
| ---: | --- | ---: | --- |
| 42 | Negative: XSS in Category Name | 201 | Expected auth failure, but request has valid auth |
| 43 | Negative: HTML in Description | 201 | Expected auth failure, but request has valid auth |
| 44 | Negative: Category Body with extra fields | 201 | Extra fields are accepted or ignored by API |
| 50 | Negative: Invalid Category ID Format | 201 | Request reused a valid `{{categoryId}}` |
| 51 | Negative: Non-existent Category ID | 201 | Request reused a valid `{{categoryId}}` |
| 58 | Negative: Product name with HTML | 201 | API accepts the payload |

These are oracle-generation problems. `401` should be reserved for missing or invalid authentication. Business validation should use `400`, `404`, `409`, or the status documented by SRS/OpenAPI.

### Body contains assertions still include unresolved placeholders

Cases `91`, `96`, and `98` fail because the report expects literal unresolved placeholders in the response body:

| Case | Name | Failed assertion |
| ---: | --- | --- |
| 91 | HappyPath: Verify created category exists | response should contain `{{categoryId}}` |
| 96 | HappyPath: Verify created product exists | response should contain `{{productId}}` |
| 98 | HappyPath: Product relationship check | response should contain `{{categoryId}}` |

Assertions should resolve placeholders before comparison or use JSONPath checks against the resolved variable value.

## Current implementation risk

The current execution path allows LLM-generated payload defects to reach the target API.

- `VariableResolver` resolves placeholders in the request body, then uses LLM-sourced bodies as provided. It intentionally skips the non-LLM normalization pipeline for LLM-sourced test cases.
- `LlmScenarioSuggester` tells the LLM to use cross-test variables such as `{{fieldName}}`, `{{productId}}`, and `{{categoryId}}`, but the rules do not strongly enforce schema-compatible placeholder use.
- `PreExecutionValidator` has semantic helpers for numeric and identifier variable names, but the current validation does not block payloads where a numeric schema field receives an identifier placeholder.
- `SaveAiGeneratedTestCasesCommand` validates placeholder availability, but availability is not enough. A placeholder can exist and still be semantically wrong for the target field.

The result is false failures: the test runner reports API validation errors, but the real defect is the generated testcase payload.

## Code-ready fix direction

### 1. Add schema-aware request body validation

Add a validation step before executing LLM-generated tests, and preferably also before persisting AI-generated test cases.

The validator should compare each request body field against the OpenAPI request schema:

- For `number` and `integer` fields:
  - Allow JSON numeric literals, for example `499.0`, `0`, `100`.
  - Allow numeric-semantic placeholders only when they resolve to numeric values, for example `{{price}}`, `{{stock}}`, `{{quantity}}`.
  - Reject identifier placeholders such as `{{productId}}`, `{{categoryId}}`, `{{id}}`.
  - Reject quoted non-numeric strings such as `"cheap"`, except in negative tests that intentionally validate wrong type and expect `400`.
- For identifier/string fields:
  - Allow resource-compatible placeholders, for example `categoryId: "{{categoryId}}"`.
  - Reject cross-resource placeholders, for example `categoryId: "{{productId}}"`.
- For all JSON bodies:
  - Reject JavaScript expressions such as `.repeat(100)`.
  - Reject bodies that cannot be parsed as JSON when `bodyType` is `JSON`.

Recommended failure mode:

- Before execution: mark the testcase as failed/skipped by pre-validation with a clear code such as `REQUEST_SCHEMA_TYPE_MISMATCH`.
- Before persistence: reject or mark the generated testcase as needing repair so it does not become a runnable false-failure testcase.

### 2. Strengthen LLM prompt rules

Update `LlmScenarioSuggester` rules so generated bodies obey the request schema:

- Numeric fields must be emitted as JSON numbers, not strings.
- Placeholder names must match the semantic type of the target field.
- Resource ID placeholders must match the field resource name.
- Boundary strings must be serialized as concrete JSON strings, not expressions like `.repeat(100)`.
- Negative tests must make the request invalid in the dimension being tested; they must not reuse valid happy-path credentials unchanged.

Example prompt rule:

```text
For body fields with OpenAPI type number/integer, use numeric JSON literals or numeric-semantic placeholders only, such as {{price}} or {{stock}}. Never use ID placeholders like {{productId}}, {{categoryId}}, or {{id}} in numeric fields.
```

### 3. Add generation/save reconciliation

During `SaveAiGeneratedTestCasesCommand`, add a reconciliation pass that validates generated scenarios against endpoint metadata before saving:

- Reject missing required body fields for happy-path and boundary-success cases.
- Reject schema type mismatches unless the test is explicitly a negative wrong-type case expecting `400`.
- Rewrite obviously wrong placeholder candidates only when the replacement is unambiguous.
- Preserve intended negative cases, but require the expected status and assertions to match the invalid input.

This prevents bad AI output from entering the suite as if it were valid test design.

### 4. Fix expectation resolution rules

Normalize expected statuses and assertions based on request intent:

- `401` only for missing/invalid auth tests.
- `400` for schema/type/required-field validation.
- `404` for not-found resource cases using guaranteed non-existent IDs.
- `409` for duplicate/conflict cases using a previously created value.
- Success statuses must not include error assertions like `errors`.
- Failure statuses must not assert success payload fields such as `$.data.token`.
- Body contains checks must resolve placeholders or be replaced with JSONPath checks.

## Proposed test plan

### Unit tests for pre-execution/schema validation

Add focused tests covering:

- `price: "{{productId}}"` is rejected with `REQUEST_SCHEMA_TYPE_MISMATCH`.
- `stock: "{{productId}}"` is rejected with `REQUEST_SCHEMA_TYPE_MISMATCH`.
- `price: "{{price}}"` passes if `price` resolves to a numeric value.
- `stock: "{{stock}}"` passes if `stock` resolves to an integer value.
- `categoryId: "{{productId}}"` is rejected as a resource placeholder mismatch.
- JSON body containing `"x".repeat(100)` is rejected as invalid JSON/expression payload.
- Wrong-type negative case such as `price: "cheap"` is allowed only when the expected status is `400`.

### Unit tests for generation/save pipeline

Add tests for AI-generated testcase persistence:

- A happy-path product update with `price: "{{productId}}"` is rejected or marked as invalid.
- A negative login testcase that reuses both `{{registeredEmail}}` and `{{registeredPassword}}` while expecting failure is rejected.
- A not-found testcase must use a guaranteed non-existent identifier, not an existing `{{categoryId}}` or `{{productId}}`.
- A duplicate testcase must reuse the conflicting field value, not generate a new unique value.

### Regression run

After implementing the fixes:

1. Regenerate the 10-endpoint suite.
2. Run the suite again.
3. Confirm the product update failures no longer include `Expected number, received string` caused by `{{productId}}`.
4. Confirm invalid AI-generated payloads fail before execution with actionable pre-validation messages.
5. Review remaining failures as target API behavior or explicit requirement mismatches, not generator payload defects.

## Completion criteria

The fix should be considered complete when:

- LLM-generated numeric fields no longer receive ID placeholders in runnable tests.
- Invalid JSON expressions are blocked before HTTP execution.
- Negative login tests use genuinely invalid credentials.
- Business-validation tests no longer expect `401` unless auth is actually missing or invalid.
- Placeholder-based assertions compare against resolved values, not literal `{{variableName}}` strings.
- New tests cover pre-execution validation and generation/save reconciliation.

## Repo rule impact

This report is documentation only. It does not require:

- EF Core migration verification.
- Dockerfile or compose registration checks.
- `docker compose config`.
- GitNexus impact or detect-changes gates.

If the proposed code fixes are implemented later, that follow-up task becomes `Application code only`. It must use GitNexus before editing existing symbols and run `gitnexus_detect_changes` before completion. EF/Docker gates are still not expected unless the implementation touches EF models, module registration, project references, or runtime wiring.
