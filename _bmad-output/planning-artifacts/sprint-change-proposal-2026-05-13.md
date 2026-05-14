---
project: Hexalith.Parties
date: 2026-05-13
workflow: bmad-correct-course
mode: incremental
status: approved
trigger: implementation-readiness-report-2026-05-13
approvedBy: Jérôme
approvedAt: 2026-05-13
---

# Sprint Change Proposal: Planning Artifact Alignment After Readiness Review

## 1. Issue Summary

The implementation readiness assessment completed on 2026-05-13 found that the PRD, architecture, UX, and epics are broadly complete, but not yet clean enough for future implementation planning. The core problem is not missing requirement coverage. The PRD contains 74 functional requirements, the epics coverage map claims 74/74 FR coverage, and UX documents exist for the admin portal and party picker.

The blocker is artifact alignment drift after the EventStore-fronted architecture pivot. Epic 12 exists in implementation artifacts and sprint status, but `epics.md` references it as canonical without including it as a planning epic. The same document also contains an invalid `FR75` reference, MCP tool naming drift between PRD and architecture, historical quality-gate stories that could be copied as a poor slicing pattern, and stale picker packaging wording.

### Evidence

- `implementation-readiness-report-2026-05-13.md` status: `NEEDS WORK`.
- `epics.md` references Epic 12 and Stories 12.7/12.8 as canonical, but has no Epic 12 section.
- `sprint-change-proposal-2026-05-07.md` defines Epic 12 and Stories 12.0-12.10.
- `sprint-status.yaml` records Epic 12 and all 11 Epic 12 stories as `done`.
- Epic 11 summary in `epics.md` says `FR75`, but the PRD ends at FR74.
- PRD still names MCP tools as `search_parties` and `list_parties`, while architecture/Epic 12 use `find_parties` and `delete_party`.
- Story 10.3 still mentions `npm package or similar`, while UX and architecture now define a FrontComposer/Blazor picker over the EventStore-fronted client boundary.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact | Required Action |
| ---- | ------ | --------------- |
| Epic 10 | Historical admin/picker scope is superseded by Epic 12 consumer-facing work. | Mark Epic 10 as historical/audit scope and point active work to Stories 12.7 and 12.8. |
| Epic 11 | Summary references non-existent `FR75`. | Replace with valid FR traceability and explain Tenants integration support for FR39-FR41 and FR61. |
| Epic 12 | Exists and is complete in implementation artifacts, but missing from `epics.md`. | Add/summarize Epic 12 in `epics.md` or explicitly make `sprint-change-proposal-2026-05-07.md` + implementation story files canonical. |
| Epic 5 | MCP naming is inconsistent with PRD. | Align PRD and traceability notes to the architecture/Epic 12 tool contract. |
| Epics 1-5 | Historical test-only stories could mislead future slicing. | Keep as historical records, but warn that future stories must include tests in behavior stories unless a reusable harness is created. |

### Story Impact

- Story 1.2 needs a stronger note that its broad contract bootstrap is historical and not a future slicing pattern.
- Stories 1.5, 2.3, 3.4, 4.3, and 5.4 need a consolidated historical quality-gate warning.
- Story 10.3 needs picker delivery wording aligned to the approved Party Picker UX addendum.
- Stories 12.7 and 12.8 should be treated as the active admin/picker implementation path.

### Artifact Conflicts

| Artifact | Conflict | Required Update |
| -------- | -------- | --------------- |
| `epics.md` | Missing Epic 12 section despite referencing Epic 12 as canonical. | Add summarized Epic 12 section and update execution notes. |
| `epics.md` | Invalid `FR75`. | Replace with valid FRs and traceability note. |
| `epics.md` | Quality-gate stories and Story 1.2 may be reused incorrectly. | Add historical/non-pattern notes. |
| `epics.md` | Story 10.3 stale package wording. | Replace with FrontComposer/Blazor/EventStore-fronted picker wording. |
| `prd.md` | MCP tool list still uses pre-architecture names. | Update MCP tool list and related journey/success language to canonical names. |
| `architecture.md` | Already records the naming decision but should remain the canonical explanation. | No structural architecture change required; optionally add a cross-reference to this proposal. |
| UX docs | Already aligned. | No UX change required. |
| `sprint-status.yaml` | Already records Epic 12 and supersession. | No status change required for this proposal. |

### Technical Impact

No code rollback or implementation rewrite is required. The proposal is a planning-artifact correction that preserves completed Epic 12 implementation evidence.

## 3. Recommended Approach

**Selected path:** Direct Adjustment.

### Rationale

The readiness issues are documentation and traceability defects, not product-scope failures. Epic 12 already exists, all Epic 12 story files exist, and sprint status records Epic 12 as done. The safest path is to update the planning artifacts so future agents and humans see the same canonical implementation direction.

### Alternatives Considered

- **Potential rollback:** Not viable. Completed Epic 12 work is the desired platform direction and is supported by sprint status and retrospectives.
- **PRD MVP review:** Not needed. MVP goals remain valid; the issue is naming/topology alignment, not product value.
- **New fundamental replan:** Not needed. The EventStore-fronted pivot already handled the major replan on 2026-05-07.

### Effort and Risk

- Effort: Medium. Requires careful document edits across `epics.md` and `prd.md`.
- Risk: Low to Medium. The biggest risk is accidentally weakening traceability or reintroducing stale REST/MCP assumptions.
- Timeline impact: Low. No implementation pause is needed beyond correcting the artifacts and re-running readiness.

## 4. Detailed Change Proposals

### Change 1: Add Epic 12 Canonical Summary To `epics.md`

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

**Old:**

```markdown
3. Epic 12: EventStore-fronted architecture pivot and consumer migration.
4. Epic 10: administration and picker user experience, consumed through the FrontComposer/EventStore/Parties client boundary.
```

**New:**

```markdown
3. Epic 12: EventStore-fronted architecture pivot and consumer migration. Epic 12 is already materialized in `_bmad-output/implementation-artifacts/12-*.md`, tracked in `_bmad-output/implementation-artifacts/sprint-status.yaml`, and sourced from `sprint-change-proposal-2026-05-07.md`. The summarized Epic 12 section below is canonical for readiness and traceability.
4. Epic 10: historical administration and picker scope. Consumer-facing admin and picker implementation is superseded by Epic 12 stories 12.7 and 12.8; Epic 10 remains for audit/history only.
```

Add after Epic 11 and before Epic 10:

```markdown
## Epic 12: EventStore-Fronted Architecture Pivot

Hexalith.Parties moves to the canonical platform topology: clients submit commands and queries to EventStore, which routes them to Parties-hosted actors. Parties is a thin actor/projection host, MCP runs as a separate thin host, and admin/picker surfaces consume the EventStore-fronted Parties client boundary.

**Status:** Completed in sprint status.
**Source:** `sprint-change-proposal-2026-05-07.md`
**Story files:** `_bmad-output/implementation-artifacts/12-0-*.md` through `12-10-*.md`

Stories:
- Story 12.0: EventStore-to-Parties actor invocation feasibility spike
- Story 12.1: AppHost recomposition
- Story 12.2: Parties actor host
- Story 12.3: Validation relocation and tenant-auth ownership
- Story 12.4: Server Tier-1/Tier-2 test rewrite
- Story 12.5: Parties client thin wrapper
- Story 12.6: Parties MCP thin host
- Story 12.7: Admin portal rebuild on FrontComposer and EventStore queries
- Story 12.8: Picker rewrite
- Story 12.9: Sample and getting-started documentation updates
- Story 12.10: Deployment validation and topology fitness rewrite
```

**Rationale:** Fixes the readiness blocker without inventing new scope. Epic 12 already exists and is complete, but `epics.md` does not expose it as a canonical planning artifact.

### Change 2: Correct Epic 11 FR Traceability

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

**Old:**

```markdown
**FRs covered:** FR39, FR40, FR41, FR75
```

**New:**

```markdown
**FRs covered:** FR39, FR40, FR41, FR61

**Traceability note:** Epic 11 is primarily architecture- and topology-driven. It strengthens tenant lifecycle, membership, role, and configuration integration for the tenant-isolation requirements already expressed by FR39-FR41. Story 11.4 also supports FR61 through deployment validation of Tenants subscription/configuration. There is no PRD requirement `FR75`.
```

**Rationale:** Removes the invalid `FR75` reference while preserving real requirement coverage.

### Change 3: Normalize MCP Tool Naming

**Artifacts:** `_bmad-output/planning-artifacts/prd.md`, `_bmad-output/planning-artifacts/epics.md`, `_bmad-output/planning-artifacts/architecture.md`

**Decision:** Keep architecture/Epic 12 tool names as canonical: `find_parties`, `get_party`, `create_party`, `update_party`, `delete_party`.

**Old PRD:**

```markdown
MCP server: 5 tools (`search_parties`, `get_party`, `create_party`, `update_party`, `list_parties`)
```

**New PRD:**

```markdown
MCP server: 5 tools (`find_parties`, `get_party`, `create_party`, `update_party`, `delete_party`); `find_parties` covers search and list modes; `delete_party` maps to soft deactivation, not GDPR erasure; `create_party` returns complete party; forgiving input schemas with sensible defaults and clear validation errors
```

**New traceability note:**

```markdown
MCP tool naming decision: `find_parties`, `get_party`, `create_party`, `update_party`, and `delete_party` are canonical after architecture refinement and Epic 12. `find_parties` unifies search/list behavior. `delete_party` is retained as an AI-ergonomic MCP alias for soft deactivation and must never be documented as GDPR erasure.
```

**Rationale:** Completed architecture and Epic 12 implementation already use `find_parties`/`delete_party`. Updating PRD/traceability avoids churn and makes the contract explicit.

### Change 4: Consolidate Historical Quality-Gate Story Warning

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

Add near `Planning Readiness Reconciliation`:

```markdown
Historical quality-gate stories: Stories 1.5, 2.3, 3.4, 4.3, and 5.4 are retained as historical sprint records. They must not be used as the pattern for new implementation planning. Future behavior stories must include their own unit, integration, accessibility, privacy, and fitness acceptance criteria unless the test work creates a reusable independent harness.
```

Shorten repeated per-story notes to:

```markdown
Planning note: historical quality-gate story; see Planning Readiness Reconciliation.
```

**Rationale:** Keeps audit history but prevents future sprint planning from copying test-only stories as independent value slices.

### Change 5: Strengthen Story 1.2 Contract-Slicing Warning

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

**Old:**

```markdown
Planning note: this story was completed historically with a broader contract surface. Do not use that historical breadth as the pattern for future stories. Contact/identifier contracts belong with Epic 2 behavior, projection query models with Epic 3 behavior, composite result contracts with Epic 4 behavior, MCP-specific schemas with Epic 5 behavior, and GDPR/privacy/search contracts with the relevant v1.1 stories.
```

**New:**

```markdown
Planning note: Story 1.2 is a historical contract-bootstrap story, not a future slicing pattern. For any new or revised work, contracts must be introduced with the first behavior that consumes them: lifecycle in Epic 1, contact/identifier contracts in Epic 2, projection query models in Epic 3, composite result contracts in Epic 4, MCP schemas in Epic 5 or Epic 12, and GDPR/privacy/search contracts in v1.1 stories. Do not add broad public contract surface ahead of consuming behavior unless a sprint change explicitly approves it.
```

**Rationale:** Strengthens the warning into an implementation rule and accounts for Epic 12 as the current MCP/client boundary.

### Change 6: Replace Story 10.3 Legacy Picker Packaging Wording

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

**Old:**

```markdown
**Given** the party picker component
**When** used in different consuming applications
**Then** it is independently deployable (published as an npm package or similar)
**And** it supports theming/customization for host app visual consistency
```

**New:**

```markdown
**Given** the party picker component
**When** used in different consuming applications
**Then** it is delivered as a FrontComposer/Blazor picker surface through the accepted Parties client/EventStore gateway configuration
**And** the durable selection contract is the party ID
**And** it supports host-provided request/auth context without persisting, refreshing, parsing, or logging tokens
```

Add planning note:

```markdown
Planning note: Story 10.3 is historical. The active picker implementation path is Story 12.8 plus `ux-party-picker-2026-05-12.md`. Do not use npm/package wording as authoritative for implementation.
```

**Rationale:** Aligns Story 10.3 with the approved picker UX addendum and the EventStore-fronted/FrontComposer direction.

## 5. Implementation Handoff

### Scope Classification

**Moderate.** This is a planning/backlog correction, not production code implementation. It requires careful artifact edits and follow-up validation.

### Handoff Recipients

- **Product Owner / Scrum Master:** Apply the `epics.md` corrections, preserve audit history, and ensure future story creation uses the corrected canonical sequence.
- **Product Manager / Architect:** Approve the MCP naming decision and ensure PRD/architecture wording remains aligned with the EventStore-fronted topology.
- **Developer agent:** After artifact approval, make the document edits and re-run implementation readiness.

### Success Criteria

1. `epics.md` contains or explicitly canonizes Epic 12 and Stories 12.0-12.10.
2. `epics.md` no longer references `FR75`.
3. PRD, architecture, and epics use the same MCP tool names.
4. Story 10.3 no longer implies npm/TypeScript packaging as the active path.
5. Historical test-only stories and broad bootstrap contract stories are clearly marked as historical patterns, not future slicing guidance.
6. Re-running `bmad-check-implementation-readiness` no longer reports the 2026-05-13 blockers.

## 6. Checklist Status

| Item | Status | Notes |
| ---- | ------ | ----- |
| 1.1 Triggering story | N/A | Trigger was readiness assessment, not a failed implementation story. |
| 1.2 Core problem | Done | Artifact alignment drift after Epic 12 pivot. |
| 1.3 Evidence | Done | Readiness report, epics, PRD, architecture, UX, sprint change proposal, sprint status, Epic 12 story files. |
| 2.1 Current epic impact | Done | Epic 10 historical scope and Epic 12 missing-from-planning issue identified. |
| 2.2 Epic-level changes | Done | Add/canonize Epic 12, fix Epic 11 traceability, mark Epic 10 historical. |
| 2.3 Remaining epics | Done | Epic 5 naming, Stories 1.2/quality-gate notes affected. |
| 2.4 New/obsolete epics | Done | No new epic required; Epic 12 already exists. |
| 2.5 Epic order | Done | Planning sequence should show 1-9, 11, 12, then historical 10. |
| 3.1 PRD conflicts | Action-needed | MCP tool names need PRD update. MVP scope unchanged. |
| 3.2 Architecture conflicts | Done | Architecture already records tool naming decision and D20 FrontComposer direction. |
| 3.3 UX conflicts | Done | UX docs align; no UX update required. |
| 3.4 Other artifacts | Done | Sprint status already records Epic 12; no status change needed. |
| 4.1 Direct adjustment | Viable | Medium effort, low-to-medium risk. |
| 4.2 Rollback | Not viable | No completed implementation should be reverted. |
| 4.3 PRD MVP review | Not viable | MVP goals remain intact. |
| 4.4 Recommended path | Done | Direct Adjustment. |
| 5.1 Issue summary | Done | Captured in this proposal. |
| 5.2 Impact summary | Done | Captured in Sections 2 and 4. |
| 5.3 Recommended path | Done | Direct Adjustment. |
| 5.4 MVP impact | Done | No MVP scope reduction. |
| 5.5 Handoff | Done | Moderate planning correction; PO/SM + PM/Architect + Developer. |
| 6.1 Checklist review | Done | All applicable sections addressed; action-needed items documented. |
| 6.2 Proposal accuracy | Done | Proposal reviewed for consistency against readiness report and source artifacts. |
| 6.3 User approval | Done | Approved by Jérôme on 2026-05-13. |
| 6.4 Sprint status update | N/A | No sprint-status change proposed. |
| 6.5 Handoff confirmation | Done | Route approved as moderate planning correction for PO/SM, PM/Architect, and Developer follow-up. |

## 7. Approval and Routing

**Approval:** Approved by Jérôme on 2026-05-13.

**Change scope:** Moderate.

**Artifacts modified by this workflow:**

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-13.md`

**Artifacts proposed for follow-up edits:**

- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/planning-artifacts/prd.md`
- Optional traceability note in `_bmad-output/planning-artifacts/architecture.md`

**Routed to:**

- Product Owner / Scrum Master for backlog and `epics.md` correction.
- Product Manager / Architect for MCP naming and topology wording approval.
- Developer agent for applying approved artifact edits and re-running implementation readiness.
