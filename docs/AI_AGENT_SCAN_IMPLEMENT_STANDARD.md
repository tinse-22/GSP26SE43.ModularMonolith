# AI Agent Scan and Implement Standard

## 1. Purpose

This document defines a standard workflow so an AI Agent can:

1. scan the codebase safely,
2. identify gaps,
3. implement changes,
4. and produce a report-only output (Markdown or JSON).

Primary quality goal for test generation:

- each API endpoint must have at least 10 test cases,
- not only 3 base cases.

## 2. Target Coverage Standard (Per API Endpoint)

Minimum distribution per endpoint:

1. HappyPath: 2 cases
2. Boundary: 3 cases
3. Negative: 5 cases

Minimum total: 10 test cases per endpoint.

Recommended coverage matrix:

1. HappyPath
- minimal valid payload
- full valid payload

2. Boundary
- min constraint boundary
- max constraint boundary
- empty or near-empty boundary

3. Negative
- missing required field
- type mismatch
- invalid enum
- malformed body
- auth or permission failure (401 or 403) or resource-not-found (404)

## 3. Mandatory Agent Workflow

### Phase A - Scan Only (No code changes)

Agent must do all items below before implementation:

1. find current generation rules and prompts,
2. map endpoint -> generated case count,
3. group by test type (HappyPath, Boundary, Negative),
4. detect duplicated scenarios,
5. identify where coverage is below 10.

Required outputs in scan phase:

1. gap table by endpoint,
2. root cause notes,
3. implementation proposal,
4. risk list and rollback notes.

### Phase B - Implementation

Agent should implement in this order:

1. update generation instruction rules to require at least 10 per endpoint,
2. add post-generation validation gate to reject endpoint groups below 10,
3. enforce coverage category presence (2 HappyPath, 3 Boundary, 5 Negative minimum),
4. add duplicate scenario guard,
5. regenerate and verify resulting distribution.

### Phase C - Verification

Agent must verify:

1. each endpoint has >= 10 cases,
2. each endpoint meets type distribution,
3. no category is empty,
4. no obvious duplicate scenario names or mutation intents,
5. generation still completes without breaking existing flow.

## 4. Repo Safety Gates

If implementation touches DB model, migration, or module wiring, the agent must follow repo gates:

1. migration freshness verification,
2. Docker and compose registration checks,
3. compose config validation.

If task is docs-only, those steps are not required.

## 5. Report-Only Output Contract

The agent must output report files only:

1. Markdown report (.md), or
2. JSON report (.json).

No extra non-report artifacts are allowed in this workflow contract.

## 6. Markdown Report Template

Use this template for final report:

```md
# Scan and Implementation Report

## Scope
- Branch:
- Target module:
- Goal: >= 10 test cases per endpoint

## Scan Summary
- Endpoints scanned:
- Total generated cases observed:
- Endpoints below threshold:

## Gap Matrix
| Endpoint | Current Total | HappyPath | Boundary | Negative | Missing To 10 | Notes |
|---|---:|---:|---:|---:|---:|---|

## Root Causes
1.
2.

## Changes Implemented
1.
2.
3.

## Verification Results
| Endpoint | Final Total | HappyPath | Boundary | Negative | Pass/Fail |
|---|---:|---:|---:|---:|---|

## Risks
1.

## Rollback Plan
1.

## Definition Of Done Check
- [ ] Every endpoint has >= 10 cases
- [ ] Distribution rule satisfied
- [ ] No critical duplicate scenarios
- [ ] No runtime regression detected
```

## 7. JSON Report Template

Use this structure if JSON output is preferred:

```json
{
	"scope": {
		"branch": "",
		"targetModule": "",
		"goal": "minimum 10 test cases per endpoint"
	},
	"scanSummary": {
		"endpointsScanned": 0,
		"totalGeneratedCasesObserved": 0,
		"endpointsBelowThreshold": 0
	},
	"gapMatrix": [
		{
			"endpoint": "",
			"currentTotal": 0,
			"happyPath": 0,
			"boundary": 0,
			"negative": 0,
			"missingTo10": 0,
			"notes": ""
		}
	],
	"rootCauses": [
		""
	],
	"changesImplemented": [
		""
	],
	"verificationResults": [
		{
			"endpoint": "",
			"finalTotal": 0,
			"happyPath": 0,
			"boundary": 0,
			"negative": 0,
			"pass": true
		}
	],
	"risks": [
		""
	],
	"rollbackPlan": [
		""
	],
	"definitionOfDone": {
		"everyEndpointAtLeast10": false,
		"distributionSatisfied": false,
		"noCriticalDuplicates": false,
		"noRuntimeRegression": false
	}
}
```

## 8. Ready-To-Use Agent Prompt

Use this prompt when assigning work to an AI Agent:

```text
Follow docs/AI_AGENT_SCAN_IMPLEMENT_STANDARD.md.

Task:
1. Scan current test generation flow and produce a gap report.
2. Implement changes so each endpoint has at least 10 test cases with minimum distribution: 2 HappyPath, 3 Boundary, 5 Negative.
3. Add validation gates to prevent below-threshold output.
4. Verify and output report only in Markdown or JSON.

Constraints:
- Preserve existing architecture and naming style.
- Do not skip repo safety gates if DB/module/docker wiring changes.
- Provide explicit pass/fail for every endpoint in final report.
```
