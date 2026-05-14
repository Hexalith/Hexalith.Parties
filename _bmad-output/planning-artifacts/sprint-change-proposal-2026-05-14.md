---
project: Hexalith.Parties
date: 2026-05-14
workflow: bmad-correct-course
mode: batch
status: implemented
trigger: implementation-readiness-report-2026-05-14
---

# Sprint Change Proposal: Readiness Artifact Repair

## 1. Issue Summary

The 2026-05-14 implementation readiness report found that the planning set was complete in product scope but inconsistent in canonical implementation direction. The blockers were a missing Epic 12 planning path in `epics.md`, an invalid Epic 11 FR reference, MCP tool-name drift between PRD/architecture/epics, and historical story wording that could mislead future implementation agents.

## 2. Impact Analysis

- Epic 12 existed in implementation artifacts and sprint status, but `epics.md` did not include it as a canonical planning epic.
- Epic 11 listed an FR number outside the PRD range, making traceability ambiguous.
- PRD still used the older MCP names while architecture, Epic 12, and implementation used `find_parties` and `delete_party`.
- Story 10.3 still carried legacy picker packaging wording, and several historical quality-gate stories needed clearer non-pattern guidance.

## 3. Recommended Approach

Direct adjustment. No product replan or rollback is required because Epic 12 is already complete and tracked in `_bmad-output/implementation-artifacts/sprint-status.yaml`. The repair is to align the selected planning artifacts with the accepted EventStore-fronted topology and canonical MCP contract.

## 4. Detailed Changes Applied

- Updated `_bmad-output/planning-artifacts/epics.md` to add a canonical Epic 12 summary with Stories 12.0-12.10, source files, status, and FR traceability.
- Replaced Epic 11's invalid traceability with valid FR39, FR40, FR41, and FR61 coverage.
- Added the canonical MCP tool naming decision to `epics.md`: `find_parties`, `get_party`, `create_party`, `update_party`, and `delete_party`.
- Updated `_bmad-output/planning-artifacts/prd.md` so MCP success criteria, MVP scope, journeys, and tool inventory use the canonical tool names.
- Updated `_bmad-output/planning-artifacts/architecture.md` so the MCP naming validation row reflects the resolved PRD/architecture alignment.
- Strengthened historical wording in `epics.md`: Story 1.2 is marked as a historical contract-bootstrap story, quality-gate stories point to the reconciliation note, Story 11.2 has a complete actor line, and Story 10.3 no longer contains npm/package wording.

## 5. Implementation Handoff

Scope classification: Moderate planning correction, completed directly.

Success criteria:

- Primary planning inputs no longer contain the invalid FR reference.
- Primary planning inputs use the canonical MCP tool names.
- `epics.md` contains the Epic 12 canonical path and explicitly names Stories 12.7 and 12.8.
- Historical story wording is clear enough that future agents should follow Epic 12 and behavior-story slicing rather than stale historical implementation wording.

## 6. Checklist Summary

- [x] Trigger and evidence understood from `implementation-readiness-report-2026-05-14.md`.
- [x] Epic impact assessed: Epic 10, Epic 11, and Epic 12 required planning clarification.
- [x] PRD, architecture, and epics conflicts assessed and repaired.
- [x] Direct adjustment selected; rollback and MVP review rejected as unnecessary.
- [x] Artifact edits applied and checked with focused text searches.
