---
project_name: Hexalith.Parties
user_name: Jerome
date: 2026-05-12
change_scope: Moderate
status: approved
trigger_report: implementation-readiness-report-2026-05-12.md
---

# Sprint Change Proposal: Planning Readiness Correction

## 1. Issue Summary

The implementation-readiness report generated on 2026-05-12 found the planning package at **NEEDS WORK**. This is not a product-scope or requirements-coverage failure: the PRD is complete, all required planning artifacts exist, and the epics map 74/74 functional requirements.

The issue is that the canonical planning artifact, `_bmad-output/planning-artifacts/epics.md`, still carries older planning shapes that no longer match the safer implementation direction now visible in the implementation artifacts:

- Story 1.2 is written as a large upfront contract inventory.
- Epic 11 is numbered after Epic 10 even though it must execute before Epic 10.
- Several test-only stories read as standalone delivery stories.
- Epic 9 mixes GDPR compliance with Memories-backed search.
- Epic 10 still says TypeScript admin portal, while the current UX, project context, and Story 12.7 establish a FrontComposer/Razor direction.

The trigger is a planning-quality finding discovered during readiness assessment, not a failed implementation approach. The correct response is a focused planning reconciliation so future agents read the current direction from the planning sources without having to infer it from later story files.

## 2. Impact Analysis

### Epic Impact

| Area | Impact | Required Change |
| --- | --- | --- |
| Epic 1 | Story 1.2 is already historically completed, but it models the wrong future slicing pattern. | Add an explicit planning erratum: completed historical contract work remains complete; future contract additions must be sliced with the consuming behavior story. |
| Epics 10/11 | Execution order is correct in practice, but numbering and source text are confusing. | Preserve existing IDs for audit, add an implementation sequence table, and mark Epic 11 as a prerequisite for Epic 10. |
| Epic 9 | Story 9.6 Memories-backed search is implemented/implemented-facing, but cohesion is weak under a pure GDPR title. | Rename/reframe Epic 9 as compliance plus v1.1 privacy/search extensions, or move Story 9.6 under an explicit search/intelligence lane if a full re-shard is approved later. |
| Epic 10 | Source text conflicts with FrontComposer UX and current implementation artifacts. | Update Epic 10 source wording to FrontComposer/Razor and point active consumer-facing work to Epic 12 stories 12.7 and 12.8. |
| Epic 12 | Already exists in implementation artifacts and sprint status as the EventStore-fronted pivot. | Cross-reference it from `epics.md` so readiness checks know Epic 12 supersedes older REST/admin/frontend assumptions. |

### Story Impact

The following story source changes are proposed in `epics.md`; no completed story status should be reverted:

1. Story 1.2 keeps its historical status but gains a warning that it is not a reusable slicing pattern.
2. Test-only stories 1.5, 2.3, 3.4, 4.3, and 5.4 are reclassified in planning language as quality gates or engineering tasks attached to behavior stories.
3. Stories 10.1-10.3 are updated to match FrontComposer/Razor direction and/or explicitly marked as superseded by Epic 12 successors where applicable.
4. Story 9.6 is either moved to a search/intelligence subsection or clearly called out as a v1.1 search extension within a broadened Epic 9 scope.
5. Story 12.7 and Story 12.8 remain the active consumer-facing admin/picker implementation path.

### Artifact Conflicts

**PRD:** Minor wording update recommended. The PRD still says "TypeScript admin portal" for v1.2. Replace that with FrontComposer/Razor/Blazor wording aligned to `ux-admin-portal-2026-05-10.md` and Story 12.7.

**Architecture:** Minor addendum recommended. The architecture currently covers admin/frontend at the FR65-FR67 level but does not fully specify FrontComposer routing, localization, deep-linking, fail-closed state clearing, contract-unavailable gating, focus management, or live regions. Add these either to the architecture frontend section or to a referenced FrontComposer admin addendum.

**UX:** Existing `ux-admin-portal-2026-05-10.md` is the authoritative admin UX direction. It does not cover FR67 party picker, so the picker needs a small UX addendum or acceptance criteria carried by Story 12.8.

**Sprint status:** No status changes should be made before approval. The status file already records Epic 12 and notes that Epic 12 supersedes the consumer-facing scope of earlier admin/frontend stories.

## 3. Recommended Approach

Use **Direct Adjustment: Planning Reconciliation**.

Do not roll back implementation work. Do not reopen completed stories solely to repair the original planning shape. Do not reduce MVP scope. The implementation artifacts already contain the newer direction: EventStore-fronted topology, FrontComposer admin portal, typed Parties client, separate MCP host, and picker rewrite.

The recommended change is to update the planning documents so the canonical PRD/epics/architecture/UX set tells the same story as the implementation artifacts.

Effort: Medium.

Risk: Low to medium. The main risk is breaking historical story references by renumbering. To avoid that, preserve existing story IDs and add explicit sequencing/supersession notes rather than rewriting history.

## 4. Detailed Change Proposals

### 4.1 Epics Document

File: `_bmad-output/planning-artifacts/epics.md`

#### Add Readiness Reconciliation Note

OLD:

```markdown
## Epic List
```

NEW:

```markdown
## Planning Readiness Reconciliation

The canonical implementation direction is now the EventStore-fronted plan recorded in `sprint-change-proposal-2026-05-07.md`, `sprint-status.yaml`, and Epic 12 story files. Earlier REST, TypeScript-admin, and upfront-contract wording in this document is retained only where it describes historical story origin. Current implementation and future story creation must follow the sequence and supersession notes below.

Execution sequence:
1. Epics 1-9: domain, discovery, integration, operations, GDPR/privacy/search foundation.
2. Epic 11: Hexalith.Tenants integration for Parties. This is a prerequisite for admin/frontend work.
3. Epic 12: EventStore-fronted architecture pivot and consumer migration.
4. Epic 10: administration and picker user experience, consumed through the FrontComposer/EventStore/Parties client boundary.
```

Rationale: Preserves existing IDs used by completed artifacts while removing ambiguity about execution order.

#### Reframe Story 1.2

OLD:

```markdown
### Story 1.2: Domain Contracts - Complete Type Definitions

As a developer,
I want all party domain types (commands, events, value objects, state, models, results, enums) defined in the Contracts project,
So that the domain model contracts are stable before aggregate and projection implementation begins.
```

NEW:

```markdown
### Story 1.2: Domain Contracts - Initial Party Lifecycle Contract Slice

As a developer,
I want the initial party lifecycle contracts needed by the first aggregate and retrieval stories,
So that the domain model can grow incrementally with the behavior that consumes each contract.

Planning note: this story was completed historically with a broader contract surface. Do not use that historical breadth as the pattern for future stories. Contact/identifier contracts belong with Epic 2 behavior, projection query models with Epic 3 behavior, composite result contracts with Epic 4 behavior, MCP-specific schemas with Epic 5 behavior, and GDPR/privacy/search contracts with the relevant v1.1 stories.
```

Rationale: Fixes future slicing guidance without rewriting completed implementation history.

#### Reclassify Test-Only Stories

OLD examples:

```markdown
### Story 1.5: Party Aggregate Tier 1 Unit Tests
### Story 2.3: Contact Channel & Identifier Unit Tests
### Story 3.4: Projection Unit & Integration Tests
### Story 4.3: Composite Command Unit Tests
### Story 5.4: MCP Tools Tests & Architectural Fitness
```

NEW:

```markdown
Planning note: this quality gate is attached to the related behavior stories and exists to preserve the historical sprint record. Future epics should express comparable unit, integration, accessibility, privacy, and fitness expectations as acceptance criteria or engineering tasks under the behavior story unless the test work creates a reusable independent test harness.
```

Rationale: Keeps traceability while preventing future "test story as user story" drift.

#### Resolve Epic 9 Cohesion

OLD:

```markdown
## Epic 9: GDPR Compliance (v1.1)
```

NEW:

```markdown
## Epic 9: GDPR, Privacy, and v1.1 Search Extensions

Administrators can fulfill GDPR obligations and the platform can support privacy-safe v1.1 search and audit extensions. GDPR erasure, consent, restriction, portability, processing records, per-party keys, erased-state behavior, and subscriber notification remain the primary compliance scope. Temporal name queries and Hexalith.Memories-backed search are explicitly v1.1 extensions and must retain privacy, erasure, and tenant-isolation guarantees.
```

Rationale: Avoids pretending Memories-backed search is pure GDPR while preserving the implemented story lane.

#### Resolve Epic 10 Frontend Direction

OLD:

```markdown
Administrators can browse, search, and inspect party records and process party-level GDPR requests (erasure, restriction, consent, export) via a TypeScript admin portal.
```

NEW:

```markdown
Administrators can browse, search, and inspect party records and process party-level GDPR requests via a FrontComposer-based Blazor/Razor admin portal that consumes EventStore-fronted Parties client/query/command contracts. Generic event and stream browsing is delegated to EventStore Admin UI through safe deep-links. Tenant lifecycle, membership, roles, and configuration management remain owned by Hexalith.Tenants admin capabilities.
```

Rationale: Aligns `epics.md` with project context, UX, and Story 12.7.

#### Add Party Picker UX Coverage

NEW:

```markdown
Story 10.3 / Story 12.8 party picker UX note: FR67 requires a small picker UX specification or explicit acceptance criteria covering embedding shape, type-ahead behavior, selected-id contract, host auth injection, stale-response clearing, accessibility, localization, and privacy-safe rendering/storage/logging. This is separate from the admin portal UX artifact.
```

Rationale: Closes the FR67 UX gap found by the readiness report.

### 4.2 PRD Update

File: `_bmad-output/planning-artifacts/prd.md`

OLD:

```markdown
- TypeScript admin portal (browse, search, inspect, GDPR operations)
```

NEW:

```markdown
- FrontComposer-based Blazor/Razor admin portal (browse, search, inspect, GDPR operations), consuming the EventStore-fronted Parties client/query/command boundary
```

Rationale: The repo context, UX artifact, and Story 12.7 all point to FrontComposer. The TypeScript phrase is stale.

### 4.3 Architecture Update

File: `_bmad-output/planning-artifacts/architecture.md`

Add or amend the Administration frontend section with:

```markdown
The Parties Admin Portal is a FrontComposer domain surface implemented with Blazor/Razor and Fluent UI Blazor. It registers Parties-domain views with the FrontComposer shell, reads through EventStore query/client abstractions, routes supported commands through the typed Parties client/EventStore command boundary, and delegates generic event/stream browsing to EventStore Admin UI safe deep-links.

The portal must fail closed and clear sensitive state on sign-out, missing tenant, non-admin user, tenant switch, stale response, forbidden, not found, gone/erased, timeout, malformed response, and contract-unavailable failures. Labels, dates, counts, status messages, validation messages, and operation outcomes must be localized. Focus management, keyboard access, non-color-only state, and polite status announcements are part of the frontend architecture contract.
```

Rationale: Moves critical UX constraints out of a single story artifact and into architecture-level guidance.

### 4.4 UX Update

File: new or existing picker UX addendum under `_bmad-output/planning-artifacts/`

Create `ux-party-picker-2026-05-12.md` or add a section to the existing UX package:

```markdown
# Party Picker UX Addendum

The party picker is an embeddable FrontComposer/Blazor component for party search and selection. It is not an admin portal, party editor, tenant selector, GDPR surface, or EventStore stream browser.

Required behavior: debounced type-ahead display-name search, bounded result count, selected party id callback, disabled/read-only states, retry, empty, loading, degraded/local-only, unauthorized, forbidden, not-found, gone/erased, stale-response clearing, tenant/user/context change cleanup, keyboard operation, localized labels/status text, and encoded rendering only.

The durable selection contract is the party id. Names, contacts, identifiers, consent text, search text, tenant ids, tokens, raw ProblemDetails, and raw query payloads must not be placed in storage keys, telemetry dimensions, URLs, logs, filenames, or JavaScript event payloads.
```

Rationale: Gives FR67 a design source instead of relying only on story ACs.

## 5. Checklist Results

| Checklist Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | [x] Done | Trigger is the 2026-05-12 readiness report; no single implementation story failed. |
| 1.2 Core problem defined | [x] Done | Planning-shape and artifact drift, not requirements coverage. |
| 1.3 Evidence gathered | [x] Done | Report, `epics.md`, PRD TypeScript line, UX FrontComposer artifact, project context, Story 12.7, and sprint status. |
| 2.1 Current epic viability | [x] Done | Epics remain viable with planning reconciliation. |
| 2.2 Epic-level changes | [x] Done | Reframe Epic 9, add sequence/supersession notes, align Epic 10 frontend direction. |
| 2.3 Future epic impact | [x] Done | Future story creation should slice contracts/tests with behavior stories. |
| 2.4 New/obsolete epics | [x] Done | No new epic needed; Epic 12 already exists. |
| 2.5 Order/priority | [x] Done | Preserve IDs; add execution sequence to avoid breaking references. |
| 3.1 PRD conflict | [x] Done | Replaced stale TypeScript admin wording after approval. |
| 3.2 Architecture conflict | [x] Done | Added FrontComposer admin architecture guidance after approval. |
| 3.3 UI/UX conflict | [x] Done | Added picker UX addendum for FR67 after approval. |
| 3.4 Secondary artifacts | [x] Done | `sprint-status.yaml` already records Epic 12 supersession; no change before approval. |
| 4.1 Direct adjustment | [x] Viable | Recommended. |
| 4.2 Rollback | [N/A] Skip | No implementation rollback is justified. |
| 4.3 PRD MVP review | [N/A] Skip | MVP scope remains intact. |
| 4.4 Path selected | [x] Done | Direct planning reconciliation. |
| 5.1 Issue summary | [x] Done | Included above. |
| 5.2 Impact and artifacts | [x] Done | Included above. |
| 5.3 Recommended path | [x] Done | Included above. |
| 5.4 MVP impact/action plan | [x] Done | No MVP scope change; artifact edits only. |
| 5.5 Handoff | [x] Done | See below. |
| 6.1-6.2 Final review | [x] Done | Proposal is ready for user review. |
| 6.3 User approval | [x] Done | Approved by user on 2026-05-12. |
| 6.4 Sprint status update | [N/A] Skip | No approved epic/story changes yet. |
| 6.5 Handoff confirmation | [x] Done | Artifact-only handoff completed after approval. |

## 6. Implementation Handoff

Scope classification: **Moderate**.

Handoff recipients:

- Product Owner / Scrum Master: update `epics.md` with reconciliation, supersession, sequencing, and story-shape corrections.
- Product Manager: update the PRD admin portal wording from TypeScript to FrontComposer-based Blazor/Razor.
- Architect: add the FrontComposer admin architecture guidance and reference the EventStore Admin UI deep-link boundary.
- UX Designer: add the party picker UX addendum for FR67.
- Developer agent: after approval, apply the artifact edits only; do not change production code as part of this correction.

Success criteria:

- A rerun of implementation readiness no longer reports the TypeScript vs FrontComposer mismatch.
- `epics.md` explicitly shows Epic 11 before Epic 10 in execution sequence without breaking historical IDs.
- Story 1.2 and test-only story language no longer teach future agents to create large upfront model stories or standalone test stories.
- Epic 9 wording honestly covers GDPR plus v1.1 search/audit extensions.
- FR67 has either a picker UX addendum or explicit UX acceptance criteria.
- No story statuses are changed unless separately approved.

## 7. Approval State

This proposal was **approved by the user on 2026-05-12**. PRD, epic, architecture, and picker UX edits have been applied. `sprint-status.yaml` was not changed because no story status or epic status change was required.
