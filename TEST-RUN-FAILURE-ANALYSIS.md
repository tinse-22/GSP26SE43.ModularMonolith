# Test Run Failure Analysis

Source workbook: `report-00cbf754-d20b-45f7-91d5-0a55cf8d72ae.xlsx`  
Compared against: `swagger (1).json`, `TEST_REQUIREMENTS (1).md`

## Summary

| Metric | Count |
| --- | ---: |
| Total tests | 110 |
| Passed | 47 |
| Failed | 63 |
| Pass rate | 42.7% |

The largest failure group is not independent assertion failure. `41/63` failed cases are transport failures caused by the test execution circuit breaker opening after earlier target API errors. These later failures hide the actual API response and should be treated as cascade noise, not separate expected-result defects.

## Failure Breakdown

| Failure pattern | Count | Interpretation |
| --- | ---: | --- |
| `HTTP_REQUEST_ERROR`: circuit is open | 41 | Cascade failure from the TestExecution HTTP client circuit breaker. |
| `JSONPATH_ASSERTION_FAILED`: `$.success` expected `false`, actual `true` | 7 | Test data/expected mismatch, especially login negative cases that actually authenticate successfully. |
| `BODY_CONTAINS_MISSING`: expected `errors` | 5 | Success expectations were polluted with error-body assertions. |
| Expected `201`, actual `500` | 3 | Target API/server behavior or excessive boundary data causing server errors. |
| Expected `401`, actual `201` | 3 | Business-validation negative cases were incorrectly treated as auth failures. |
| Expected `400`, actual `201` | 1 | Generated request was not actually invalid for the target endpoint. |
| Expected `200`, actual `500` | 1 | Target API/server behavior. |
| Expected `201`, actual `409` | 1 | Test data uniqueness/setup collision. |
| Expected `401`, actual `500` | 1 | Mixed expected-status issue plus target API/server error. |

## Endpoint Hotspots

| Endpoint/status group | Failed count |
| --- | ---: |
| transport `/api/products/{id}` | 18 |
| transport `/api/categories/{id}` | 10 |
| `200` `/api/auth/login` | 7 |
| transport `/api/categories` | 5 |
| transport `/api/products` | 5 |
| `201` `/api/auth/register` | 4 |
| `201` `/api/products` | 3 |
| `500` `/api/categories` | 2 |
| `200` `/api/categories/{id}` | 2 |

## Root Causes

1. Swagger is incomplete for business errors. For example, product/category create/update endpoints often document only success plus `401`, while `TEST_REQUIREMENTS (1).md` describes `400`, `404`, and `409` cases. If the generator treats swagger statuses as the only allowed set, it coerces real business negatives into `401`.
2. Some generated negative cases are not actually negative. Several login tests expected failure but reused credentials that allowed successful login, so the API correctly returned `200`.
3. Success expectations include error assertions. Cases with `201` expected body fragments such as `errors`, which cannot be valid for a successful response shape.
4. Resource dependency setup is ambiguous. Some “non-existent” or duplicate scenarios reused IDs/names created earlier, causing `200`, `201`, or `409` instead of the intended status.
5. The TestExecution HTTP client circuit breaker converts later cases into transport failures, producing 41 cascade failures after earlier failures.

## Implemented Fix Direction

- Treat OpenAPI as the structural contract, but allow direct reviewed SRS/requirement statuses to supplement incomplete swagger response lists.
- Normalize expected assertions by status family:
  - success statuses remove error assertions such as `errors` and error JSON paths;
  - failure statuses remove success payload assertions such as `$.data.*` and token checks.
- Strengthen n8n prompt rules so LLM generation distinguishes auth-negative `401` from business-validation `400/404/409`, avoids fake negative cases, and preserves setup/non-existent resource intent.
- Prevent circuit-breaker cascade in the TestExecution named HTTP client by making the breaker effectively non-opening for test-run traffic.
